using Newtonsoft.Json.Linq;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class GetSelectedElementsHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public GetSelectedElementsHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "get_selected_elements";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Returns the currently selected elements in the Revit UI.",
            InputSchema = new { type = "object", properties = new { }, required = Array.Empty<string>() }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _revitService.GetSelectedElementsAsync(cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("GetSelectedElementsHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to get selected elements: {ex.Message}");
            }
        }
    }
}
