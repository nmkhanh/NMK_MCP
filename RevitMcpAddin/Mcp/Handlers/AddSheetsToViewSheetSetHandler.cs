using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class AddSheetsToViewSheetSetHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public AddSheetsToViewSheetSetHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "add_sheets_to_view_sheet_set";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Adds sheetIds to an existing ViewSheetSet.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string", description = "Existing ViewSheetSet name." },
                    sheetIds = new { type = "array", items = new { type = "string" }, description = "Sheet ids to add." }
                },
                required = new[] { "name", "sheetIds" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<ModifyViewSheetSetRequest>() ?? new ModifyViewSheetSetRequest();
                if (string.IsNullOrWhiteSpace(request.Name)) return ToolHandlerResult.FromError("'name' is required.");
                if (request.SheetIds.Count == 0) return ToolHandlerResult.FromError("'sheetIds' must contain at least one id.");
                return ToolHandlerResult.FromJson(await _revitService.AddSheetsToViewSheetSetAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("AddSheetsToViewSheetSetHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to add sheets to ViewSheetSet: {ex.Message}");
            }
        }
    }
}
