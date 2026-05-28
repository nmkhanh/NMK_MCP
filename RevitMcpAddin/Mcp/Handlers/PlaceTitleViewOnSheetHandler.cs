using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class PlaceTitleViewOnSheetHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public PlaceTitleViewOnSheetHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "place_title_view_on_sheet";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Places a drafting or legend view used as a title graphic on a sheet.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    sheetId = new { type = "string", description = "Target sheet ElementId." },
                    viewId = new { type = "string", description = "Drafting View or Legend ElementId to place." },
                    x = new { type = "number", description = "Sheet X coordinate." },
                    y = new { type = "number", description = "Sheet Y coordinate." },
                    unit = new { type = "string", description = "Coordinate unit: feet, meters, or millimeters. Default: feet.", @default = "feet" },
                    viewportTypeId = new { type = "string", description = "Optional viewport type ElementId." }
                },
                required = new[] { "sheetId", "viewId", "x", "y" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<PlaceViewOnSheetRequest>() ?? new PlaceViewOnSheetRequest();
                if (string.IsNullOrWhiteSpace(request.SheetId)) return ToolHandlerResult.FromError("'sheetId' is required.");
                if (string.IsNullOrWhiteSpace(request.ViewId)) return ToolHandlerResult.FromError("'viewId' is required.");
                return ToolHandlerResult.FromJson(await _revitService.PlaceViewOnSheetAsync(request, true, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("PlaceTitleViewOnSheetHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to place title view on sheet: {ex.Message}");
            }
        }
    }
}
