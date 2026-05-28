using Autodesk.Revit.DB;
using RevitMcpAddin.Models;

namespace RevitMcpAddin.Services
{
    public sealed partial class RevitService
    {
        public Task<object> GetViewSheetSetsAsync(CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var sets = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheetSet))
                    .Cast<ViewSheetSet>()
                    .OrderBy(s => s.Name)
                    .Select(BuildViewSheetSetInfo)
                    .ToList();

                return Task.FromResult<object>(new
                {
                    success = true,
                    count = sets.Count,
                    viewSheetSets = sets,
                    message = $"Found {sets.Count} view/sheet set(s)."
                });
            }, ct);
        }

        public Task<object> CreateViewSheetSetAsync(CreateViewSheetSetRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                if (string.IsNullOrWhiteSpace(request.Name))
                    throw new ArgumentException("'name' is required.");

                var existing = FindViewSheetSet(doc, request.Name);
                if (existing != null && !request.ReplaceExisting)
                    throw new InvalidOperationException(
                        $"ViewSheetSet '{request.Name}' already exists. Set replaceExisting=true to replace it.");

                var viewSet = BuildViewSet(doc, request.ViewIds, request.SheetIds);
                if (viewSet.Size == 0)
                    throw new ArgumentException("Provide at least one viewId or sheetId.");

                using (var tx = new Transaction(doc, "RevitMCP: Create ViewSheetSet"))
                {
                    tx.Start();
                    var setting = doc.PrintManager.ViewSheetSetting;
                    if (existing != null)
                    {
                        setting.CurrentViewSheetSet = existing;
                        setting.Delete();
                    }

                    setting.CurrentViewSheetSet.Views = viewSet;
                    setting.SaveAs(request.Name.Trim());

                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                var created = FindViewSheetSet(doc, request.Name)
                    ?? throw new InvalidOperationException($"ViewSheetSet '{request.Name}' was not found after save.");

                return Task.FromResult<object>(new
                {
                    success = true,
                    viewSheetSet = BuildViewSheetSetInfo(created),
                    elementId = created.Id.ToString(),
                    message = $"ViewSheetSet '{created.Name}' saved with {created.Views.Size} view(s)."
                });
            }, ct);
        }

        public Task<object> AddViewsToViewSheetSetAsync(ModifyViewSheetSetRequest request, CancellationToken ct = default)
            => ModifyViewSheetSetAsync(request, remove: false, ct);

        public Task<object> AddSheetsToViewSheetSetAsync(ModifyViewSheetSetRequest request, CancellationToken ct = default)
            => ModifyViewSheetSetAsync(request, remove: false, ct);

        public Task<object> RemoveFromViewSheetSetAsync(ModifyViewSheetSetRequest request, CancellationToken ct = default)
            => ModifyViewSheetSetAsync(request, remove: true, ct);

        public Task<object> DeleteViewSheetSetAsync(DeleteViewSheetSetRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                if (string.IsNullOrWhiteSpace(request.Name))
                    throw new ArgumentException("'name' is required.");

                var existing = FindViewSheetSet(doc, request.Name)
                    ?? throw new InvalidOperationException($"ViewSheetSet '{request.Name}' was not found.");

                using (var tx = new Transaction(doc, "RevitMCP: Delete ViewSheetSet"))
                {
                    tx.Start();
                    var setting = doc.PrintManager.ViewSheetSetting;
                    setting.CurrentViewSheetSet = existing;
                    setting.Delete();

                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    name = request.Name,
                    message = $"ViewSheetSet '{request.Name}' deleted."
                });
            }, ct);
        }

        private Task<object> ModifyViewSheetSetAsync(ModifyViewSheetSetRequest request, bool remove, CancellationToken ct)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                if (string.IsNullOrWhiteSpace(request.Name))
                    throw new ArgumentException("'name' is required.");

                var existing = FindViewSheetSet(doc, request.Name)
                    ?? throw new InvalidOperationException($"ViewSheetSet '{request.Name}' was not found.");

                var currentViews = existing.Views.Cast<View>().ToDictionary(v => v.Id.Value);
                var requestedViews = ResolveViewsForSet(doc, request.ViewIds, request.SheetIds);

                if (requestedViews.Count == 0)
                    throw new ArgumentException("Provide at least one viewId or sheetId.");

                foreach (var view in requestedViews)
                {
                    if (remove) currentViews.Remove(view.Id.Value);
                    else currentViews[view.Id.Value] = view;
                }

                var nextSet = new ViewSet();
                foreach (var view in currentViews.Values.OrderBy(v => v.Name))
                    nextSet.Insert(view);

                using (var tx = new Transaction(doc, remove
                    ? "RevitMCP: Remove From ViewSheetSet"
                    : "RevitMCP: Add To ViewSheetSet"))
                {
                    tx.Start();
                    var setting = doc.PrintManager.ViewSheetSetting;
                    setting.CurrentViewSheetSet = existing;
                    setting.CurrentViewSheetSet.Views = nextSet;
                    setting.Save();

                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                var updated = FindViewSheetSet(doc, request.Name)
                    ?? throw new InvalidOperationException($"ViewSheetSet '{request.Name}' was not found after update.");

                return Task.FromResult<object>(new
                {
                    success = true,
                    viewSheetSet = BuildViewSheetSetInfo(updated),
                    modifiedCount = requestedViews.Count,
                    message = remove
                        ? $"Removed {requestedViews.Count} view/sheet item(s) from ViewSheetSet '{updated.Name}'."
                        : $"Added {requestedViews.Count} view/sheet item(s) to ViewSheetSet '{updated.Name}'."
                });
            }, ct);
        }

        private static ViewSheetSet? FindViewSheetSet(Document doc, string name)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheetSet))
                .Cast<ViewSheetSet>()
                .FirstOrDefault(s => string.Equals(s.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static ViewSheetSetInfo BuildViewSheetSetInfo(ViewSheetSet set)
        {
            var views = set.Views.Cast<View>()
                .OrderBy(v => v.Name)
                .Select(BuildViewInfo)
                .ToList();

            return new ViewSheetSetInfo
            {
                Id = set.Id.ToString(),
                Name = set.Name,
                ViewCount = views.Count,
                Views = views
            };
        }

        private static ViewSet BuildViewSet(Document doc, List<string> viewIds, List<string> sheetIds)
        {
            var set = new ViewSet();
            foreach (var view in ResolveViewsForSet(doc, viewIds, sheetIds))
                set.Insert(view);
            return set;
        }

        private static List<View> ResolveViewsForSet(Document doc, List<string> viewIds, List<string> sheetIds)
        {
            var views = new Dictionary<long, View>();

            foreach (var rawId in viewIds)
            {
                var view = ResolveView(doc, rawId);
                if (view.IsTemplate)
                    throw new InvalidOperationException($"View '{view.Name}' is a template and cannot be added.");
                views[view.Id.Value] = view;
            }

            foreach (var rawId in sheetIds)
            {
                var sheet = ResolveSheet(doc, rawId);
                views[sheet.Id.Value] = sheet;
            }

            return views.Values.ToList();
        }
    }
}
