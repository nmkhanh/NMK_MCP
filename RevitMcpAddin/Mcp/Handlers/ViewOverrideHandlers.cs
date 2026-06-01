using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public static class ViewOverrideHandlerRegistration
    {
        public static McpRouter RegisterViewOverrideHandlers(this McpRouter router, RevitService service)
        {
            return router
                .Register(new ViewOverrideToolHandler<GetViewOverridesRequest>(
                    service,
                    "get_view_overrides",
                    "Reads element, category, and filter override graphics in a view.",
                    ViewOverrideSchemas.GetViewOverrides,
                    (svc, req, ct) => svc.GetViewOverridesAsync(req, ct)))
                .Register(new ViewOverrideToolHandler<ElementOverridesInViewRequest>(
                    service,
                    "set_element_overrides_in_view",
                    "Applies graphic overrides to elements in a view.",
                    ViewOverrideSchemas.ElementOverrides,
                    (svc, req, ct) => svc.SetElementOverridesInViewAsync(req, ct)))
                .Register(new ViewOverrideToolHandler<ViewElementIdsRequest>(
                    service,
                    "clear_element_overrides_in_view",
                    "Clears element graphic overrides in a view.",
                    ViewOverrideSchemas.ElementOverrideClear,
                    (svc, req, ct) => svc.ClearElementOverridesInViewAsync(req, ct)))
                .Register(new ViewOverrideToolHandler<CategoryOverrideInViewRequest>(
                    service,
                    "set_category_overrides_in_view",
                    "Applies graphic overrides to one category in a view.",
                    ViewOverrideSchemas.CategoryOverride,
                    (svc, req, ct) => svc.SetCategoryOverridesInViewAsync(req, ct)))
                .Register(new ViewOverrideToolHandler<CategoryOverrideInViewRequest>(
                    service,
                    "clear_category_overrides_in_view",
                    "Clears category graphic overrides in a view.",
                    ViewOverrideSchemas.CategoryTarget,
                    (svc, req, ct) => svc.ClearCategoryOverridesInViewAsync(req, ct)))
                .Register(new ViewOverrideToolHandler<CategoryVisibilityInViewRequest>(
                    service,
                    "set_category_visibility_in_view",
                    "Shows or hides one category in a view.",
                    ViewOverrideSchemas.CategoryVisibility,
                    (svc, req, ct) => svc.SetCategoryVisibilityInViewAsync(req, ct)))
                .Register(new ViewOverrideToolHandler<FilterOverrideInViewRequest>(
                    service,
                    "set_filter_overrides_in_view",
                    "Applies graphic overrides and visibility settings to a view filter.",
                    ViewOverrideSchemas.FilterOverride,
                    (svc, req, ct) => svc.SetFilterOverridesInViewAsync(req, ct)))
                .Register(new ViewOverrideToolHandler<FilterOverrideInViewRequest>(
                    service,
                    "clear_filter_overrides_in_view",
                    "Clears graphic overrides for a view filter.",
                    ViewOverrideSchemas.FilterTarget,
                    (svc, req, ct) => svc.ClearFilterOverridesInViewAsync(req, ct)))
                .Register(new ViewOverrideToolHandler<FilterInViewRequest>(
                    service,
                    "add_filter_to_view",
                    "Adds a filter to a view and optionally applies visibility or overrides.",
                    ViewOverrideSchemas.FilterAdd,
                    (svc, req, ct) => svc.AddFilterToViewAsync(req, ct)))
                .Register(new ViewOverrideToolHandler<FilterInViewRequest>(
                    service,
                    "remove_filter_from_view",
                    "Removes a filter from a view.",
                    ViewOverrideSchemas.FilterTarget,
                    (svc, req, ct) => svc.RemoveFilterFromViewAsync(req, ct)))
                .Register(new ViewOverrideToolHandler<ViewDetailGraphicsRequest>(
                    service,
                    "set_view_detail_graphics",
                    "Updates detail level, display style, parts visibility, and discipline for a view.",
                    ViewOverrideSchemas.ViewDetailGraphics,
                    (svc, req, ct) => svc.SetViewDetailGraphicsAsync(req, ct)))
                .Register(new ViewOverrideToolHandler<ViewOverridePresetRequest>(
                    service,
                    "create_view_graphics_override_preset",
                    "Stores a named graphic override preset for this add-in session.",
                    ViewOverrideSchemas.Preset,
                    (svc, req, ct) => svc.CreateViewGraphicsOverridePresetAsync(req, ct)))
                .Register(new ViewOverrideToolHandler<ApplyViewOverridePresetRequest>(
                    service,
                    "apply_view_graphics_override_preset",
                    "Applies a named graphic override preset to elements, categories, or filters in a view.",
                    ViewOverrideSchemas.ApplyPreset,
                    (svc, req, ct) => svc.ApplyViewGraphicsOverridePresetAsync(req, ct)));
        }
    }

    internal sealed class ViewOverrideToolHandler<TRequest> : IToolHandler
        where TRequest : class, new()
    {
        private readonly RevitService _revitService;
        private readonly string _description;
        private readonly object _inputSchema;
        private readonly Func<RevitService, TRequest, CancellationToken, Task<object>> _execute;

        public ViewOverrideToolHandler(
            RevitService revitService,
            string toolName,
            string description,
            object inputSchema,
            Func<RevitService, TRequest, CancellationToken, Task<object>> execute)
        {
            _revitService = revitService;
            ToolName = toolName;
            _description = description;
            _inputSchema = inputSchema;
            _execute = execute;
        }

        public string ToolName { get; }

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = _description,
            InputSchema = _inputSchema
        };

        public async Task<ToolHandlerResult> HandleAsync(
            JObject? arguments,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<TRequest>() ?? new TRequest();
                return ToolHandlerResult.FromJson(await _execute(_revitService, request, cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return ToolHandlerResult.FromError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ToolHandlerResult.FromError(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Error($"{ToolName} handler error", ex);
                return ToolHandlerResult.FromError($"Failed to run {ToolName}: {ex.Message}");
            }
        }
    }

    internal static class ViewOverrideSchemas
    {
        private static object Object(Dictionary<string, object> properties, params string[] required)
            => new
            {
                type = "object",
                properties,
                required
            };

        private static Dictionary<string, object> Props(params (string Name, object Value)[] props)
            => props.ToDictionary(p => p.Name, p => p.Value);

        private static object String(string description)
            => new { type = "string", description };

        private static object Integer(string description)
            => new { type = "integer", description };

        private static object Boolean(string description)
            => new { type = "boolean", description };

        private static object StringArray(string description)
            => new { type = "array", description, items = new { type = "string" } };

        private static object Overrides => Object(Props(
            ("projectionLineColor", String("Projection line color as #RRGGBB.")),
            ("projectionLinePatternId", String("Projection line pattern ElementId.")),
            ("projectionLineWeight", Integer("Projection line weight.")),
            ("cutLineColor", String("Cut line color as #RRGGBB.")),
            ("cutLinePatternId", String("Cut line pattern ElementId.")),
            ("cutLineWeight", Integer("Cut line weight.")),
            ("surfaceForegroundPatternId", String("Surface foreground fill pattern ElementId.")),
            ("surfaceForegroundPatternColor", String("Surface foreground fill color as #RRGGBB.")),
            ("surfaceForegroundPatternVisible", Boolean("Surface foreground pattern visibility.")),
            ("surfaceBackgroundPatternId", String("Surface background fill pattern ElementId.")),
            ("surfaceBackgroundPatternColor", String("Surface background fill color as #RRGGBB.")),
            ("surfaceBackgroundPatternVisible", Boolean("Surface background pattern visibility.")),
            ("cutForegroundPatternId", String("Cut foreground fill pattern ElementId.")),
            ("cutForegroundPatternColor", String("Cut foreground fill color as #RRGGBB.")),
            ("cutForegroundPatternVisible", Boolean("Cut foreground pattern visibility.")),
            ("cutBackgroundPatternId", String("Cut background fill pattern ElementId.")),
            ("cutBackgroundPatternColor", String("Cut background fill color as #RRGGBB.")),
            ("cutBackgroundPatternVisible", Boolean("Cut background pattern visibility.")),
            ("transparency", Integer("Surface transparency from 0 to 100.")),
            ("halftone", Boolean("Halftone override.")),
            ("detailLevel", String("coarse, medium, fine, or undefined."))));

        private static Dictionary<string, object> TargetViewProps(params (string Name, object Value)[] props)
        {
            var all = new List<(string Name, object Value)> { ("viewId", String("Optional view ElementId. Defaults to active view.")) };
            all.AddRange(props);
            return Props(all.ToArray());
        }

        public static object GetViewOverrides => Object(TargetViewProps(
            ("elementIds", StringArray("ElementIds whose overrides should be read.")),
            ("categoryIds", StringArray("Category ElementIds whose overrides should be read.")),
            ("categories", StringArray("BuiltInCategory names or suffixes whose overrides should be read.")),
            ("filterIds", StringArray("Filter ElementIds to inspect. Defaults to filters already on the view.")),
            ("includeDefaults", Boolean("Include unset/default override properties."))));

        public static object ElementOverrides => Object(TargetViewProps(
            ("elementIds", StringArray("ElementIds to override.")),
            ("overrides", Overrides),
            ("dryRun", Boolean("Validate targets without changing the model.")),
            ("maxItems", Integer("Maximum element count allowed."))), "elementIds", "overrides");

        public static object ElementOverrideClear => Object(TargetViewProps(
            ("elementIds", StringArray("ElementIds whose element overrides should be cleared.")),
            ("dryRun", Boolean("Validate targets without changing the model.")),
            ("maxItems", Integer("Maximum element count allowed."))), "elementIds");

        public static object CategoryTarget => Object(TargetViewProps(
            ("categoryId", String("Category ElementId.")),
            ("category", String("BuiltInCategory name or suffix, e.g. Walls or OST_Walls."))));

        public static object CategoryOverride => Object(TargetViewProps(
            ("categoryId", String("Category ElementId.")),
            ("category", String("BuiltInCategory name or suffix, e.g. Walls or OST_Walls.")),
            ("overrides", Overrides)), "overrides");

        public static object CategoryVisibility => Object(TargetViewProps(
            ("categoryId", String("Category ElementId.")),
            ("category", String("BuiltInCategory name or suffix, e.g. Walls or OST_Walls.")),
            ("visible", Boolean("True to show the category, false to hide it."))), "visible");

        public static object FilterTarget => Object(TargetViewProps(
            ("filterId", String("ParameterFilterElement or selection filter ElementId."))), "filterId");

        public static object FilterOverride => Object(TargetViewProps(
            ("filterId", String("ParameterFilterElement or selection filter ElementId.")),
            ("overrides", Overrides),
            ("visible", Boolean("Optional filter visibility in the view.")),
            ("enabled", Boolean("Optional filter enabled state when supported by this Revit API."))), "filterId", "overrides");

        public static object FilterAdd => Object(TargetViewProps(
            ("filterId", String("ParameterFilterElement or selection filter ElementId.")),
            ("overrides", Overrides),
            ("visible", Boolean("Optional filter visibility in the view.")),
            ("enabled", Boolean("Optional filter enabled state when supported by this Revit API."))), "filterId");

        public static object ViewDetailGraphics => Object(TargetViewProps(
            ("detailLevel", String("coarse, medium, or fine.")),
            ("displayStyle", String("wireframe, hidden_line, shaded, consistent_colors, realistic, or flat_colors.")),
            ("partsVisibility", String("show_parts_only, show_original_only, or show_parts_and_original.")),
            ("discipline", String("architecture, structural, mechanical, electrical, coordination, or plumbing."))));

        public static object Preset => Object(Props(
            ("name", String("Preset name for this add-in session.")),
            ("overrides", Overrides)), "name", "overrides");

        public static object ApplyPreset => Object(TargetViewProps(
            ("targetType", String("element, category, or filter.")),
            ("targetIds", StringArray("ElementIds, category ids, or filter ids to receive the preset.")),
            ("presetName", String("Preset name created earlier.")),
            ("visible", Boolean("Optional visibility for category/filter targets.")),
            ("enabled", Boolean("Optional enabled state for filter targets.")),
            ("maxItems", Integer("Maximum target count allowed."))), "targetType", "targetIds", "presetName");
    }
}
