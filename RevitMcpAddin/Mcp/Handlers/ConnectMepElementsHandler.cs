using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class ConnectMepElementsHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public ConnectMepElementsHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "connect_mep_elements";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Connects two MEP connectors by index or nearest XYZ.", InputSchema = ConnectorPairSchema() };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.ConnectMepElementsAsync(arguments?.ToObject<ConnectMepElementsRequest>() ?? new ConnectMepElementsRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("ConnectMepElementsHandler error", ex); return ToolHandlerResult.FromError($"Failed to connect MEP elements: {ex.Message}"); }
        }
        internal static object ConnectorRefSchema() => new
        {
            type = "object",
            properties = new
            {
                elementId = new { type = "string", description = "ElementId that owns the connector." },
                connectorIndex = new { type = "integer", description = "Optional connector index from get_connectors." },
                x = new { type = "number", description = "Optional connector target X; nearest connector is used." },
                y = new { type = "number", description = "Optional connector target Y; nearest connector is used." },
                z = new { type = "number", description = "Optional connector target Z; nearest connector is used." }
            },
            required = new[] { "elementId" }
        };
        internal static object ConnectorPairSchema() => new
        {
            type = "object",
            properties = new
            {
                first = ConnectorRefSchema(),
                second = ConnectorRefSchema(),
                unit = new { type = "string", description = "Coordinate unit for XYZ connector references. Default: feet.", @default = "feet" }
            },
            required = new[] { "first", "second" }
        };
    }
}
