using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class DeleteViewSheetSetHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public DeleteViewSheetSetHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "delete_view_sheet_set";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Deletes a named ViewSheetSet.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string", description = "ViewSheetSet name to delete." }
                },
                required = new[] { "name" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<DeleteViewSheetSetRequest>() ?? new DeleteViewSheetSetRequest();
                if (string.IsNullOrWhiteSpace(request.Name)) return ToolHandlerResult.FromError("'name' is required.");
                return ToolHandlerResult.FromJson(await _revitService.DeleteViewSheetSetAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("DeleteViewSheetSetHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to delete ViewSheetSet: {ex.Message}");
            }
        }
    }
}
