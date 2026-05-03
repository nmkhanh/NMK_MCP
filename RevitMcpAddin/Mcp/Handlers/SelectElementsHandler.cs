using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    /// <summary>
    /// MCP tool: <b>select_elements_by_ids</b>
    /// Selects and highlights elements in the active Revit UI by ElementId.
    ///
    /// Input:
    /// <code>
    /// {
    ///   "ids":           [687839, 665246, 670566],  // required
    ///   "zoom":          true,                       // optional — zoom/fit to selection (default: true)
    ///   "clearPrevious": true                        // optional — clear prior selection first (default: true)
    /// }
    /// </code>
    ///
    /// Output:
    /// <code>
    /// { "success": true, "count": 3, "selectedIds": [687839, 665246, 670566] }
    /// </code>
    /// </summary>
    public sealed class SelectElementsHandler : IToolHandler
    {
        #region Fields

        private readonly RevitService _revitService;

        #endregion

        #region Constructor

        public SelectElementsHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        #endregion

        #region IToolHandler

        public string ToolName => "select_elements_by_ids";

        public McpToolDefinition GetDefinition() => new()
        {
            Name        = ToolName,
            Description = "Select and highlight elements in the Revit UI by ElementId. " +
                          "Optionally zooms the active view to fit the selection.",
            InputSchema = new
            {
                type       = "object",
                properties = new
                {
                    ids = new
                    {
                        type        = "array",
                        description = "List of integer ElementIds to select.",
                        items       = new { type = "integer" }
                    },
                    zoom = new
                    {
                        type        = "boolean",
                        description = "Zoom the active view to fit the selected elements. Default: true.",
                        @default    = true
                    },
                    clearPrevious = new
                    {
                        type        = "boolean",
                        description = "Clear the existing selection before selecting new elements. Default: true.",
                        @default    = true
                    }
                },
                required = new[] { "ids" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(
            JObject? arguments,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // ── Parse ids ────────────────────────────────────────────
                var idsToken = arguments?["ids"] as JArray;
                if (idsToken == null || idsToken.Count == 0)
                {
                    // Empty list — clear selection and return early
                    var cleared = await _revitService.SelectElementsAsync(
                        new List<int>(), zoom: false, clearPrevious: true, ct: cancellationToken);
                    return ToolHandlerResult.FromJson(new
                    {
                        success     = true,
                        count       = 0,
                        selectedIds = Array.Empty<int>(),
                        message     = "Selection cleared (empty ids list)."
                    });
                }

                List<int> ids;
                try
                {
                    ids = idsToken.Select(t => t.Value<int>()).ToList();
                }
                catch (Exception ex)
                {
                    return ToolHandlerResult.FromError($"'ids' must be an array of integers: {ex.Message}");
                }

                var zoom          = arguments?["zoom"]?.Value<bool>()          ?? true;
                var clearPrevious = arguments?["clearPrevious"]?.Value<bool>() ?? true;

                Logger.Info($"select_elements_by_ids: count={ids.Count}, zoom={zoom}, clearPrevious={clearPrevious}");

                // ── Delegate to RevitService ─────────────────────────────
                var result = await _revitService.SelectElementsAsync(ids, zoom, clearPrevious, cancellationToken);

                return ToolHandlerResult.FromJson(result);
            }
            catch (InvalidOperationException ex)
            {
                Logger.Warning($"SelectElementsHandler: {ex.Message}");
                return ToolHandlerResult.FromError(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Error("SelectElementsHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to select elements: {ex.Message}");
            }
        }

        #endregion
    }
}
