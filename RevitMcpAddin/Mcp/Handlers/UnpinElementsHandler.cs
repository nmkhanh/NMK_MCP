using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UnpinElementsHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public UnpinElementsHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "unpin_elements";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Unpins Revit elements by ElementId.",
            InputSchema = DeleteElementsHandler.BatchSchema("Revit ElementIds to unpin.")
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<ElementIdsRequest>() ?? new ElementIdsRequest();
                var result = await _revitService.UnpinElementsAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("UnpinElementsHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to unpin elements: {ex.Message}");
            }
        }
    }
}
