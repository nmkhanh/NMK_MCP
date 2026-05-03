'use strict';

// ============================================================
//  revitClient.js
//
//  Manages the persistent SSE connection from this STDIO bridge
//  to the Revit MCP add-in HTTP server (default: localhost:5000).
//
//  Protocol:
//    1. Open  GET  <REVIT_BASE_URL>/sse
//    2. On "endpoint" event, record the POST URL for messages
//    3. Send JSON-RPC requests via  POST  <messagesUrl>
//    4. Wait for the matching JSON-RPC response via the SSE stream
//
//  ALL output goes to process.stderr — never to stdout (stdout is
//  reserved exclusively for JSON-RPC responses to Claude).
// ============================================================

const EventSource = require('eventsource');

const REVIT_BASE_URL       = (process.env.REVIT_BASE_URL || 'http://localhost:5000').replace(/\/$/, '');
const REVIT_SSE_URL        = `${REVIT_BASE_URL}/sse`;
const REQUEST_TIMEOUT_MS   = parseInt(process.env.REQUEST_TIMEOUT_MS || '120000', 10);
const RECONNECT_BASE_MS    = 3_000;
const RECONNECT_MAX_MS     = 30_000;

const debug = (...a) => process.env.DEBUG && console.error('[revit]', ...a);

