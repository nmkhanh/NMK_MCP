using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class GetViewsHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public GetViewsHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "get_views";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Returns Revit views with id, name, view type, template flag, scale, and level.",
            InputSchema = new { type = "object", properties = new { }, required = new string[] { } }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.GetViewsAsync(cancellationToken)); }
            catch (Exception ex)
            {
                Logger.Error("GetViewsHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to get views: {ex.Message}");
            }
        }
    }
}
