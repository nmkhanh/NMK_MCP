using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class RemoveViewFromSheetHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public RemoveViewFromSheetHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "remove_view_from_sheet";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Removes a placed view from a sheet by viewportId or by sheetId + viewId.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    viewportId = new { type = "string", description = "Viewport ElementId to delete." },
                    sheetId = new { type = "string", description = "Sheet ElementId, used with viewId if viewportId is omitted." },
                    viewId = new { type = "string", description = "View ElementId, used with sheetId if viewportId is omitted." }
                },
                required = new string[] { }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<RemoveViewFromSheetRequest>() ?? new RemoveViewFromSheetRequest();
                if (string.IsNullOrWhiteSpace(request.ViewportId) &&
                    (string.IsNullOrWhiteSpace(request.SheetId) || string.IsNullOrWhiteSpace(request.ViewId)))
                {
                    return ToolHandlerResult.FromError("Provide either 'viewportId' or both 'sheetId' and 'viewId'.");
                }

                return ToolHandlerResult.FromJson(await _revitService.RemoveViewFromSheetAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("RemoveViewFromSheetHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to remove view from sheet: {ex.Message}");
            }
        }
    }
}
