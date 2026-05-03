'use strict';

// ============================================================
//  mcpRouter.js
//
//  Dispatches incoming JSON-RPC 2.0 messages (MCP protocol).
//
//  Supported methods:
//    initialize              → return server capabilities
//    notifications/initialized → notification, no reply (return null)
//    ping                    → echo empty result
//    tools/list              → return tool definitions
//    tools/call              → forward to Revit, return result
//
//  All logging goes to stderr only — never stdout.
// ============================================================

const { TOOLS } = require('./tools');
const revit      = require('./revitClient');

const debug = (...a) => process.env.DEBUG && console.error('[router]', ...a);

// Live tool list — starts as hardcoded fallback, replaced on connect
let _tools = [...TOOLS];

// ── initTools ────────────────────────────────────────────────
/**
 * Try to load the authoritative tool list from the Revit add-in.
 * Safe to call after connection is established.
 * Falls back silently to the hardcoded list on any failure.
 */
async function initTools() {
  try {
    const liveTools = await revit.fetchTools();
    if (Array.isArray(liveTools) && liveTools.length > 0) {
      _tools = liveTools;
      console.error('[router] Live tools loaded from Revit:', _tools.map(t => t.name).join(', '));
    } else {
      console.error('[router] No live tools returned — using hardcoded fallback definitions.');
    }
  } catch (err) {
    console.error('[router] initTools error (non-fatal):', err.message);
  }
}

// ── dispatch ─────────────────────────────────────────────────
/**
 * Handle one JSON-RPC request/notification.
 *
 * @param   {{ jsonrpc: string, id?: any, method: string, params?: any }} rpc
 * @returns {Promise<object|null>}  JSON-RPC response, or null for notifications
 */
async function dispatch(rpc) {
  debug(`method="${rpc.method}" id=${JSON.stringify(rpc.id ?? null)}`);

  switch (rpc.method) {

    // ── initialize ──────────────────────────────────────────
    case 'initialize':
      return {
        jsonrpc: '2.0',
        id:      rpc.id,
        result: {
          protocolVersion: '2024-11-05',
          capabilities: {
            tools: {}
          },
          serverInfo: {
            name:    'RevitMCP-STDIO',
            version: '1.0.0'
          }
        }
      };

    // ── notifications/initialized ───────────────────────────
    // This is a notification — the MCP spec says we must NOT reply.
    case 'notifications/initialized':
      return null;

    // ── ping ────────────────────────────────────────────────
    case 'ping':
      return { jsonrpc: '2.0', id: rpc.id, result: {} };

    // ── tools/list ─────────────────────────────────────────
    case 'tools/list':
      return {
        jsonrpc: '2.0',
        id:      rpc.id,
        result:  { tools: _tools }
      };

    // ── tools/call ─────────────────────────────────────────
    case 'tools/call':
      return await _handleToolCall(rpc);

    // ── unknown ─────────────────────────────────────────────
    default:
      return {
        jsonrpc: '2.0',
        id:      rpc.id ?? null,
        error: {
          code:    -32601,
          message: `Method not found: ${rpc.method}`
        }
      };
  }
}

// ── _handleToolCall ───────────────────────────────────────────
async function _handleToolCall(rpc) {
  const name = rpc.params?.name;
  const args = rpc.params?.arguments ?? {};

  // Validate the tool name param
  if (!name || typeof name !== 'string') {
    return {
      jsonrpc: '2.0',
      id:      rpc.id,
      error: {
        code:    -32602,
        message: "Invalid params: 'params.name' (string) is required for tools/call"
      }
    };
  }

  // Graceful degraded response when Revit is offline
  if (!revit.isConnected) {
    return {
      jsonrpc: '2.0',
      id:      rpc.id,
      result: {
        content: [{
          type: 'text',
          text: `Revit is not connected.\n\n` +
                `Make sure Revit 2026 is open with the RevitMCP add-in running ` +
                `and the server is started (${revit.revitBaseUrl}).`
        }],
        isError: true
      }
    };
  }

  try {
    debug(`Tool call: ${name}(${JSON.stringify(args)})`);

    const revitResp = await revit.callTool(name, args);
    debug(`Tool result for ${name}:`, JSON.stringify(revitResp).slice(0, 200));

    // Revit returned a JSON-RPC error (e.g. unknown tool, internal error)
    if (revitResp.error) {
      return {
        jsonrpc: '2.0',
        id:      rpc.id,
        result: {
          content: [{
            type: 'text',
            text: `Revit error [${revitResp.error.code ?? 'unknown'}]: ${revitResp.error.message ?? JSON.stringify(revitResp.error)}`
          }],
          isError: true
        }
      };
    }

    // Happy path — pass Revit's result directly to Claude
    // revitResp.result is already in MCP ToolResult format: { content: [...], isError: bool }
    return {
      jsonrpc: '2.0',
      id:      rpc.id,
      result:  revitResp.result ?? { content: [{ type: 'text', text: '(empty result from Revit)' }] }
    };

  } catch (err) {
    console.error(`[router] Tool call "${name}" failed:`, err.message);
    return {
      jsonrpc: '2.0',
      id:      rpc.id,
      result: {
        content: [{ type: 'text', text: `Error calling "${name}": ${err.message}` }],
        isError: true
      }
    };
  }
}

module.exports = { dispatch, initTools };
