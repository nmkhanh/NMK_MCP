using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class GetViewSheetSetsHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public GetViewSheetSetsHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "get_view_sheet_sets";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Lists named Revit ViewSheetSets used by PrintManager.",
            InputSchema = new { type = "object", properties = new { }, required = new string[] { } }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.GetViewSheetSetsAsync(cancellationToken)); }
            catch (Exception ex)
            {
                Logger.Error("GetViewSheetSetsHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to get ViewSheetSets: {ex.Message}");
            }
        }
    }
}
