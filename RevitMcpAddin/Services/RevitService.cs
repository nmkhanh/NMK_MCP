using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcpAddin.Models;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Services
{
    // ═══════════════════════════════════════════════════════════════════════
    //  RevitService
    //  High-level wrapper around the Revit API.
    //  All public methods call AsyncQueueService.EnqueueAsync → RevitTask
    //  so they are safe to call from any thread.
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Provides domain-level Revit operations consumed by MCP tool handlers.
    /// Never call Revit API directly from handlers; use this service instead.
    /// </summary>
    public sealed class RevitService
    {
        #region Fields

        private readonly AsyncQueueService _queue;

        #endregion

        #region Constructor

        public RevitService(AsyncQueueService queue)
        {
            _queue = queue;
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        #region Document

        /// <summary>Returns metadata about the active Revit document.</summary>
        public Task<DocumentInfo> GetDocumentInfoAsync(CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(async uiApp =>
            {
                var uidoc = uiApp.ActiveUIDocument
                    ?? throw new InvalidOperationException("No active UIDocument.");
                var doc = uidoc.Document;

                var info = new DocumentInfo
                {
                    Title          = doc.Title,
                    FilePath       = string.IsNullOrEmpty(doc.PathName) ? "(unsaved)" : doc.PathName,
                    IsModified     = doc.IsModified,
                    IsWorkshared   = doc.IsWorkshared,
                    ActiveViewName = uidoc.ActiveView?.Name     ?? "None",
                    ActiveViewType = uidoc.ActiveView?.ViewType.ToString() ?? "None",
                    ElementCount   = new FilteredElementCollector(doc)
                                         .WhereElementIsNotElementType()
                                         .GetElementCount(),
                    RevitVersion   = uiApp.Application.VersionName
                };

                return await Task.FromResult(info);
            }, ct);
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        #region Elements

        // Safety cap: never return more than this many elements in a single call
        // to avoid Revit UI freezing and MCP timeouts on large models.
        private const int HardMaxElements = 2_000;

        // Per-request timeout for element queries (longer than default to handle large categories)
        private const int ElementsTimeoutMs = 120_000;  // 2 minutes

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

                // Try OST_ prefix first, then raw name
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

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        #region Walls

        /// <summary>Creates a straight wall using the parameters in <paramref name="request"/>.</summary>
        public Task<WallCreationResult> CreateWallAsync(CreateWallRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(async uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                // ── Resolve Level ─────────────────────────────────────────
                var level = ResolveLevel(doc, request.LevelName)
                    ?? throw new InvalidOperationException(
                        $"Level '{request.LevelName}' not found. Check the level name.");

                // ── Resolve WallType (optional) ────────────────────────────
                WallType? wallType = null;
                if (!string.IsNullOrWhiteSpace(request.WallTypeName))
                {
                    wallType = ResolveWallType(doc, request.WallTypeName)
                        ?? throw new InvalidOperationException(
                            $"Wall type '{request.WallTypeName}' not found.");
                }
                else
                {
                    // Use first available wall type as default
                    wallType = new FilteredElementCollector(doc)
                        .OfClass(typeof(WallType))
                        .Cast<WallType>()
                        .FirstOrDefault()
                        ?? throw new InvalidOperationException("No wall types available in the project.");
                }

                // ── Build the baseline curve ───────────────────────────────
                var startPt = new XYZ(request.StartX, request.StartY, 0);
                var endPt   = new XYZ(request.EndX,   request.EndY,   0);
                var line    = Line.CreateBound(startPt, endPt);

                // ── Create wall inside a transaction ──────────────────────
                Wall wall;
                using (var tx = new Transaction(doc, "RevitMCP: Create Wall"))
                {
                    tx.Start();

                    wall = Wall.Create(
                        doc,
                        line,
                        wallType.Id,
                        level.Id,
                        request.Height,
                        offset: 0.0,
                        flip: false,
                        structural: false);

                    tx.Commit();
                }

                Logger.Info($"Wall created: id={wall.Id}, type={wallType.Name}, level={level.Name}");

                var result = new WallCreationResult
                {
                    Success      = true,
                    ElementId    = wall.Id.ToString(),
                    WallTypeName = wallType.Name,
                    LevelName    = level.Name,
                    Message      = $"Wall created successfully (id={wall.Id})."
                };

                return await Task.FromResult(result);
            }, ct);
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        #region Selection

        /// <summary>
        /// Selects elements in the active Revit UI by integer ElementId.
        /// Optionally zooms the view to fit the selection.
        /// </summary>
        /// <param name="ids">Integer element ids to select.</param>
        /// <param name="zoom">If true, calls <see cref="UIDocument.ShowElements"/> to fit the view.</param>
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
                var validIds = new List<ElementId>();
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

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        #region Private Helpers

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

        private static Level? ResolveLevel(Document doc, string levelName)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => string.Equals(l.Name, levelName, StringComparison.OrdinalIgnoreCase));
        }

        private static WallType? ResolveWallType(Document doc, string typeName)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(w => string.Equals(w.Name, typeName, StringComparison.OrdinalIgnoreCase));
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

        #endregion
    }
}
