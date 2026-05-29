using Autodesk.Revit.DB;
using RevitMcpAddin.Models;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Services
{
    public sealed partial class RevitService
    {
        public Task<object> GetLevelsAsync(
            bool useActiveView = false,
            string? viewId = null,
            CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var levels = CreateScopedElementCollector(
                        doc,
                        uiApp.ActiveUIDocument?.ActiveView,
                        useActiveView,
                        viewId)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .Select(BuildLevelInfo)
                    .ToList();

                return Task.FromResult<object>(new
                {
                    success = true,
                    count = levels.Count,
                    useActiveView,
                    viewId,
                    levels,
                    message = $"Found {levels.Count} level(s)."
                });
            }, ct);
        }

        public Task<object> CreateLevelAsync(CreateLevelRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var elevationFeet = ToInternalFeet(request.Elevation, request.Unit);
                Level level;

                using (var tx = new Transaction(doc, "RevitMCP: Create Level"))
                {
                    tx.Start();
                    level = Level.Create(doc, elevationFeet);

                    if (!string.IsNullOrWhiteSpace(request.Name))
                        level.Name = request.Name.Trim();

                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                Logger.Info($"Level created: id={level.Id}, name={level.Name}, elevation={level.Elevation}");

                return Task.FromResult<object>(new
                {
                    success = true,
                    level = BuildLevelInfo(level),
                    elementId = level.Id.ToString(),
                    message = $"Level '{level.Name}' created successfully (id={level.Id})."
                });
            }, ct);
        }

        public Task<object> UpdateLevelAsync(UpdateLevelRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var level = ResolveLevel(doc, request.LevelId);
                var changed = new List<string>();

                using (var tx = new Transaction(doc, "RevitMCP: Update Level"))
                {
                    tx.Start();

                    if (!string.IsNullOrWhiteSpace(request.Name) &&
                        !string.Equals(level.Name, request.Name.Trim(), StringComparison.Ordinal))
                    {
                        level.Name = request.Name.Trim();
                        changed.Add("name");
                    }

                    if (request.Elevation.HasValue)
                    {
                        var elevationFeet = ToInternalFeet(request.Elevation.Value, request.Unit);
                        var elevParam = level.get_Parameter(BuiltInParameter.LEVEL_ELEV);
                        if (elevParam == null || elevParam.IsReadOnly)
                            throw new InvalidOperationException("Level elevation parameter is not writable.");

                        elevParam.Set(elevationFeet);
                        changed.Add("elevation");
                    }

                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                Logger.Info($"Level updated: id={level.Id}, changed={string.Join(",", changed)}");

                return Task.FromResult<object>(new
                {
                    success = true,
                    level = BuildLevelInfo(level),
                    changed,
                    message = changed.Count == 0
                        ? $"Level '{level.Name}' had no requested changes."
                        : $"Level '{level.Name}' updated successfully."
                });
            }, ct);
        }

        private static Level ResolveLevel(Document doc, string? rawId)
        {
            if (string.IsNullOrWhiteSpace(rawId))
                throw new ArgumentException("'levelId' is required.");

            if (!int.TryParse(rawId.Trim(), out var intId))
                throw new ArgumentException($"Invalid levelId '{rawId}'. Expected an integer Revit ElementId.");

            var level = doc.GetElement(new ElementId(intId)) as Level;
            return level ?? throw new InvalidOperationException($"Level id '{rawId}' was not found.");
        }

        private static LevelInfo BuildLevelInfo(Level level)
        {
            return new LevelInfo
            {
                Id = level.Id.ToString(),
                Name = level.Name,
                Elevation = level.Elevation,
                ElevationMeters = Math.Round(
                    UnitUtils.ConvertFromInternalUnits(level.Elevation, UnitTypeId.Meters), 6)
            };
        }

        private static double ToInternalFeet(double value, string? unit)
        {
            return unit?.Trim().ToLowerInvariant() switch
            {
                "m" or "meter" or "meters" or "metre" or "metres" =>
                    UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Meters),
                "mm" or "millimeter" or "millimeters" or "millimetre" or "millimetres" =>
                    UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters),
                "ft" or "foot" or "feet" or null or "" => value,
                _ => throw new ArgumentException($"Unsupported unit '{unit}'. Use feet, meters, or millimeters.")
            };
        }
    }
}
