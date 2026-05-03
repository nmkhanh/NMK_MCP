using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    /// <summary>
    /// MCP tool: <b>get_elements</b>
    /// Returns elements from the active document filtered by Revit category.
    ///
    /// Input:
    /// <code>
    /// {
    ///   "category": "Walls"   // required — BuiltInCategory suffix (Walls, Doors, Windows …)
    /// }
    /// </code>
    ///
    /// Output: JSON array of element objects.
    /// </summary>
    public sealed class GetElementsHandler : IToolHandler
    {
        #region Fields

        private readonly RevitService _revitService;

        #endregion

        #region Constructor

        public GetElementsHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        #endregion

        #region IToolHandler

        public string ToolName => "get_elements";

        public McpToolDefinition GetDefinition() => new()
        {
            Name        = ToolName,
            Description = "Returns elements from the active Revit document filtered by category. " +
                          "Use the BuiltInCategory suffix as the category name, e.g. 'Walls', 'Doors', 'Floors'.",
            InputSchema = new
            {
                type       = "object",
                properties = new
                {
                    category = new
                    {
                        type        = "string",
                        description = "Revit category name — use the BuiltInCategory OST_ suffix " +
                                      "(e.g. 'Walls', 'Doors', 'Windows', 'Floors', 'Columns')."
                    },
                    includeParameters = new
                    {
                        type        = "boolean",
                        description = "Include element parameters in the response. Defaults to false for speed. " +
                                      "Set to true only when you need parameter values.",
                        @default    = false
                    }
                },
                required = new[] { "category" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(
            JObject? arguments,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // ── Parse arguments ──────────────────────────────────────
                var category = arguments?["category"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(category))
                    return ToolHandlerResult.FromError("'category' parameter is required.");

                var includeParameters = arguments?["includeParameters"]?.Value<bool>() ?? false;

                Logger.Info($"get_elements: category='{category}', includeParams={includeParameters}");

                // ── Delegate to RevitService ─────────────────────────────
                var elements = await _revitService.GetElementsAsync(category, 0, includeParameters, cancellationToken);

                var summary = new
                {
                    category          = category,
                    returnedCount     = elements.Count,
                    includeParameters,
                    elements
                };

                return ToolHandlerResult.FromJson(summary);
            }
            catch (ArgumentException ex)
            {
                Logger.Warning($"GetElementsHandler invalid argument: {ex.Message}");
                return ToolHandlerResult.FromError(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Error("GetElementsHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to get elements: {ex.Message}");
            }
        }

        #endregion
    }
}
