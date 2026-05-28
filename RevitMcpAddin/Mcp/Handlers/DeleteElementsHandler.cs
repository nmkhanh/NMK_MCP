using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class DeleteElementsHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public DeleteElementsHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "delete_elements";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Deletes Revit elements by ElementId, with dryRun and maxItems safety controls.",
            InputSchema = BatchSchema("Revit ElementIds to delete.")
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<ElementIdsRequest>() ?? new ElementIdsRequest();
                var result = await _revitService.DeleteElementsAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("DeleteElementsHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to delete elements: {ex.Message}");
            }
        }

        internal static object BatchSchema(string elementIdsDescription) => new
        {
            type = "object",
            properties = new
            {
                elementIds = new { type = "array", description = elementIdsDescription, items = new { type = "string" } },
                dryRun = new { type = "boolean", description = "Validate inputs without modifying the model. Default: false.", @default = false },
                maxItems = new { type = "integer", description = "Safety cap. Default: 100, hard max: 500.", @default = 100 }
            },
            required = new[] { "elementIds" }
        };
    }
}
