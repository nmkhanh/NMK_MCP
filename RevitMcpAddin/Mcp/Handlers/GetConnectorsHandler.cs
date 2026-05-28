using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class GetConnectorsHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public GetConnectorsHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "get_connectors";
        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Lists MEP connectors for a MEPCurve or MEP FamilyInstance.",
            InputSchema = new
            {
                type = "object",
                properties = new { elementId = new { type = "string", description = "ElementId to inspect." } },
                required = new[] { "elementId" }
            }
        };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.GetConnectorsAsync(arguments?.ToObject<GetConnectorsRequest>() ?? new GetConnectorsRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("GetConnectorsHandler error", ex); return ToolHandlerResult.FromError($"Failed to get connectors: {ex.Message}"); }
        }
    }
}
