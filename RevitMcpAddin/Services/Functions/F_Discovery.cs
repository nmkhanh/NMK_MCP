using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using RevitMcpAddin.Models;

namespace RevitMcpAddin.Services
{
    public sealed partial class RevitService
    {
        public Task<object> GetProjectInfoAsync(CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var uiDoc = uiApp.ActiveUIDocument
                    ?? throw new InvalidOperationException("No active document.");
                var doc = uiDoc.Document;
                var projectInfo = doc.ProjectInformation;

                return Task.FromResult<object>(new
                {
                    success = true,
                    title = doc.Title,
                    pathName = doc.PathName,
                    isFamilyDocument = doc.IsFamilyDocument,
                    isWorkshared = doc.IsWorkshared,
                    projectInfoId = projectInfo?.Id.ToString(),
                    projectName = projectInfo?.Name,
                    projectNumber = projectInfo?.Number,
                    clientName = projectInfo?.ClientName,
                    organizationName = projectInfo?.OrganizationName,
                    activeView = uiDoc.ActiveView == null ? null : new
                    {
                        id = uiDoc.ActiveView.Id.ToString(),
                        name = uiDoc.ActiveView.Name,
                        viewType = uiDoc.ActiveView.ViewType.ToString()
                    },
                    message = $"Read project info for '{doc.Title}'."
                });
            }, ct);
        }

        public Task<object> ListCategoriesAsync(ListCategoriesRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var categoryType = ParseCategoryType(request.CategoryType);
                var categories = doc.Settings.Categories
                    .Cast<Category>()
                    .Where(c => categoryType == null || c.CategoryType == categoryType.Value)
                    .OrderBy(c => c.CategoryType.ToString())
                    .ThenBy(c => c.Name)
                    .Select(c => new CategoryInfo
                    {
                        Id = c.Id.ToString(),
                        Name = c.Name,
                        CategoryType = c.CategoryType.ToString(),
                        AllowsBoundParameters = c.AllowsBoundParameters
                    })
                    .ToList();

                return Task.FromResult<object>(new
                {
                    success = true,
                    count = categories.Count,
                    categories,
                    message = $"Found {categories.Count} categor(ies)."
                });
            }, ct);
        }

        public Task<object> ListElementTypesAsync(ListElementTypesRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var collector = new FilteredElementCollector(doc).WhereElementIsElementType();
                if (!string.IsNullOrWhiteSpace(request.Category))
                {
                    if (!TryResolveCategory(request.Category, out var bic))
                        throw new ArgumentException($"Unknown or unsupported category: '{request.Category}'.");
                    collector = collector.OfCategory(bic);
                }

                var maxItems = NormalizeDiscoveryLimit(request.MaxItems);
                var types = collector
                    .Cast<Element>()
                    .OfType<ElementType>()
                    .Where(t => string.IsNullOrWhiteSpace(request.FamilyName) ||
                                string.Equals(SafeFamilyName(t), request.FamilyName.Trim(), StringComparison.OrdinalIgnoreCase))
                    .OrderBy(t => t.Category?.Name ?? string.Empty)
                    .ThenBy(SafeFamilyName)
                    .ThenBy(t => t.Name)
                    .Take(maxItems)
                    .Select(BuildElementTypeSummary)
                    .ToList();

                return Task.FromResult<object>(new
                {
                    success = true,
                    count = types.Count,
                    maxItems,
                    types,
                    message = $"Found {types.Count} element type(s)."
                });
            }, ct);
        }

        public Task<object> ListFamilyTypesAsync(ListElementTypesRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol));
                if (!string.IsNullOrWhiteSpace(request.Category))
                {
                    if (!TryResolveCategory(request.Category, out var bic))
                        throw new ArgumentException($"Unknown or unsupported category: '{request.Category}'.");
                    collector = collector.OfCategory(bic);
                }

                var maxItems = NormalizeDiscoveryLimit(request.MaxItems);
                var symbols = collector
                    .Cast<FamilySymbol>()
                    .Where(s => string.IsNullOrWhiteSpace(request.FamilyName) ||
                                string.Equals(s.FamilyName, request.FamilyName.Trim(), StringComparison.OrdinalIgnoreCase))
                    .OrderBy(s => s.Category?.Name ?? string.Empty)
                    .ThenBy(s => s.FamilyName)
                    .ThenBy(s => s.Name)
                    .Take(maxItems)
                    .Select(BuildElementTypeSummary)
                    .ToList();

                return Task.FromResult<object>(new
                {
                    success = true,
                    count = symbols.Count,
                    maxItems,
                    familyTypes = symbols,
                    message = $"Found {symbols.Count} family type(s)."
                });
            }, ct);
        }

        public Task<object> GetSelectedElementsAsync(CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var uiDoc = uiApp.ActiveUIDocument
                    ?? throw new InvalidOperationException("No active document.");
                var doc = uiDoc.Document;

                ICollection<ElementId> selectedIds = uiDoc.Selection.GetElementIds();
                var elements = selectedIds
                    .Select(id => doc.GetElement(id))
                    .Where(e => e != null)
                    .Select(e => BuildElementDetailInfo(e!, includeParameters: false, includeTypeParameters: false))
                    .ToList();

                return Task.FromResult<object>(new
                {
                    success = true,
                    count = elements.Count,
                    elements,
                    message = $"Found {elements.Count} selected element(s)."
                });
            }, ct);
        }

        public Task<object> FindElementsByParameterAsync(
            FindElementsByParameterRequest request,
            CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                if (string.IsNullOrWhiteSpace(request.ParameterName))
                    throw new ArgumentException("'parameterName' is required.");

                var maxItems = NormalizeDiscoveryLimit(request.MaxItems, hardMax: 2000);
                var collector = CreateScopedElementCollector(
                        doc,
                        uiApp.ActiveUIDocument?.ActiveView,
                        request.UseActiveView,
                        request.ViewId)
                    .WhereElementIsNotElementType();
                if (!string.IsNullOrWhiteSpace(request.Category))
                {
                    if (!TryResolveCategory(request.Category, out var bic))
                        throw new ArgumentException($"Unknown or unsupported category: '{request.Category}'.");
                    collector = collector.OfCategory(bic);
                }

                var elements = new List<ElementInfo>();
                foreach (var element in collector.Cast<Element>())
                {
                    if (!TryFindParameterDisplayValue(element, request.ParameterName, out var actual))
                        continue;
                    if (!MatchesParameterComparison(actual, request.Value, request.Comparison))
                        continue;

                    elements.Add(BuildElementInfo(element, request.IncludeParameters));
                    if (elements.Count >= maxItems)
                        break;
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    count = elements.Count,
                    maxItems,
                    useActiveView = request.UseActiveView,
                    viewId = request.ViewId,
                    parameterName = request.ParameterName,
                    value = request.Value,
                    comparison = request.Comparison,
                    elements,
                    message = $"Found {elements.Count} element(s) matching parameter '{request.ParameterName}'."
                });
            }, ct, timeoutMs: 120_000);
        }

        private static CategoryType? ParseCategoryType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim().ToLowerInvariant() switch
            {
                "model" => CategoryType.Model,
                "annotation" => CategoryType.Annotation,
                "analytical" => CategoryType.AnalyticalModel,
                "internal" => CategoryType.Internal,
                _ => throw new ArgumentException(
                    $"Unsupported categoryType '{value}'. Use model, annotation, analytical, or internal.")
            };
        }

        private static int NormalizeDiscoveryLimit(int requested, int hardMax = 1000)
        {
            var value = requested <= 0 ? 500 : requested;
            return Math.Min(value, hardMax);
        }

        private static ElementTypeSummary BuildElementTypeSummary(ElementType type)
        {
            return new ElementTypeSummary
            {
                Id = type.Id.ToString(),
                Name = type.Name ?? string.Empty,
                FamilyName = SafeFamilyName(type),
                Category = type.Category?.Name ?? string.Empty,
                ClassName = type.GetType().Name,
                IsActive = type is FamilySymbol symbol ? symbol.IsActive : null
            };
        }

        private static string SafeFamilyName(ElementType type)
        {
            try { return type.FamilyName ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static bool TryFindParameterDisplayValue(
            Element element,
            string parameterName,
            out string value)
        {
            value = string.Empty;
            if (TryGetParameterDisplayValue(element, parameterName, out value))
                return true;

            var typeElement = GetTypeElement(element);
            return typeElement != null && TryGetParameterDisplayValue(typeElement, parameterName, out value);
        }

        private static bool TryGetParameterDisplayValue(
            Element element,
            string parameterName,
            out string value)
        {
            value = string.Empty;
            var parameter = element.LookupParameter(parameterName.Trim());
            if (parameter == null)
                return false;

            value = GetParameterDisplayValue(parameter);
            return true;
        }

        private static bool MatchesParameterComparison(string actual, string? expected, string? comparison)
        {
            if (expected == null)
                return true;

            var mode = comparison?.Trim().ToLowerInvariant();
            return mode switch
            {
                "contains" => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
                "startswith" or "starts_with" => actual.StartsWith(expected, StringComparison.OrdinalIgnoreCase),
                "endswith" or "ends_with" => actual.EndsWith(expected, StringComparison.OrdinalIgnoreCase),
                "not_equals" or "notequals" => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                _ => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
            };
        }
    }
}
