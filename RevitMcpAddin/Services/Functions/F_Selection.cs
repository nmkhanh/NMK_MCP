using System.Diagnostics;
using Autodesk.Revit.DB;

namespace RevitMcpAddin.Services
{
    // ═══════════════════════════════════════════════════════════════════════
    //  F_Selection — UI element selection
    //  Part of the RevitService partial class.
    // ═══════════════════════════════════════════════════════════════════════

    public sealed partial class RevitService
    {
        /// <summary>
        /// Selects elements in the active Revit UI by integer ElementId.
        /// Optionally zooms the view to fit the selection.
        /// </summary>
        /// <param name="ids">Integer element ids to select.</param>
        /// <param name="zoom">If true, calls <see cref="Autodesk.Revit.UI.UIDocument.ShowElements"/> to fit the view.</param>
        /// <param name="clearPrevious">If true, clears the existing selection before applying the new one.</param>
        public Task<object> SelectElementsAsync(
            List<int> ids,
            bool zoom          = true,
            bool clearPrevious = true,
            CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(async uiApp =>
            {
                var uidoc = uiApp.ActiveUIDocument
                    ?? throw new InvalidOperationException("No active UIDocument.");
                var doc = uidoc.Document;

                // ── Convert ints → ElementIds that exist in the document ──
                var validIds   = new List<ElementId>();
                var missingIds = new List<int>();

                foreach (var rawId in ids)
                {
                    var eid = new ElementId(rawId);
                    if (doc.GetElement(eid) != null)
                        validIds.Add(eid);
                    else
                        missingIds.Add(rawId);
                }

                Trace.WriteLine(
                    $"[RevitMCP][SELECT] requested={ids.Count}, valid={validIds.Count}, " +
                    $"missing={missingIds.Count}");

                // ── Clear previous selection if requested ─────────────────
                if (clearPrevious)
                    uidoc.Selection.SetElementIds(new List<ElementId>());

                // ── Apply selection ───────────────────────────────────────
                if (validIds.Count > 0)
                {
                    try
                    {
                        uidoc.Selection.SetElementIds(validIds);
                        Trace.WriteLine(
                            $"[RevitMCP][SELECT] selected ids: {string.Join(", ", validIds.Select(e => e.Value))}");
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"[RevitMCP][SELECT] SetElementIds failed: {ex.Message}");
                        throw;
                    }

                    // ── Zoom to fit ───────────────────────────────────────
                    if (zoom)
                    {
                        try
                        {
                            uidoc.ShowElements(validIds);
                            Trace.WriteLine("[RevitMCP][SELECT] ShowElements called.");
                        }
                        catch (Exception ex)
                        {
                            // Non-fatal: selection succeeded; log and continue
                            Trace.WriteLine($"[RevitMCP][SELECT] ShowElements failed (non-fatal): {ex.Message}");
                        }
                    }
                }

                var result = new
                {
                    success     = true,
                    count       = validIds.Count,
                    selectedIds = validIds.Select(e => e.Value).ToList(),
                    missingIds,
                    message     = validIds.Count == ids.Count
                        ? $"Selected {validIds.Count} element(s)."
                        : $"Selected {validIds.Count} of {ids.Count} element(s). " +
                          $"{missingIds.Count} id(s) not found in the document."
                };

                return await Task.FromResult<object>(result);
            }, ct);
        }
    }
}
