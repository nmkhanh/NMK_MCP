using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateSheetHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public UpdateSheetHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "update_sheet";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Updates a Revit sheet number and/or name.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    sheetId = new { type = "string", description = "Sheet ElementId." },
                    sheetNumber = new { type = "string", description = "New sheet number." },
                    sheetName = new { type = "string", description = "New sheet name." }
                },
                required = new[] { "sheetId" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<UpdateSheetRequest>() ?? new UpdateSheetRequest();
                if (string.IsNullOrWhiteSpace(request.SheetId)) return ToolHandlerResult.FromError("'sheetId' is required.");
                return ToolHandlerResult.FromJson(await _revitService.UpdateSheetAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("UpdateSheetHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to update sheet: {ex.Message}");
            }
        }
    }
}
