using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CopyElementsHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public CopyElementsHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "copy_elements";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Copies Revit elements by a translation vector and returns the copied ElementIds.",
            InputSchema = new MoveElementsHandler(_revitService).GetDefinition().InputSchema
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<MoveElementsRequest>() ?? new MoveElementsRequest();
                var result = await _revitService.CopyElementsAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("CopyElementsHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to copy elements: {ex.Message}");
            }
        }
    }
}
