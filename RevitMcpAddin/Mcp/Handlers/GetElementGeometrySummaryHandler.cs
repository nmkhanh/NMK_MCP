using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class GetElementGeometrySummaryHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public GetElementGeometrySummaryHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "get_element_geometry_summary";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Returns a lightweight geometry summary for one Revit element.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    elementId = new { type = "string", description = "Revit ElementId to inspect." },
                    detailLevel = new { type = "string", description = "Geometry detail level: Coarse, Medium, Fine, or Undefined. Default: Medium.", @default = "Medium" },
                    includeNonVisibleObjects = new { type = "boolean", description = "Include non-visible geometry objects. Default: false.", @default = false }
                },
                required = new[] { "elementId" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<ElementGeometrySummaryRequest>() ?? new ElementGeometrySummaryRequest();
                var result = await _revitService.GetElementGeometrySummaryAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("GetElementGeometrySummaryHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to get geometry summary: {ex.Message}");
            }
        }
    }
}
