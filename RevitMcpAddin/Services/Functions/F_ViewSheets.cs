using Autodesk.Revit.DB;
using RevitMcpAddin.Models;
using RevitMcpAddin.Utils;
using View = Autodesk.Revit.DB.View;

namespace RevitMcpAddin.Services
{
    public sealed partial class RevitService
    {
        public Task<object> GetViewsAsync(CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var views = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => v.ViewType != ViewType.Internal)
                    .OrderBy(v => v.ViewType.ToString())
                    .ThenBy(v => v.Name)
                    .Select(BuildViewInfo)
                    .ToList();

                return Task.FromResult<object>(new
                {
                    success = true,
                    count = views.Count,
                    views,
                    message = $"Found {views.Count} view(s)."
                });
            }, ct);
        }

        public Task<object> CreateViewAsync(CreateViewRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                View view;
                using (var tx = new Transaction(doc, "RevitMCP: Create View"))
                {
                    tx.Start();

                    var viewFamily = ParseViewFamily(request.ViewType);
                    var viewFamilyTypeId = ResolveViewFamilyTypeId(doc, request.ViewFamilyTypeId, viewFamily);

                    view = viewFamily switch
                    {
                        ViewFamily.FloorPlan or ViewFamily.CeilingPlan or ViewFamily.StructuralPlan =>
                            ViewPlan.Create(doc, viewFamilyTypeId, ResolveRequiredElementId(doc, request.LevelId, "levelId")),
                        ViewFamily.ThreeDimensional =>
                            View3D.CreateIsometric(doc, viewFamilyTypeId),
                        ViewFamily.Drafting =>
                            ViewDrafting.Create(doc, viewFamilyTypeId),
                        _ => throw new ArgumentException($"Unsupported create_view viewType '{request.ViewType}'.")
                    };

                    ApplyViewBasicUpdates(view, request.Name, request.Scale, null);

                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                Logger.Info($"View created: id={view.Id}, name={view.Name}, type={view.ViewType}");

                return Task.FromResult<object>(new
                {
                    success = true,
                    view = BuildViewInfo(view),
                    elementId = view.Id.ToString(),
                    message = $"View '{view.Name}' created successfully (id={view.Id})."
                });
            }, ct);
        }

        public Task<object> DuplicateViewAsync(DuplicateViewRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var source = ResolveView(doc, request.ViewId);
                var option = ParseDuplicateOption(request.DuplicateOption);
                if (!source.CanViewBeDuplicated(option))
                    throw new InvalidOperationException(
                        $"View '{source.Name}' cannot be duplicated with option '{option}'.");

                View duplicated;
                using (var tx = new Transaction(doc, "RevitMCP: Duplicate View"))
                {
                    tx.Start();
                    var newId = source.Duplicate(option);
                    duplicated = doc.GetElement(newId) as View
                        ?? throw new InvalidOperationException("Duplicate returned an element that is not a view.");

                    if (!string.IsNullOrWhiteSpace(request.Name))
                        duplicated.Name = request.Name.Trim();

                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    sourceViewId = source.Id.ToString(),
                    view = BuildViewInfo(duplicated),
                    elementId = duplicated.Id.ToString(),
                    message = $"View '{source.Name}' duplicated as '{duplicated.Name}'."
                });
            }, ct);
        }

        public Task<object> UpdateViewAsync(UpdateViewRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var view = ResolveView(doc, request.ViewId);
                var changed = new List<string>();

                using (var tx = new Transaction(doc, "RevitMCP: Update View"))
                {
                    tx.Start();
                    ApplyViewBasicUpdates(view, request.Name, request.Scale, request.ViewTemplateId, changed);

                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    view = BuildViewInfo(view),
                    changed,
                    message = changed.Count == 0
                        ? $"View '{view.Name}' had no requested changes."
                        : $"View '{view.Name}' updated successfully."
                });
            }, ct);
        }

        public Task<object> GetSheetsAsync(CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var sheets = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => !s.IsTemplate)
                    .OrderBy(s => s.SheetNumber)
                    .Select(s => BuildSheetInfo(doc, s))
                    .ToList();

                return Task.FromResult<object>(new
                {
                    success = true,
                    count = sheets.Count,
                    sheets,
                    message = $"Found {sheets.Count} sheet(s)."
                });
            }, ct);
        }

        public Task<object> CreateSheetAsync(CreateSheetRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                ViewSheet sheet;
                using (var tx = new Transaction(doc, "RevitMCP: Create Sheet"))
                {
                    tx.Start();
                    var titleBlockTypeId = ResolveTitleBlockTypeId(doc, request.TitleBlockTypeId, request.TitleBlockTypeName);
                    sheet = ViewSheet.Create(doc, titleBlockTypeId);

                    if (!string.IsNullOrWhiteSpace(request.SheetNumber))
                        sheet.SheetNumber = request.SheetNumber.Trim();
                    if (!string.IsNullOrWhiteSpace(request.SheetName))
                        sheet.Name = request.SheetName.Trim();

                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    sheet = BuildSheetInfo(doc, sheet),
                    elementId = sheet.Id.ToString(),
                    message = $"Sheet '{sheet.SheetNumber}' created successfully (id={sheet.Id})."
                });
            }, ct);
        }

        public Task<object> UpdateSheetAsync(UpdateSheetRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var sheet = ResolveSheet(doc, request.SheetId);
                var changed = new List<string>();

                using (var tx = new Transaction(doc, "RevitMCP: Update Sheet"))
                {
                    tx.Start();
                    if (!string.IsNullOrWhiteSpace(request.SheetNumber) &&
                        !string.Equals(sheet.SheetNumber, request.SheetNumber.Trim(), StringComparison.Ordinal))
                    {
                        sheet.SheetNumber = request.SheetNumber.Trim();
                        changed.Add("sheetNumber");
                    }

                    if (!string.IsNullOrWhiteSpace(request.SheetName) &&
                        !string.Equals(sheet.Name, request.SheetName.Trim(), StringComparison.Ordinal))
                    {
                        sheet.Name = request.SheetName.Trim();
                        changed.Add("sheetName");
                    }

                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    sheet = BuildSheetInfo(doc, sheet),
                    changed,
                    message = changed.Count == 0
                        ? $"Sheet '{sheet.SheetNumber}' had no requested changes."
                        : $"Sheet '{sheet.SheetNumber}' updated successfully."
                });
            }, ct);
        }

        public Task<object> PlaceViewOnSheetAsync(
            PlaceViewOnSheetRequest request,
            bool titleViewOnly = false,
            CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var sheet = ResolveSheet(doc, request.SheetId);
                var view = ResolveView(doc, request.ViewId);

                if (titleViewOnly && view.ViewType is not (ViewType.DraftingView or ViewType.Legend))
                    throw new InvalidOperationException(
                        "place_title_view_on_sheet only accepts Drafting View or Legend views.");

                if (!Viewport.CanAddViewToSheet(doc, sheet.Id, view.Id))
                    throw new InvalidOperationException(
                        $"View '{view.Name}' cannot be added to sheet '{sheet.SheetNumber}'. " +
                        "It may already be placed, be a template, or have an unsupported view type.");

                Viewport viewport;
                using (var tx = new Transaction(doc, titleViewOnly
                    ? "RevitMCP: Place Title View On Sheet"
                    : "RevitMCP: Place View On Sheet"))
                {
                    tx.Start();
                    var point = new XYZ(ToInternalFeet(request.X, request.Unit), ToInternalFeet(request.Y, request.Unit), 0);
                    viewport = Viewport.Create(doc, sheet.Id, view.Id, point);

                    if (!string.IsNullOrWhiteSpace(request.ViewportTypeId))
                        viewport.ChangeTypeId(ResolveRequiredElementId(doc, request.ViewportTypeId, "viewportTypeId"));

                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    sheetId = sheet.Id.ToString(),
                    viewId = view.Id.ToString(),
                    viewportId = viewport.Id.ToString(),
                    message = $"Placed view '{view.Name}' on sheet '{sheet.SheetNumber}'."
                });
            }, ct);
        }

        public Task<object> RemoveViewFromSheetAsync(RemoveViewFromSheetRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var viewport = ResolveViewport(doc, request);
                var removedViewportId = viewport.Id.ToString();
                var removedViewId = viewport.ViewId.ToString();

                using (var tx = new Transaction(doc, "RevitMCP: Remove View From Sheet"))
                {
                    tx.Start();
                    doc.Delete(viewport.Id);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    viewportId = removedViewportId,
                    viewId = removedViewId,
                    message = $"Removed viewport {removedViewportId} from sheet."
                });
            }, ct);
        }

        private static ViewInfo BuildViewInfo(View view)
        {
            return new ViewInfo
            {
                Id = view.Id.ToString(),
                Name = view.Name,
                ViewType = view.ViewType.ToString(),
                IsTemplate = view.IsTemplate,
                Scale = SafeGetScale(view),
                LevelId = (view as ViewPlan)?.GenLevel?.Id.ToString(),
                ViewTemplateId = view.ViewTemplateId == ElementId.InvalidElementId ? null : view.ViewTemplateId.ToString(),
                CanBePrinted = view.CanBePrinted
            };
        }

        private static int SafeGetScale(View view)
        {
            try { return view.Scale; }
            catch { return 0; }
        }

        private static SheetInfo BuildSheetInfo(Document doc, ViewSheet sheet)
        {
            return new SheetInfo
            {
                Id = sheet.Id.ToString(),
                SheetNumber = sheet.SheetNumber,
                Name = sheet.Name,
                PlacedViews = sheet.GetAllViewports()
                    .Select(id => doc.GetElement(id) as Viewport)
                    .Where(vp => vp != null)
                    .Select(vp =>
                    {
                        var view = doc.GetElement(vp!.ViewId) as View;
                        return new PlacedViewInfo
                        {
                            ViewportId = vp.Id.ToString(),
                            ViewId = vp.ViewId.ToString(),
                            ViewName = view?.Name ?? string.Empty,
                            ViewType = view?.ViewType.ToString() ?? string.Empty
                        };
                    })
                    .ToList()
            };
        }

        private static View ResolveView(Document doc, string? rawId)
        {
            var view = doc.GetElement(ResolveRequiredElementId(doc, rawId, "viewId")) as View;
            return view ?? throw new InvalidOperationException($"View id '{rawId}' was not found.");
        }

        private static ViewSheet ResolveSheet(Document doc, string? rawId)
        {
            var sheet = doc.GetElement(ResolveRequiredElementId(doc, rawId, "sheetId")) as ViewSheet;
            return sheet ?? throw new InvalidOperationException($"Sheet id '{rawId}' was not found.");
        }

        private static Viewport ResolveViewport(Document doc, RemoveViewFromSheetRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.ViewportId))
            {
                var viewport = doc.GetElement(ResolveRequiredElementId(doc, request.ViewportId, "viewportId")) as Viewport;
                return viewport ?? throw new InvalidOperationException($"Viewport id '{request.ViewportId}' was not found.");
            }

            var sheet = ResolveSheet(doc, request.SheetId);
            var viewId = ResolveRequiredElementId(doc, request.ViewId, "viewId");
            var match = sheet.GetAllViewports()
                .Select(id => doc.GetElement(id) as Viewport)
                .FirstOrDefault(vp => vp != null && vp.ViewId == viewId);

            return match ?? throw new InvalidOperationException(
                $"View id '{request.ViewId}' is not placed on sheet '{sheet.SheetNumber}'.");
        }

        private static ElementId ResolveRequiredElementId(Document doc, string? rawId, string paramName)
        {
            if (string.IsNullOrWhiteSpace(rawId))
                throw new ArgumentException($"'{paramName}' is required.");
            if (!long.TryParse(rawId.Trim(), out var id))
                throw new ArgumentException($"Invalid {paramName} '{rawId}'. Expected an integer Revit ElementId.");
            var elementId = new ElementId(id);
            if (doc.GetElement(elementId) == null)
                throw new InvalidOperationException($"Element id '{rawId}' was not found.");
            return elementId;
        }

        private static ViewFamily ParseViewFamily(string? value)
        {
            return value?.Trim().ToLowerInvariant() switch
            {
                "floorplan" or "floor_plan" or "floor" => ViewFamily.FloorPlan,
                "ceilingplan" or "ceiling_plan" or "ceiling" => ViewFamily.CeilingPlan,
                "structuralplan" or "structural_plan" or "structural" => ViewFamily.StructuralPlan,
                "3d" or "three_d" or "threed" or "threedimensional" => ViewFamily.ThreeDimensional,
                "drafting" or "draftingview" or "drafting_view" => ViewFamily.Drafting,
                _ => throw new ArgumentException(
                    $"Unsupported viewType '{value}'. Use floorPlan, ceilingPlan, structuralPlan, threeD, or drafting.")
            };
        }

        private static ElementId ResolveViewFamilyTypeId(Document doc, string? rawId, ViewFamily family)
        {
            if (!string.IsNullOrWhiteSpace(rawId))
                return ResolveRequiredElementId(doc, rawId, "viewFamilyTypeId");

            var type = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == family);

            return type?.Id ?? throw new InvalidOperationException($"No ViewFamilyType found for {family}.");
        }

        private static ViewDuplicateOption ParseDuplicateOption(string? value)
        {
            return value?.Trim().ToLowerInvariant() switch
            {
                "withdetailing" or "with_detailing" => ViewDuplicateOption.WithDetailing,
                "asdependent" or "as_dependent" or "dependent" => ViewDuplicateOption.AsDependent,
                _ => ViewDuplicateOption.Duplicate
            };
        }

        private static void ApplyViewBasicUpdates(
            View view,
            string? name,
            int? scale,
            string? viewTemplateId,
            List<string>? changed = null)
        {
            if (!string.IsNullOrWhiteSpace(name) &&
                !string.Equals(view.Name, name.Trim(), StringComparison.Ordinal))
            {
                view.Name = name.Trim();
                changed?.Add("name");
            }

            if (scale.HasValue && scale.Value > 0 && SafeGetScale(view) != scale.Value)
            {
                view.Scale = scale.Value;
                changed?.Add("scale");
            }

            if (!string.IsNullOrWhiteSpace(viewTemplateId))
            {
                if (string.Equals(viewTemplateId.Trim(), "none", StringComparison.OrdinalIgnoreCase) ||
                    viewTemplateId.Trim() == "-1")
                {
                    view.ViewTemplateId = ElementId.InvalidElementId;
                    changed?.Add("viewTemplateId");
                }
                else
                {
                    var template = view.Document.GetElement(new ElementId(long.Parse(viewTemplateId.Trim()))) as View;
                    if (template == null || !template.IsTemplate)
                        throw new InvalidOperationException($"View template id '{viewTemplateId}' was not found.");
                    view.ViewTemplateId = template.Id;
                    changed?.Add("viewTemplateId");
                }
            }
        }

        private static ElementId ResolveTitleBlockTypeId(Document doc, string? rawId, string? typeName)
        {
            if (!string.IsNullOrWhiteSpace(rawId))
                return ResolveRequiredElementId(doc, rawId, "titleBlockTypeId");

            var symbols = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .ToList();

            if (!string.IsNullOrWhiteSpace(typeName))
            {
                var match = symbols.FirstOrDefault(s =>
                    string.Equals(s.Name, typeName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals($"{s.FamilyName}: {s.Name}", typeName.Trim(), StringComparison.OrdinalIgnoreCase));

                if (match == null)
                    throw new InvalidOperationException($"Title block type '{typeName}' was not found.");

                return match.Id;
            }

            return symbols.FirstOrDefault()?.Id ?? ElementId.InvalidElementId;
        }
    }
}
