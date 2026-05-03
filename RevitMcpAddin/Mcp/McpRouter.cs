using RevitMcpAddin.Mcp.Handlers;
using RevitMcpAddin.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMcpAddin.Mcp
{
    /// <summary>
    /// Routes incoming MCP JSON-RPC method calls to the correct handler
    /// or composes built-in protocol responses.
    ///
    /// Supported methods:
    ///   • initialize              – MCP handshake
    ///   • notifications/initialized – client acknowledgement (fire-and-forget)
    ///   • tools/list             – advertise available tools
    ///   • tools/call             – execute a named tool
    ///   • ping                   – health check
    /// </summary>
    public class McpRouter
    {
        #region Fields

        private const string ServerProtocolVersion = "2024-11-05";
        private const string ServerName = "RevitMCP";
        private const string ServerVersion = "1.0.0";

        /// <summary>Registered handlers keyed by tool name.</summary>
        private readonly Dictionary<string, IToolHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Constructor / Registration

        public McpRouter() { }

        /// <summary>Register a tool handler. Call before starting the HTTP server.</summary>
        public McpRouter Register(IToolHandler handler)
        {
            _handlers[handler.ToolName] = handler;
            Logger.Info($"McpRouter: registered tool '{handler.ToolName}'");
            return this;
        }

        /// <summary>Names of all registered tools (for UI display).</summary>
        public IEnumerable<string> ToolNames => _handlers.Keys;

        #endregion

        #region Routing

        /// <summary>
        /// Dispatch a parsed <see cref="McpRequest"/> to the appropriate handler.
        /// Returns <c>null</c> for notifications (no response must be sent).
        /// </summary>
        public async Task<McpResponse?> RouteAsync(McpRequest request, CancellationToken ct = default)
        {
            Logger.Debug($"McpRouter: method='{request.Method}' id={request.Id}");

            try
            {
                return request.Method switch
                {
                    "initialize"                 => HandleInitialize(request),
                    "notifications/initialized"  => null,   // notification, no reply
                    "tools/list"                 => HandleToolsList(request),
                    "tools/call"                 => await HandleToolCallAsync(request, ct),
                    "ping"                       => McpResponse.Success(request.Id, new { }),
                    _                            => McpResponse.MethodNotFound(request.Id, request.Method)
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"McpRouter unhandled exception for method '{request.Method}'", ex);
                return McpResponse.InternalError(request.Id, ex.Message);
            }
        }

        #endregion

        #region Built-in method handlers

        private static McpResponse HandleInitialize(McpRequest request)
        {
            // Parse client info for logging (best-effort)
            var initParams = request.Params?.ToObject<InitializeParams>();
            if (initParams?.ClientInfo is { } ci)
                Logger.Info($"MCP client connected: {ci.Name} v{ci.Version}");

            return McpResponse.Success(request.Id, new
            {
                protocolVersion = ServerProtocolVersion,
                capabilities    = new { tools = new { } },
                serverInfo      = new { name = ServerName, version = ServerVersion }
            });
        }

        private McpResponse HandleToolsList(McpRequest request)
        {
            var tools = _handlers.Values
                                 .Select(h => h.GetDefinition())
                                 .ToList();

            return McpResponse.Success(request.Id, new { tools });
        }

        private async Task<McpResponse> HandleToolCallAsync(McpRequest request, CancellationToken ct)
        {
            // Deserialise tools/call params
            ToolCallParams? callParams;
            try
            {
                callParams = request.Params?.ToObject<ToolCallParams>();
            }
            catch (JsonException ex)
            {
                return McpResponse.InvalidParams(request.Id, $"Bad params: {ex.Message}");
            }

            if (callParams == null || string.IsNullOrWhiteSpace(callParams.Name))
                return McpResponse.InvalidParams(request.Id, "'name' is required in tools/call params");

            if (!_handlers.TryGetValue(callParams.Name, out var handler))
                return McpResponse.MethodNotFound(request.Id, callParams.Name);

            Logger.Info($"McpRouter: executing tool '{callParams.Name}'");

            ToolHandlerResult result = await handler.HandleAsync(callParams.Arguments, ct);

            return McpResponse.Success(request.Id, result);
        }

        #endregion
    }
}