// ── Resolve a raw endpoint path/URL to a full URL ────────────
function resolveUrl(raw) {
  raw = raw.trim();
  if (/^https?:\/\//.test(raw)) return raw;
  const path = raw.startsWith('/') ? raw : `/${raw}`;
  return `${REVIT_BASE_URL}${path}`;
}

// ── RevitClient ───────────────────────────────────────────────
class RevitClient {
  constructor() {
    /** @type {string|null} full POST URL received via the endpoint event */
    this._messagesUrl = null;
    /** @type {EventSource|null} */
    this._es          = null;
    /** @type {boolean} */
    this._connected   = false;
    /** Map<string, {resolve, reject, timer}> — in-flight request promises */
    this._pending     = new Map();
    /** monotonic request id counter */
    this._counter     = 1;
    this._reconnectMs = RECONNECT_BASE_MS;
  }

  // ── Public: start connection (non-blocking) ─────────────────
  /**
   * Open the SSE connection to Revit in the background.
   * @returns {Promise<void>} resolves when first endpoint URL is received
   */
  connect() {
    return new Promise((resolve, reject) => {
      this._openEventSource(resolve, reject);
    });
  }

  // ── Internal: open/re-open EventSource ──────────────────────
  _openEventSource(onFirstConnect, onFirstError) {
    debug(`Connecting → ${REVIT_SSE_URL}`);

    this._es = new EventSource(REVIT_SSE_URL, {
      headers: {
        // Prevents ngrok browser-interstitial from blocking the SSE stream
        'ngrok-skip-browser-warning': '1'
      }
    });

    let firstConnectSettled = false;

    // Startup timeout — if Revit isn't available in 10 s, let the router know
    const startupTimer = setTimeout(() => {
      if (!firstConnectSettled) {
        firstConnectSettled = true;
        onFirstError(new Error(`Revit unreachable within 10 s (${REVIT_SSE_URL})`));
      }
    }, 10_000);

    // ── endpoint event ────────────────────────────────────────
    this._es.addEventListener('endpoint', (evt) => {
      this._messagesUrl = resolveUrl(evt.data);
      this._connected   = true;
      this._reconnectMs = RECONNECT_BASE_MS;   // reset backoff on success
      debug(`Endpoint received → ${this._messagesUrl}`);

      if (!firstConnectSettled) {
        firstConnectSettled = true;
        clearTimeout(startupTimer);
        onFirstConnect();
      }
    });

    // ── message event (JSON-RPC responses from Revit) ─────────
    this._es.addEventListener('message', (evt) => {
      let msg;
      try {
        msg = JSON.parse(evt.data);
      } catch {
        debug('Unreadable SSE message — not valid JSON, ignoring.');
        return;
      }

      const id = String(msg.id ?? '');
      const pending = this._pending.get(id);
      if (pending) {
        clearTimeout(pending.timer);
        this._pending.delete(id);
        pending.resolve(msg);
      } else {
        debug(`Received SSE message with no pending handler (id=${id})`);
      }
    });

    // ── error / disconnect ────────────────────────────────────
    this._es.onerror = () => {
      this._connected   = false;
      this._messagesUrl = null;

      // Reject all in-flight requests immediately
      for (const [, p] of this._pending) {
        clearTimeout(p.timer);
        p.reject(new Error('Revit SSE connection lost.'));
      }
      this._pending.clear();

      const delay = this._reconnectMs;
      this._reconnectMs = Math.min(this._reconnectMs * 1.5, RECONNECT_MAX_MS);

      console.error(`[revit] Connection lost — reconnecting in ${Math.round(delay / 1000)} s…`);

      // Settle the startup promise on first failure so the server can start
      // in degraded mode rather than hanging forever.
      if (!firstConnectSettled) {
        firstConnectSettled = true;
        clearTimeout(startupTimer);
        onFirstError(new Error(`Revit SSE connection failed (${REVIT_SSE_URL})`));
      }

      // Schedule reconnect (new EventSource, no callbacks needed for subsequent attempts)
      setTimeout(() => {
        try { this._es.close(); } catch { /* ignore */ }
        this._es = null;
        this._openEventSource(() => {
          console.error('[revit] Reconnected successfully.');
        }, () => { /* reconnect failure already logged above */ });
      }, delay);
    };
  }

  // ── Public: send a JSON-RPC request, await response via SSE ─
  /**
   * @param {string}  method   JSON-RPC method name
   * @param {object}  [params] JSON-RPC params
   * @returns {Promise<object>} full JSON-RPC response object
   */
  async send(method, params = {}) {
    if (!this._connected || !this._messagesUrl) {
      throw new Error('Revit is not connected. Start Revit 2026 with the RevitMCP add-in.');
    }

    const id   = `stdio-${this._counter++}`;
    const body = JSON.stringify({ jsonrpc: '2.0', id, method, params });

    debug(`→ ${method}`, params);

    return new Promise((resolve, reject) => {
      // Timeout: reject if Revit doesn't respond in time
      const timer = setTimeout(() => {
        this._pending.delete(String(id));
        reject(new Error(`Revit request timed out after ${REQUEST_TIMEOUT_MS} ms (method: ${method})`));
      }, REQUEST_TIMEOUT_MS);

      this._pending.set(String(id), { resolve, reject, timer });

      // Fire-and-forget HTTP POST; the response arrives asynchronously via SSE
      fetch(this._messagesUrl, {
        method:  'POST',
        headers: {
          'Content-Type':               'application/json',
          'ngrok-skip-browser-warning':  '1'
        },
        body
      }).catch((err) => {
        // POST itself failed (network error) — clean up the pending entry
        clearTimeout(timer);
        if (this._pending.delete(String(id))) {
          reject(new Error(`POST to Revit failed: ${err.message}`));
        }
      });
    });
  }

  // ── Convenience wrappers ─────────────────────────────────────

  /** Call a Revit tool by name with the given arguments. */
  async callTool(name, args) {
    return this.send('tools/call', { name, arguments: args ?? {} });
  }

  /**
   * Fetch the live tool list from Revit.
   * Returns null if Revit is offline or the request fails.
   * @returns {Promise<object[]|null>}
   */
  async fetchTools() {
    try {
      const resp = await this.send('tools/list', {});
      return resp?.result?.tools ?? null;
    } catch (err) {
      debug('fetchTools failed:', err.message);
      return null;
    }
  }

  // ── Accessors ────────────────────────────────────────────────
  get isConnected()  { return this._connected; }
  get revitBaseUrl() { return REVIT_BASE_URL; }
}

// Export a singleton so the router and server share one connection
module.exports = new RevitClient();
