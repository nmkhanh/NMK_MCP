'use strict';

// ============================================================
//  mcpServer.js  —  MCP STDIO transport entry point
//
//  Claude Desktop spawns this process and communicates via:
//    stdin  → newline-delimited JSON-RPC 2.0 requests
//    stdout → newline-delimited JSON-RPC 2.0 responses
//    stderr → all log / debug output (never touches stdout)
//
//  The server connects to the Revit MCP add-in at
//  REVIT_BASE_URL (default: http://localhost:5000) in the
//  background.  While Revit is offline the server still
//  answers initialize / tools/list from hardcoded definitions
//  and returns a helpful error for tools/call.
//
//  Usage (Claude Desktop claude_desktop_config.json):
//  {
//    "mcpServers": {
//      "revit": {
//        "command": "node",
//        "args": ["C:/Test/RevitMcpStdio/mcpServer.js"],
//        "env": {
//          "REVIT_BASE_URL": "http://localhost:5000"
//        }
//      }
//    }
//  }
//
//  Environment variables:
//    REVIT_BASE_URL       — Revit add-in base URL (default: http://localhost:5000)
//    REQUEST_TIMEOUT_MS   — per-request timeout in ms   (default: 30000)
//    DEBUG                — set to any truthy value to enable verbose stderr logging
// ============================================================

// Load .env file if present (optional — env vars can also be passed by Claude Desktop)
try {
  require('dotenv').config({ override: false });
} catch { /* dotenv is optional */ }

const readline = require('readline');
const router   = require('./mcpRouter');
const revit    = require('./revitClient');

// ── stdout write helper ──────────────────────────────────────
// This is the ONLY place that writes to process.stdout.
function sendToClient(obj) {
  const line = JSON.stringify(obj);
  process.stdout.write(line + '\n');
  if (process.env.DEBUG) {
    console.error('[server] → ', line.length > 300 ? line.slice(0, 300) + '…' : line);
  }
}

// ── stdin reader ─────────────────────────────────────────────
const rl = readline.createInterface({
  input:     process.stdin,
  terminal:  false,   // stdin is not a TTY when spawned by Claude Desktop
  crlfDelay: Infinity // handle \r\n line endings on Windows
});

rl.on('line', async (raw) => {
  const line = raw.trim();
  if (!line) return;   // ignore blank lines

  if (process.env.DEBUG) {
    console.error('[server] ← ', line.length > 300 ? line.slice(0, 300) + '…' : line);
  }

  // ── Parse JSON ─────────────────────────────────────────────
  let rpc;
  try {
    rpc = JSON.parse(line);
  } catch {
    // JSON parse error — spec says respond with id:null
    sendToClient({
      jsonrpc: '2.0',
      id:      null,
      error: { code: -32700, message: 'Parse error: input is not valid JSON' }
    });
    return;
  }

  // ── Minimal structural validation ──────────────────────────
  if (!rpc || typeof rpc !== 'object' || typeof rpc.method !== 'string') {
    sendToClient({
      jsonrpc: '2.0',
      id:      rpc?.id ?? null,
      error: { code: -32600, message: 'Invalid Request: missing or non-string "method"' }
    });
    return;
  }

  // ── Dispatch ───────────────────────────────────────────────
  let response;
  try {
    response = await router.dispatch(rpc);
  } catch (err) {
    console.error('[server] Unexpected dispatch error:', err);
    response = {
      jsonrpc: '2.0',
      id:      rpc.id ?? null,
      error: { code: -32603, message: `Internal error: ${err.message}` }
    };
  }

  // null means "no reply" (e.g. notifications/initialized)
  if (response !== null && response !== undefined) {
    sendToClient(response);
  }
});

// ── stdin EOF — Claude Desktop closed the connection ─────────
rl.on('close', () => {
  console.error('[server] stdin closed — exiting.');
  process.exit(0);
});

// ── Global error guards (prevent the process from dying) ─────
process.on('uncaughtException', (err) => {
  console.error('[server] Uncaught exception:', err);
});
process.on('unhandledRejection', (reason) => {
  console.error('[server] Unhandled rejection:', reason);
});

// ── Startup ───────────────────────────────────────────────────
(async function start() {
  console.error('[server] RevitMCP STDIO Server starting…');
  console.error(`[server] Revit endpoint : ${process.env.REVIT_BASE_URL || 'http://localhost:5000'}`);
  console.error(`[server] Debug logging  : ${process.env.DEBUG ? 'ON' : 'OFF'}`);
  console.error('[server] Ready — listening on stdin.');

  // Connect to Revit in the background.
  // We do NOT await this so stdin processing starts immediately.
  // Claude Desktop can begin its initialize handshake while we connect.
  revit.connect()
    .then(async () => {
      console.error('[server] Connected to Revit.');
      await router.initTools();
    })
    .catch((err) => {
      console.error('[server] Warning: Revit not available at startup:', err.message);
      console.error('[server] Using hardcoded fallback tools. Revit will be retried automatically.');
    });
})();
