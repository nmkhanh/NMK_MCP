using Newtonsoft.Json.Linq;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class GetRevitLinksHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public GetRevitLinksHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "get_revit_links";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Lists Revit link instances and loaded linked document info.", InputSchema = new { type = "object", properties = new { }, required = Array.Empty<string>() } };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.GetRevitLinksAsync(cancellationToken)); }
            catch (Exception ex) { Logger.Error("GetRevitLinksHandler error", ex); return ToolHandlerResult.FromError($"Failed to get Revit links: {ex.Message}"); }
        }
    }
}
