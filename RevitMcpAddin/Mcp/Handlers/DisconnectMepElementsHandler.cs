using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class DisconnectMepElementsHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public DisconnectMepElementsHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "disconnect_mep_elements";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Disconnects two MEP connectors by index or nearest XYZ.", InputSchema = ConnectMepElementsHandler.ConnectorPairSchema() };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.DisconnectMepElementsAsync(arguments?.ToObject<ConnectMepElementsRequest>() ?? new ConnectMepElementsRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("DisconnectMepElementsHandler error", ex); return ToolHandlerResult.FromError($"Failed to disconnect MEP elements: {ex.Message}"); }
        }
    }
}
