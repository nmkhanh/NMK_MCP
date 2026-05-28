using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class RemoveFromViewSheetSetHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public RemoveFromViewSheetSetHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "remove_from_view_sheet_set";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Removes viewIds and/or sheetIds from an existing ViewSheetSet.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string", description = "Existing ViewSheetSet name." },
                    viewIds = new { type = "array", items = new { type = "string" }, description = "View ids to remove." },
                    sheetIds = new { type = "array", items = new { type = "string" }, description = "Sheet ids to remove." }
                },
                required = new[] { "name" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<ModifyViewSheetSetRequest>() ?? new ModifyViewSheetSetRequest();
                if (string.IsNullOrWhiteSpace(request.Name)) return ToolHandlerResult.FromError("'name' is required.");
                if (request.ViewIds.Count == 0 && request.SheetIds.Count == 0)
                    return ToolHandlerResult.FromError("Provide at least one viewId or sheetId.");
                return ToolHandlerResult.FromJson(await _revitService.RemoveFromViewSheetSetAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("RemoveFromViewSheetSetHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to remove from ViewSheetSet: {ex.Message}");
            }
        }
    }
}
