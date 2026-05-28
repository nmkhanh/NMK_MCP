using Autodesk.Revit.DB;
using RevitMcpAddin.Models;

namespace RevitMcpAddin.Services
{
    public sealed partial class RevitService
    {
        public Task<object> ActivateFamilySymbolAsync(
            ActivateFamilySymbolRequest request,
            CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var symbol = ResolveElement(doc, request.SymbolId, "symbolId") as FamilySymbol
                    ?? throw new InvalidOperationException($"FamilySymbol id '{request.SymbolId}' was not found.");

                using (var tx = new Transaction(doc, "RevitMCP: Activate Family Symbol"))
                {
                    tx.Start();
                    if (!symbol.IsActive)
                    {
                        symbol.Activate();
                        doc.Regenerate();
                    }
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    symbol = BuildElementTypeSummary(symbol),
                    elementId = symbol.Id.ToString(),
                    message = $"Family symbol '{symbol.FamilyName}: {symbol.Name}' is active."
                });
            }, ct);
        }

        public Task<object> GetRevitLinksAsync(CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var links = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .OrderBy(l => l.Name)
                    .Select(link =>
                    {
                        var linkedDoc = link.GetLinkDocument();
                        return new
                        {
                            id = link.Id.ToString(),
                            name = link.Name,
                            typeId = link.GetTypeId().ToString(),
                            linkedDocumentTitle = linkedDoc?.Title,
                            linkedDocumentPath = linkedDoc?.PathName,
                            isLoaded = linkedDoc != null
                        };
                    })
                    .ToList();

                return Task.FromResult<object>(new
                {
                    success = true,
                    count = links.Count,
                    links,
                    message = $"Found {links.Count} Revit link instance(s)."
                });
            }, ct);
        }

        public Task<object> GetDesignOptionsAsync(CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var options = new FilteredElementCollector(doc)
                    .OfClass(typeof(DesignOption))
                    .Cast<DesignOption>()
                    .OrderBy(o => o.Name)
                    .Select(o => new
                    {
                        id = o.Id.ToString(),
                        name = o.Name,
                        isPrimary = o.IsPrimary
                    })
                    .ToList();

                return Task.FromResult<object>(new
                {
                    success = true,
                    count = options.Count,
                    designOptions = options,
                    message = $"Found {options.Count} design option(s)."
                });
            }, ct);
        }
    }
}
