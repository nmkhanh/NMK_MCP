using Autodesk.Revit.DB;
using RevitMcpAddin.Models;
using RevitView = Autodesk.Revit.DB.View;

namespace RevitMcpAddin.Services
{
    public sealed partial class RevitService
    {
        private const int DefaultOverrideBatchLimit = 100;
        private const int HardOverrideBatchLimit = 500;
        private static readonly Dictionary<string, GraphicOverrideRequest> ViewOverridePresets =
            new(StringComparer.OrdinalIgnoreCase);

        public Task<object> GetViewOverridesAsync(GetViewOverridesRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var uiDoc = uiApp.ActiveUIDocument
                    ?? throw new InvalidOperationException("No active document.");
                var doc = uiDoc.Document;
                var view = ResolveTargetView(doc, uiDoc.ActiveView, request.ViewId);

                var elementRows = request.ElementIds
                    .Select(raw => ResolveElement(doc, raw, "elementIds"))
                    .Select(element => new
                    {
                        elementId = element.Id.ToString(),
                        name = element.Name,
                        category = element.Category?.Name,
                        hidden = SafeViewCall(() => element.IsHidden(view), false),
                        overrides = BuildOverrideInfo(view.GetElementOverrides(element.Id), request.IncludeDefaults)
                    })
                    .ToList();

                var categoryIds = ResolveCategoryIds(doc, request.CategoryIds, request.Categories).ToList();
                var categoryRows = categoryIds
                    .Select(id =>
                    {
                        var category = Category.GetCategory(doc, id);
                        return new
                        {
                            categoryId = id.ToString(),
                            name = category?.Name,
                            hidden = SafeViewCall(() => view.GetCategoryHidden(id), false),
                            overrides = BuildOverrideInfo(view.GetCategoryOverrides(id), request.IncludeDefaults)
                        };
                    })
                    .ToList();

                var viewFilterIds = view.GetFilters().ToList();
                var requestedFilterIds = request.FilterIds.Count > 0
                    ? request.FilterIds.Select(raw => ResolveRequiredElementId(doc, raw, "filterIds")).ToList()
                    : viewFilterIds;
                var filterRows = requestedFilterIds
                    .Select(id =>
                    {
                        var filter = doc.GetElement(id);
                        var inView = viewFilterIds.Contains(id);
                        return new
                        {
                            filterId = id.ToString(),
                            name = filter?.Name,
                            inView,
                            visible = inView ? SafeViewCall(() => view.GetFilterVisibility(id), true) : (bool?)null,
                            enabled = inView ? GetFilterEnabledIfAvailable(view, id) : null,
                            overrides = inView ? BuildOverrideInfo(view.GetFilterOverrides(id), request.IncludeDefaults) : null
                        };
                    })
                    .ToList();

                return Task.FromResult<object>(new
                {
                    success = true,
                    view = BuildViewOverrideViewInfo(view),
                    elementOverrides = elementRows,
                    categoryOverrides = categoryRows,
                    filterOverrides = filterRows,
                    message = $"Read override data for view '{view.Name}'."
                });
            }, ct);
        }

        public Task<object> SetElementOverridesInViewAsync(ElementOverridesInViewRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var uiDoc = uiApp.ActiveUIDocument
                    ?? throw new InvalidOperationException("No active document.");
                var doc = uiDoc.Document;
                var view = ResolveEditableTargetView(doc, uiDoc.ActiveView, request.ViewId);
                var ids = ResolveElementIds(doc, request.ElementIds, request.MaxItems);
                var overrides = BuildOverrideGraphicSettings(doc, request.Overrides);

                if (!request.DryRun)
                {
                    using var tx = new Transaction(doc, "RevitMCP: Set Element Overrides In View");
                    tx.Start();
                    foreach (var id in ids)
                        view.SetElementOverrides(id, overrides);
                    CommitOrThrow(tx);
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    dryRun = request.DryRun,
                    viewId = view.Id.ToString(),
                    elementIds = ids.Select(id => id.ToString()).ToList(),
                    changedCount = request.DryRun ? 0 : ids.Count,
                    message = request.DryRun
                        ? $"Dry run validated {ids.Count} element override(s)."
                        : $"Set element overrides for {ids.Count} element(s) in view '{view.Name}'."
                });
            }, ct);
        }

        public Task<object> ClearElementOverridesInViewAsync(ViewElementIdsRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var uiDoc = uiApp.ActiveUIDocument
                    ?? throw new InvalidOperationException("No active document.");
                var doc = uiDoc.Document;
                var view = ResolveEditableTargetView(doc, uiDoc.ActiveView, request.ViewId);
                var ids = ResolveElementIds(doc, request.ElementIds, request.MaxItems);

                if (!request.DryRun)
                {
                    using var tx = new Transaction(doc, "RevitMCP: Clear Element Overrides In View");
                    tx.Start();
                    foreach (var id in ids)
                        view.SetElementOverrides(id, new OverrideGraphicSettings());
                    CommitOrThrow(tx);
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    dryRun = request.DryRun,
                    viewId = view.Id.ToString(),
                    elementIds = ids.Select(id => id.ToString()).ToList(),
                    changedCount = request.DryRun ? 0 : ids.Count,
                    message = request.DryRun
                        ? $"Dry run validated {ids.Count} element override reset(s)."
                        : $"Cleared element overrides for {ids.Count} element(s) in view '{view.Name}'."
                });
            }, ct);
        }

        public Task<object> SetCategoryOverridesInViewAsync(CategoryOverrideInViewRequest request, CancellationToken ct = default)
            => SetCategoryOverrideStateAsync(request, clear: false, ct);

        public Task<object> ClearCategoryOverridesInViewAsync(CategoryOverrideInViewRequest request, CancellationToken ct = default)
            => SetCategoryOverrideStateAsync(request, clear: true, ct);

        public Task<object> SetCategoryVisibilityInViewAsync(CategoryVisibilityInViewRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var uiDoc = uiApp.ActiveUIDocument
                    ?? throw new InvalidOperationException("No active document.");
                var doc = uiDoc.Document;
                var view = ResolveEditableTargetView(doc, uiDoc.ActiveView, request.ViewId);
                var categoryId = ResolveCategoryId(doc, request.CategoryId, request.Category);

                using var tx = new Transaction(doc, "RevitMCP: Set Category Visibility In View");
                tx.Start();
                view.SetCategoryHidden(categoryId, !request.Visible);
                CommitOrThrow(tx);

                return Task.FromResult<object>(new
                {
                    success = true,
                    viewId = view.Id.ToString(),
                    categoryId = categoryId.ToString(),
                    visible = request.Visible,
                    message = $"Set category {categoryId} visibility to {request.Visible} in view '{view.Name}'."
                });
            }, ct);
        }

        public Task<object> SetFilterOverridesInViewAsync(FilterOverrideInViewRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var uiDoc = uiApp.ActiveUIDocument
                    ?? throw new InvalidOperationException("No active document.");
                var doc = uiDoc.Document;
                var view = ResolveEditableTargetView(doc, uiDoc.ActiveView, request.ViewId);
                var filterId = ResolveRequiredElementId(doc, request.FilterId, "filterId");
                var overrides = BuildOverrideGraphicSettings(doc, request.Overrides);

                using var tx = new Transaction(doc, "RevitMCP: Set Filter Overrides In View");
                tx.Start();
                EnsureFilterInView(view, filterId);
                view.SetFilterOverrides(filterId, overrides);
                if (request.Visible.HasValue)
                    view.SetFilterVisibility(filterId, request.Visible.Value);
                if (request.Enabled.HasValue)
                    SetFilterEnabledIfAvailable(view, filterId, request.Enabled.Value);
                CommitOrThrow(tx);

                return Task.FromResult<object>(new
                {
                    success = true,
                    viewId = view.Id.ToString(),
                    filterId = filterId.ToString(),
                    visible = SafeViewCall(() => view.GetFilterVisibility(filterId), true),
                    enabled = GetFilterEnabledIfAvailable(view, filterId),
                    message = $"Set filter overrides for filter {filterId} in view '{view.Name}'."
                });
            }, ct);
        }

        public Task<object> ClearFilterOverridesInViewAsync(FilterOverrideInViewRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var uiDoc = uiApp.ActiveUIDocument
                    ?? throw new InvalidOperationException("No active document.");
                var doc = uiDoc.Document;
                var view = ResolveEditableTargetView(doc, uiDoc.ActiveView, request.ViewId);
                var filterId = ResolveRequiredElementId(doc, request.FilterId, "filterId");

                using var tx = new Transaction(doc, "RevitMCP: Clear Filter Overrides In View");
                tx.Start();
                EnsureFilterInView(view, filterId);
                view.SetFilterOverrides(filterId, new OverrideGraphicSettings());
                CommitOrThrow(tx);

                return Task.FromResult<object>(new
                {
                    success = true,
                    viewId = view.Id.ToString(),
                    filterId = filterId.ToString(),
                    message = $"Cleared filter overrides for filter {filterId} in view '{view.Name}'."
                });
            }, ct);
        }

        public Task<object> AddFilterToViewAsync(FilterInViewRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var uiDoc = uiApp.ActiveUIDocument
                    ?? throw new InvalidOperationException("No active document.");
                var doc = uiDoc.Document;
                var view = ResolveEditableTargetView(doc, uiDoc.ActiveView, request.ViewId);
                var filterId = ResolveRequiredElementId(doc, request.FilterId, "filterId");

                using var tx = new Transaction(doc, "RevitMCP: Add Filter To View");
                tx.Start();
                var added = EnsureFilterInView(view, filterId);
                if (request.Overrides != null)
                    view.SetFilterOverrides(filterId, BuildOverrideGraphicSettings(doc, request.Overrides));
                if (request.Visible.HasValue)
                    view.SetFilterVisibility(filterId, request.Visible.Value);
                if (request.Enabled.HasValue)
                    SetFilterEnabledIfAvailable(view, filterId, request.Enabled.Value);
                CommitOrThrow(tx);

                return Task.FromResult<object>(new
                {
                    success = true,
                    added,
                    viewId = view.Id.ToString(),
                    filterId = filterId.ToString(),
                    visible = SafeViewCall(() => view.GetFilterVisibility(filterId), true),
                    enabled = GetFilterEnabledIfAvailable(view, filterId),
                    message = added ? $"Added filter {filterId} to view '{view.Name}'." : $"Filter {filterId} was already in view '{view.Name}'."
                });
            }, ct);
        }

        public Task<object> RemoveFilterFromViewAsync(FilterInViewRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var uiDoc = uiApp.ActiveUIDocument
                    ?? throw new InvalidOperationException("No active document.");
                var doc = uiDoc.Document;
                var view = ResolveEditableTargetView(doc, uiDoc.ActiveView, request.ViewId);
                var filterId = ResolveRequiredElementId(doc, request.FilterId, "filterId");
                var removed = false;

                using var tx = new Transaction(doc, "RevitMCP: Remove Filter From View");
                tx.Start();
                if (view.GetFilters().Contains(filterId))
                {
                    view.RemoveFilter(filterId);
                    removed = true;
                }
                CommitOrThrow(tx);

                return Task.FromResult<object>(new
                {
                    success = true,
                    removed,
                    viewId = view.Id.ToString(),
                    filterId = filterId.ToString(),
                    message = removed ? $"Removed filter {filterId} from view '{view.Name}'." : $"Filter {filterId} was not in view '{view.Name}'."
                });
            }, ct);
        }

        public Task<object> SetViewDetailGraphicsAsync(ViewDetailGraphicsRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var uiDoc = uiApp.ActiveUIDocument
                    ?? throw new InvalidOperationException("No active document.");
                var doc = uiDoc.Document;
                var view = ResolveEditableTargetView(doc, uiDoc.ActiveView, request.ViewId);
                var changed = new List<string>();

                using var tx = new Transaction(doc, "RevitMCP: Set View Detail Graphics");
                tx.Start();
                if (!string.IsNullOrWhiteSpace(request.DetailLevel))
                {
                    view.DetailLevel = ParseViewDetailLevel(request.DetailLevel);
                    changed.Add("detailLevel");
                }
                if (!string.IsNullOrWhiteSpace(request.DisplayStyle))
                {
                    view.DisplayStyle = ParseEnumFlexible<DisplayStyle>(request.DisplayStyle, "displayStyle");
                    changed.Add("displayStyle");
                }
                if (!string.IsNullOrWhiteSpace(request.PartsVisibility))
                {
                    view.PartsVisibility = ParseEnumFlexible<PartsVisibility>(request.PartsVisibility, "partsVisibility");
                    changed.Add("partsVisibility");
                }
                if (!string.IsNullOrWhiteSpace(request.Discipline))
                {
                    SetViewDiscipline(view, request.Discipline);
                    changed.Add("discipline");
                }
                CommitOrThrow(tx);

                return Task.FromResult<object>(new
                {
                    success = true,
                    view = BuildViewOverrideViewInfo(view),
                    changed,
                    message = changed.Count == 0
                        ? $"View '{view.Name}' had no requested graphics changes."
                        : $"Updated view graphics for '{view.Name}'."
                });
            }, ct);
        }

        public Task<object> CreateViewGraphicsOverridePresetAsync(ViewOverridePresetRequest request, CancellationToken ct = default)
        {
            return Task.FromResult<object>(CreateViewOverridePresetCore(request));
        }

        public Task<object> ApplyViewGraphicsOverridePresetAsync(ApplyViewOverridePresetRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var uiDoc = uiApp.ActiveUIDocument
                    ?? throw new InvalidOperationException("No active document.");
                var doc = uiDoc.Document;
                var view = ResolveEditableTargetView(doc, uiDoc.ActiveView, request.ViewId);
                if (string.IsNullOrWhiteSpace(request.PresetName))
                    throw new ArgumentException("'presetName' is required.");
                if (!ViewOverridePresets.TryGetValue(request.PresetName.Trim(), out var preset))
                    throw new InvalidOperationException($"Override preset '{request.PresetName}' was not found.");

                var targetType = (request.TargetType ?? "element").Trim().ToLowerInvariant();
                var overrides = BuildOverrideGraphicSettings(doc, preset);
                var changedIds = new List<string>();

                using var tx = new Transaction(doc, "RevitMCP: Apply View Override Preset");
                tx.Start();
                switch (targetType)
                {
                    case "element":
                    case "elements":
                        foreach (var id in ResolveElementIds(doc, request.TargetIds, request.MaxItems))
                        {
                            view.SetElementOverrides(id, overrides);
                            changedIds.Add(id.ToString());
                        }
                        break;

                    case "category":
                    case "categories":
                        foreach (var id in ResolveCategoryIds(doc, request.TargetIds, Array.Empty<string>()))
                        {
                            view.SetCategoryOverrides(id, overrides);
                            changedIds.Add(id.ToString());
                        }
                        break;

                    case "filter":
                    case "filters":
                        foreach (var rawId in request.TargetIds)
                        {
                            var id = ResolveRequiredElementId(doc, rawId, "targetIds");
                            EnsureFilterInView(view, id);
                            view.SetFilterOverrides(id, overrides);
                            if (request.Visible.HasValue)
                                view.SetFilterVisibility(id, request.Visible.Value);
                            if (request.Enabled.HasValue)
                                SetFilterEnabledIfAvailable(view, id, request.Enabled.Value);
                            changedIds.Add(id.ToString());
                        }
                        break;

                    default:
                        throw new ArgumentException("'targetType' must be element, category, or filter.");
                }
                CommitOrThrow(tx);

                return Task.FromResult<object>(new
                {
                    success = true,
                    viewId = view.Id.ToString(),
                    presetName = request.PresetName.Trim(),
                    targetType,
                    changedCount = changedIds.Count,
                    changedIds,
                    message = $"Applied preset '{request.PresetName.Trim()}' to {changedIds.Count} {targetType} target(s)."
                });
            }, ct);
        }

        private Task<object> SetCategoryOverrideStateAsync(
            CategoryOverrideInViewRequest request,
            bool clear,
            CancellationToken ct)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var uiDoc = uiApp.ActiveUIDocument
                    ?? throw new InvalidOperationException("No active document.");
                var doc = uiDoc.Document;
                var view = ResolveEditableTargetView(doc, uiDoc.ActiveView, request.ViewId);
                var categoryId = ResolveCategoryId(doc, request.CategoryId, request.Category);
                var overrides = clear
                    ? new OverrideGraphicSettings()
                    : BuildOverrideGraphicSettings(doc, request.Overrides);

                using var tx = new Transaction(doc, clear
                    ? "RevitMCP: Clear Category Overrides In View"
                    : "RevitMCP: Set Category Overrides In View");
                tx.Start();
                view.SetCategoryOverrides(categoryId, overrides);
                CommitOrThrow(tx);

                return Task.FromResult<object>(new
                {
                    success = true,
                    viewId = view.Id.ToString(),
                    categoryId = categoryId.ToString(),
                    message = clear
                        ? $"Cleared category overrides for {categoryId} in view '{view.Name}'."
                        : $"Set category overrides for {categoryId} in view '{view.Name}'."
                });
            }, ct);
        }

        private static object CreateViewOverridePresetCore(ViewOverridePresetRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("'name' is required.");
            ViewOverridePresets[request.Name.Trim()] = request.Overrides;

            return new
            {
                success = true,
                name = request.Name.Trim(),
                presetCount = ViewOverridePresets.Count,
                presets = ViewOverridePresets.Keys.OrderBy(k => k).ToList(),
                message = $"Override preset '{request.Name.Trim()}' saved for this add-in session."
            };
        }

        private static RevitView ResolveTargetView(Document doc, RevitView? activeView, string? rawId)
        {
            var view = string.IsNullOrWhiteSpace(rawId)
                ? activeView
                : ResolveView(doc, rawId);

            return view ?? throw new InvalidOperationException("No active view.");
        }

        private static RevitView ResolveEditableTargetView(Document doc, RevitView? activeView, string? rawId)
        {
            var view = ResolveTargetView(doc, activeView, rawId);
            if (view.IsTemplate)
                throw new InvalidOperationException("Cannot apply view overrides to a view template.");
            return view;
        }

        private static OverrideGraphicSettings BuildOverrideGraphicSettings(
            Document doc,
            GraphicOverrideRequest request)
        {
            var settings = new OverrideGraphicSettings();

            if (!string.IsNullOrWhiteSpace(request.ProjectionLineColor))
                settings.SetProjectionLineColor(ParseColor(request.ProjectionLineColor));
            if (!string.IsNullOrWhiteSpace(request.ProjectionLinePatternId))
                settings.SetProjectionLinePatternId(ResolveRequiredElementId(doc, request.ProjectionLinePatternId, "projectionLinePatternId"));
            if (request.ProjectionLineWeight.HasValue)
                settings.SetProjectionLineWeight(request.ProjectionLineWeight.Value);

            if (!string.IsNullOrWhiteSpace(request.CutLineColor))
                settings.SetCutLineColor(ParseColor(request.CutLineColor));
            if (!string.IsNullOrWhiteSpace(request.CutLinePatternId))
                settings.SetCutLinePatternId(ResolveRequiredElementId(doc, request.CutLinePatternId, "cutLinePatternId"));
            if (request.CutLineWeight.HasValue)
                settings.SetCutLineWeight(request.CutLineWeight.Value);

            if (!string.IsNullOrWhiteSpace(request.SurfaceForegroundPatternId))
                settings.SetSurfaceForegroundPatternId(ResolveRequiredElementId(doc, request.SurfaceForegroundPatternId, "surfaceForegroundPatternId"));
            if (!string.IsNullOrWhiteSpace(request.SurfaceForegroundPatternColor))
                settings.SetSurfaceForegroundPatternColor(ParseColor(request.SurfaceForegroundPatternColor));
            if (request.SurfaceForegroundPatternVisible.HasValue)
                settings.SetSurfaceForegroundPatternVisible(request.SurfaceForegroundPatternVisible.Value);

            if (!string.IsNullOrWhiteSpace(request.SurfaceBackgroundPatternId))
                settings.SetSurfaceBackgroundPatternId(ResolveRequiredElementId(doc, request.SurfaceBackgroundPatternId, "surfaceBackgroundPatternId"));
            if (!string.IsNullOrWhiteSpace(request.SurfaceBackgroundPatternColor))
                settings.SetSurfaceBackgroundPatternColor(ParseColor(request.SurfaceBackgroundPatternColor));
            if (request.SurfaceBackgroundPatternVisible.HasValue)
                settings.SetSurfaceBackgroundPatternVisible(request.SurfaceBackgroundPatternVisible.Value);

            if (!string.IsNullOrWhiteSpace(request.CutForegroundPatternId))
                settings.SetCutForegroundPatternId(ResolveRequiredElementId(doc, request.CutForegroundPatternId, "cutForegroundPatternId"));
            if (!string.IsNullOrWhiteSpace(request.CutForegroundPatternColor))
                settings.SetCutForegroundPatternColor(ParseColor(request.CutForegroundPatternColor));
            if (request.CutForegroundPatternVisible.HasValue)
                settings.SetCutForegroundPatternVisible(request.CutForegroundPatternVisible.Value);

            if (!string.IsNullOrWhiteSpace(request.CutBackgroundPatternId))
                settings.SetCutBackgroundPatternId(ResolveRequiredElementId(doc, request.CutBackgroundPatternId, "cutBackgroundPatternId"));
            if (!string.IsNullOrWhiteSpace(request.CutBackgroundPatternColor))
                settings.SetCutBackgroundPatternColor(ParseColor(request.CutBackgroundPatternColor));
            if (request.CutBackgroundPatternVisible.HasValue)
                settings.SetCutBackgroundPatternVisible(request.CutBackgroundPatternVisible.Value);

            if (request.Transparency.HasValue)
                settings.SetSurfaceTransparency(Math.Clamp(request.Transparency.Value, 0, 100));
            if (request.Halftone.HasValue)
                settings.SetHalftone(request.Halftone.Value);
            if (!string.IsNullOrWhiteSpace(request.DetailLevel))
                settings.SetDetailLevel(ParseViewDetailLevel(request.DetailLevel));

            return settings;
        }

        private static object BuildOverrideInfo(OverrideGraphicSettings settings, bool includeDefaults)
        {
            var data = new Dictionary<string, object?>();
            AddColor(data, "projectionLineColor", settings.ProjectionLineColor, includeDefaults);
            AddElementId(data, "projectionLinePatternId", settings.ProjectionLinePatternId, includeDefaults);
            AddInt(data, "projectionLineWeight", settings.ProjectionLineWeight, includeDefaults, OverrideGraphicSettings.InvalidPenNumber);
            AddColor(data, "cutLineColor", settings.CutLineColor, includeDefaults);
            AddElementId(data, "cutLinePatternId", settings.CutLinePatternId, includeDefaults);
            AddInt(data, "cutLineWeight", settings.CutLineWeight, includeDefaults, OverrideGraphicSettings.InvalidPenNumber);
            AddElementId(data, "surfaceForegroundPatternId", settings.SurfaceForegroundPatternId, includeDefaults);
            AddColor(data, "surfaceForegroundPatternColor", settings.SurfaceForegroundPatternColor, includeDefaults);
            AddBool(data, "surfaceForegroundPatternVisible", settings.IsSurfaceForegroundPatternVisible, includeDefaults);
            AddElementId(data, "surfaceBackgroundPatternId", settings.SurfaceBackgroundPatternId, includeDefaults);
            AddColor(data, "surfaceBackgroundPatternColor", settings.SurfaceBackgroundPatternColor, includeDefaults);
            AddBool(data, "surfaceBackgroundPatternVisible", settings.IsSurfaceBackgroundPatternVisible, includeDefaults);
            AddElementId(data, "cutForegroundPatternId", settings.CutForegroundPatternId, includeDefaults);
            AddColor(data, "cutForegroundPatternColor", settings.CutForegroundPatternColor, includeDefaults);
            AddBool(data, "cutForegroundPatternVisible", settings.IsCutForegroundPatternVisible, includeDefaults);
            AddElementId(data, "cutBackgroundPatternId", settings.CutBackgroundPatternId, includeDefaults);
            AddColor(data, "cutBackgroundPatternColor", settings.CutBackgroundPatternColor, includeDefaults);
            AddBool(data, "cutBackgroundPatternVisible", settings.IsCutBackgroundPatternVisible, includeDefaults);
            AddInt(data, "transparency", settings.Transparency, includeDefaults, -1);
            AddBool(data, "halftone", settings.Halftone, includeDefaults);
            if (includeDefaults || settings.DetailLevel != ViewDetailLevel.Undefined)
                data["detailLevel"] = settings.DetailLevel.ToString();
            return data;
        }

        private static object BuildViewOverrideViewInfo(RevitView view)
        {
            return new
            {
                id = view.Id.ToString(),
                name = view.Name,
                viewType = view.ViewType.ToString(),
                isTemplate = view.IsTemplate,
                viewTemplateId = view.ViewTemplateId == ElementId.InvalidElementId ? null : view.ViewTemplateId.ToString(),
                detailLevel = view.DetailLevel.ToString(),
                displayStyle = view.DisplayStyle.ToString(),
                partsVisibility = view.PartsVisibility.ToString(),
                filterCount = SafeViewCall(() => view.GetFilters().Count, 0)
            };
        }

        private static IEnumerable<ElementId> ResolveCategoryIds(
            Document doc,
            IReadOnlyList<string> categoryIds,
            IReadOnlyList<string> categories)
        {
            foreach (var rawId in categoryIds)
                yield return ResolveRequiredElementId(doc, rawId, "categoryIds");
            foreach (var category in categories)
                yield return ResolveCategoryId(doc, null, category);
        }

        private static ElementId ResolveCategoryId(Document doc, string? rawId, string? category)
        {
            if (!string.IsNullOrWhiteSpace(rawId))
                return ResolveRequiredElementId(doc, rawId, "categoryId");
            if (!string.IsNullOrWhiteSpace(category))
            {
                if (!TryResolveCategory(category, out var bic))
                    throw new ArgumentException($"Unknown or unsupported category: '{category}'.");
                return Category.GetCategory(doc, bic)?.Id
                    ?? throw new InvalidOperationException($"Category '{category}' was not found in this document.");
            }
            throw new ArgumentException("Either 'categoryId' or 'category' is required.");
        }

        private static bool EnsureFilterInView(RevitView view, ElementId filterId)
        {
            if (view.GetFilters().Contains(filterId))
                return false;
            view.AddFilter(filterId);
            return true;
        }

        private static bool? GetFilterEnabledIfAvailable(RevitView view, ElementId filterId)
        {
            var method = view.GetType().GetMethod("GetIsFilterEnabled", new[] { typeof(ElementId) });
            if (method == null)
                return null;
            return method.Invoke(view, new object[] { filterId }) as bool?;
        }

        private static void SetFilterEnabledIfAvailable(RevitView view, ElementId filterId, bool enabled)
        {
            var method = view.GetType().GetMethod("SetIsFilterEnabled", new[] { typeof(ElementId), typeof(bool) });
            method?.Invoke(view, new object[] { filterId, enabled });
        }

        private static void SetViewDiscipline(RevitView view, string value)
        {
            var discipline = ParseEnumFlexible<ViewDiscipline>(value, "discipline");
            var parameter = view.get_Parameter(BuiltInParameter.VIEW_DISCIPLINE)
                ?? throw new InvalidOperationException("This view does not expose VIEW_DISCIPLINE.");
            if (parameter.IsReadOnly)
                throw new InvalidOperationException("VIEW_DISCIPLINE is read-only for this view.");
            parameter.Set((int)discipline);
        }

        private static Autodesk.Revit.DB.Color ParseColor(string value)
        {
            var text = value.Trim().TrimStart('#');
            if (text.Length != 6)
                throw new ArgumentException($"Invalid color '{value}'. Use #RRGGBB.");
            return new Autodesk.Revit.DB.Color(
                Convert.ToByte(text.Substring(0, 2), 16),
                Convert.ToByte(text.Substring(2, 2), 16),
                Convert.ToByte(text.Substring(4, 2), 16));
        }

        private static string? ColorToHex(Autodesk.Revit.DB.Color color)
        {
            if (!color.IsValid)
                return null;
            return $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
        }

        private static void AddColor(Dictionary<string, object?> data, string key, Autodesk.Revit.DB.Color color, bool includeDefaults)
        {
            var value = ColorToHex(color);
            if (includeDefaults || value != null)
                data[key] = value;
        }

        private static void AddElementId(Dictionary<string, object?> data, string key, ElementId id, bool includeDefaults)
        {
            if (includeDefaults || id != ElementId.InvalidElementId)
                data[key] = id == ElementId.InvalidElementId ? null : id.ToString();
        }

        private static void AddInt(Dictionary<string, object?> data, string key, int value, bool includeDefaults, int unsetValue)
        {
            if (includeDefaults || value != unsetValue)
                data[key] = value == unsetValue ? null : value;
        }

        private static void AddBool(Dictionary<string, object?> data, string key, bool value, bool includeDefaults)
        {
            if (includeDefaults || value)
                data[key] = value;
        }

        private static TEnum ParseEnumFlexible<TEnum>(string value, string paramName)
            where TEnum : struct
        {
            var normalized = value.Trim().Replace("_", string.Empty).Replace(" ", string.Empty);
            foreach (var name in Enum.GetNames(typeof(TEnum)))
            {
                var comparable = name.Replace("_", string.Empty).Replace(" ", string.Empty);
                if (string.Equals(comparable, normalized, StringComparison.OrdinalIgnoreCase))
                    return Enum.Parse<TEnum>(name);
            }
            throw new ArgumentException($"Unsupported {paramName} '{value}'.");
        }

        private static T SafeViewCall<T>(Func<T> getter, T fallback)
        {
            try { return getter(); }
            catch { return fallback; }
        }

        private static void CommitOrThrow(Transaction tx)
        {
            var status = tx.Commit();
            if (status != TransactionStatus.Committed)
                throw new InvalidOperationException($"Transaction did not commit (status={status}).");
        }
    }
}
