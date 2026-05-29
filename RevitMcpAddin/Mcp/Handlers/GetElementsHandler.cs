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
            Description = "Returns elements from the active Revit document filtered by category and optional " +
                          "parameter values. Supports both built-in parameters and shared parameters by name.",
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
                    },
                    useActiveView = new
                    {
                        type        = "boolean",
                        description = "When true, return only elements visible in the active view. Default: false.",
                        @default    = false
                    },
                    viewId = new
                    {
                        type        = "string",
                        description = "Optional view ElementId to scope the query. Overrides useActiveView when supplied."
                    },
                    parameterFilters = new
                    {
                        type        = "array",
                        description = "Optional filters on parameter values. " +
                                      "An element is returned only when it satisfies ALL filters. " +
                                      "Works with built-in parameters (e.g. 'Comments', 'Mark') " +
                                      "and shared parameters by their display name. " +
                                      "Checks both instance and type parameters. " +
                                      "Comparison is case-insensitive exact match.",
                        items = new
                        {
                            type       = "object",
                            properties = new
                            {
                                name = new
                                {
                                    type        = "string",
                                    description = "Parameter display name (e.g. 'Fire Rating', 'Mark', 'Comments')."
                                },
                                value = new
                                {
                                    type        = "string",
                                    description = "Expected parameter value (case-insensitive)."
                                }
                            },
                            required = new[] { "name", "value" }
                        }
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
                var useActiveView = arguments?["useActiveView"]?.Value<bool>() ?? false;
                var viewId = arguments?["viewId"]?.Value<string>();

                // Parse optional parameterFilters array — supports shared + built-in params
                List<(string Name, string Value)>? parameterFilters = null;
                var filtersToken = arguments?["parameterFilters"] as JArray;
                if (filtersToken != null && filtersToken.Count > 0)
                {
                    parameterFilters = new List<(string, string)>();
                    foreach (var item in filtersToken)
                    {
                        var n = item["name"]?.Value<string>();
                        var v = item["value"]?.Value<string>();
                        if (!string.IsNullOrWhiteSpace(n) && v != null)
                            parameterFilters.Add((n!, v));
                    }
                    if (parameterFilters.Count == 0) parameterFilters = null;
                }

                Logger.Info($"get_elements: category='{category}', includeParams={includeParameters}, " +
                            $"filters={parameterFilters?.Count ?? 0}, useActiveView={useActiveView}, viewId={viewId}");

                // ── Delegate to RevitService ─────────────────────────────
                var elements = await _revitService.GetElementsAsync(
                    category, 0, includeParameters, parameterFilters, useActiveView, viewId, cancellationToken);

                var summary = new
                {
                    category,
                    returnedCount = elements.Count,
                    includeParameters,
                    useActiveView,
                    viewId,
                    filterCount   = parameterFilters?.Count ?? 0,
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
