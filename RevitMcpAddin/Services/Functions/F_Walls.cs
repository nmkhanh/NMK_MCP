using Autodesk.Revit.DB;
using RevitMcpAddin.Models;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Services
{
    // ═══════════════════════════════════════════════════════════════════════
    //  F_Walls — Wall creation
    //  Part of the RevitService partial class.
    // ═══════════════════════════════════════════════════════════════════════

    public sealed partial class RevitService
    {
        /// <summary>Creates a straight wall using the parameters in <paramref name="request"/>.</summary>
        public Task<WallCreationResult> CreateWallAsync(CreateWallRequest request, CancellationToken ct = default)
        {
            // NOT async: runs synchronously on the Revit main thread inside RevitTask.RunAsync.
            // Task.FromResult avoids async state-machine overhead and keeps exception
            // propagation straightforward.
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                // ── Resolve Level ─────────────────────────────────────────
                // List available levels in the error message to help diagnosis.
                var allLevels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .ToList();

                var level = allLevels
                    .FirstOrDefault(l => string.Equals(l.Name, request.LevelName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException(
                        $"Level '{request.LevelName}' not found. " +
                        $"Available levels: {string.Join(", ", allLevels.Select(l => l.Name))}");

                // ── Resolve WallType (optional) ────────────────────────────
                WallType? wallType;
                if (!string.IsNullOrWhiteSpace(request.WallTypeName))
                {
                    wallType = ResolveWallType(doc, request.WallTypeName)
                        ?? throw new InvalidOperationException(
                            $"Wall type '{request.WallTypeName}' not found.");
                }
                else
                {
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

                    // Set failure handling AFTER Start() so options are preserved.
                    // Suppress warning dialogs — they block the Revit main thread.
                    var fo = tx.GetFailureHandlingOptions();
                    fo.SetClearAfterRollback(true);
                    fo.SetFailuresPreprocessor(new SilentFailurePreprocessor());
                    tx.SetFailureHandlingOptions(fo);

                    wall = Wall.Create(
                        doc,
                        line,
                        wallType.Id,
                        level.Id,
                        request.Height,
                        offset: 0.0,
                        flip: false,
                        structural: false);

                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException(
                            $"Transaction did not commit (status={status}). " +
                            "Check model warnings or try a different wall location.");
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

                return Task.FromResult(result);
            }, ct);
        }

        // ── Private Helpers ──────────────────────────────────────────────

        private static WallType? ResolveWallType(Document doc, string typeName)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(w => string.Equals(w.Name, typeName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Silently suppresses all Revit warning dialogs during transaction commit
        /// so the Revit main thread is never blocked.
        /// Rolls back cleanly on hard errors.
        /// </summary>
        private sealed class SilentFailurePreprocessor : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(FailuresAccessor a)
            {
                var messages = a.GetFailureMessages();

                // If there are hard errors, roll back without showing a dialog.
                if (messages.Any(m => m.GetSeverity() == FailureSeverity.Error))
                    return FailureProcessingResult.ProceedWithRollBack;

                // Delete each warning individually (more reliable than DeleteAllWarnings
                // in some Revit versions).
                foreach (var msg in messages)
                    a.DeleteWarning(msg);

                return FailureProcessingResult.Continue;
            }
        }
    }
}
