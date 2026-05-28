using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class PinElementsHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public PinElementsHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "pin_elements";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Pins Revit elements by ElementId.",
            InputSchema = DeleteElementsHandler.BatchSchema("Revit ElementIds to pin.")
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<ElementIdsRequest>() ?? new ElementIdsRequest();
                var result = await _revitService.PinElementsAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("PinElementsHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to pin elements: {ex.Message}");
            }
        }
    }
}
