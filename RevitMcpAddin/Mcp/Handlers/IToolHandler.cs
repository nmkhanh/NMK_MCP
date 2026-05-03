using Newtonsoft.Json.Linq;

namespace RevitMcpAddin.Mcp.Handlers
{
    /// <summary>
    /// Contract that every MCP tool handler must implement.
    /// Handlers are registered by name in <see cref="McpRouter"/> and invoked
    /// when Claude Code sends a <c>tools/call</c> request.
    /// </summary>
    public interface IToolHandler
    {
        /// <summary>
        /// Unique tool identifier exposed to MCP clients (e.g. "get_elements").
        /// Must match the name advertised in <see cref="GetDefinition"/>.
        /// </summary>
        string ToolName { get; }

        /// <summary>
        /// Returns the full tool descriptor used in the <c>tools/list</c> response,
        /// including the JSON Schema for input validation.
        /// </summary>
        McpToolDefinition GetDefinition();

        /// <summary>
        /// Executes the tool with the given JSON arguments.
        /// Implementations MUST dispatch all Revit API calls through
        /// <see cref="Services.RevitService"/> (which in turn uses
        /// <see cref="Services.AsyncQueueService"/> + RevitTask.RunAsync).
        /// </summary>
        /// <param name="arguments">Parsed JSON arguments from the client.</param>
        /// <param name="cancellationToken">Propagated from the HTTP request lifetime.</param>
        Task<ToolHandlerResult> HandleAsync(
            JObject? arguments,
            CancellationToken cancellationToken = default);
    }
}
