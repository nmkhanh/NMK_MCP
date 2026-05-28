using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class HideElementsInViewHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public HideElementsInViewHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "hide_elements_in_view";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Permanently hides elements in a target view, or the active view when viewId is omitted.",
            InputSchema = ViewBatchSchema("Revit ElementIds to hide in the view.")
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<ViewElementIdsRequest>() ?? new ViewElementIdsRequest();
                var result = await _revitService.HideElementsInViewAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("HideElementsInViewHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to hide elements in view: {ex.Message}");
            }
        }

        internal static object ViewBatchSchema(string elementIdsDescription) => new
        {
            type = "object",
            properties = new
            {
                viewId = new { type = "string", description = "Optional target View ElementId. Uses active view when omitted." },
                elementIds = new { type = "array", description = elementIdsDescription, items = new { type = "string" } },
                dryRun = new { type = "boolean", description = "Validate inputs without modifying the model. Default: false.", @default = false },
                maxItems = new { type = "integer", description = "Safety cap. Default: 100, hard max: 500.", @default = 100 }
            },
            required = new[] { "elementIds" }
        };
    }
}
