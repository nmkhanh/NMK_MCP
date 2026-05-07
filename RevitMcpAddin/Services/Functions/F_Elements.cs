using System.Diagnostics;
using Autodesk.Revit.DB;
using RevitMcpAddin.Models;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Services
{
    // ═══════════════════════════════════════════════════════════════════════
    //  F_Elements — Element querying
    //  Part of the RevitService partial class.
    // ═══════════════════════════════════════════════════════════════════════

    public sealed partial class RevitService
    {
        // Safety cap: never return more than this many elements in a single call
        // to avoid Revit UI freezing and MCP timeouts on large models.
        private const int HardMaxElements = 2_000;

        // Per-request timeout for element queries (longer than default to handle large categories)
        private const int ElementsTimeoutMs = 120_000; // 2 minutes

        /// <summary>
        /// Returns elements filtered by a Revit built-in category name.
        /// <paramref name="categoryName"/> should match the suffix of a
        /// <see cref="BuiltInCategory"/> value, e.g. "Walls" → OST_Walls.
        /// </summary>
        /// <param name="categoryName">Category name (OST_ suffix).</param>
        /// <param name="limit">Max elements. 0 / int.MaxValue = all (capped at <see cref="HardMaxElements"/>).</param>
        /// <param name="includeParameters">When false (default), skips parameter extraction for speed.</param>
        public Task<List<ElementInfo>> GetElementsAsync(
            string categoryName,
            int limit = 50,
            bool includeParameters = false,
            CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(async uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                if (!TryResolveCategory(categoryName, out var bic))
                    throw new ArgumentException($"Unknown or unsupported category: '{categoryName}'. " +
                        "Use the suffix of a BuiltInCategory, e.g. 'Walls', 'Doors', 'Windows'.");

                // Resolve effective limit — always cap at HardMaxElements
                var effectiveLimit = (limit <= 0 || limit == int.MaxValue)
                    ? HardMaxElements
                    : Math.Min(limit, HardMaxElements);

                var elements = new FilteredElementCollector(doc)
                    .OfCategory(bic)
                    .WhereElementIsNotElementType()
                    .Take(effectiveLimit)
                    .Cast<Element>()
                    .ToList();

                Trace.WriteLine(
                    $"[RevitMCP][ELEMENTS] category={categoryName}, found={elements.Count}, " +
                    $"limit={effectiveLimit}, includeParams={includeParameters}");

                var result = elements.Select(e => BuildElementInfo(e, includeParameters)).ToList();
                return await Task.FromResult(result);

            }, ct, timeoutMs: ElementsTimeoutMs);
        }

        // ── Private Helpers ──────────────────────────────────────────────

        private static bool TryResolveCategory(string name, out BuiltInCategory bic)
        {
            bic = BuiltInCategory.INVALID;

            // Try "OST_Walls" format first (user provides "Walls" → we try "OST_Walls")
            if (Enum.TryParse<BuiltInCategory>($"OST_{name}", ignoreCase: true, out var bic1))
            {
                bic = bic1;
                return true;
            }

            // Try the name as-is (user already provided "OST_Walls")
            if (Enum.TryParse<BuiltInCategory>(name, ignoreCase: true, out var bic2))
            {
                bic = bic2;
                return true;
            }

            return false;
        }

        private static ElementInfo BuildElementInfo(Element e, bool includeParameters = false)
        {
            var info = new ElementInfo
            {
                Id         = e.Id.ToString(),
                Name       = e.Name ?? string.Empty,
                Category   = e.Category?.Name ?? "Unknown",
                LevelId    = e.LevelId?.ToString(),
                FamilyName = (e is FamilyInstance fi) ? fi.Symbol.FamilyName : string.Empty,
                TypeName   = doc_GetTypeName(e)
            };

            if (!includeParameters) return info;

            // Best-effort parameter extraction (only when explicitly requested)
            try
            {
                int max = 20;
                foreach (Parameter p in e.Parameters)
                {
                    if (max-- <= 0) break;
                    if (!p.HasValue) continue;
                    var paramName = p.Definition?.Name;
                    if (string.IsNullOrWhiteSpace(paramName)) continue;
                    info.Parameters[paramName] = p.AsValueString() ?? p.AsString() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Parameter extraction failed for element {e.Id}: {ex.Message}");
            }

            return info;
        }

        private static string doc_GetTypeName(Element e)
        {
            try
            {
                return e.Document.GetElement(e.GetTypeId())?.Name ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
