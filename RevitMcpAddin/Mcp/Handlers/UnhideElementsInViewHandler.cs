using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UnhideElementsInViewHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public UnhideElementsInViewHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "unhide_elements_in_view";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Unhides elements in a target view, or the active view when viewId is omitted.",
            InputSchema = HideElementsInViewHandler.ViewBatchSchema("Revit ElementIds to unhide in the view.")
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<ViewElementIdsRequest>() ?? new ViewElementIdsRequest();
                var result = await _revitService.UnhideElementsInViewAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("UnhideElementsInViewHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to unhide elements in view: {ex.Message}");
            }
        }
    }
}
