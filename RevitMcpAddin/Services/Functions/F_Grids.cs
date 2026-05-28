using Autodesk.Revit.DB;
using RevitMcpAddin.Models;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Services
{
    public sealed partial class RevitService
    {
        public Task<object> GetGridsAsync(CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var grids = new FilteredElementCollector(doc)
                    .OfClass(typeof(Grid))
                    .Cast<Grid>()
                    .OrderBy(g => g.Name)
                    .Select(BuildGridInfo)
                    .ToList();

                return Task.FromResult<object>(new
                {
                    success = true,
                    count = grids.Count,
                    grids,
                    message = $"Found {grids.Count} grid(s)."
                });
            }, ct);
        }

        public Task<object> CreateGridAsync(CreateGridRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var start = new XYZ(
                    ToInternalFeet(request.StartX, request.Unit),
                    ToInternalFeet(request.StartY, request.Unit),
                    ToInternalFeet(request.Z, request.Unit));

                var end = new XYZ(
                    ToInternalFeet(request.EndX, request.Unit),
                    ToInternalFeet(request.EndY, request.Unit),
                    ToInternalFeet(request.Z, request.Unit));

                if (start.DistanceTo(end) < 1e-9)
                    throw new ArgumentException("Grid start and end points must be different.");

                Grid grid;
                using (var tx = new Transaction(doc, "RevitMCP: Create Grid"))
                {
                    tx.Start();

                    var line = Line.CreateBound(start, end);
                    grid = Grid.Create(doc, line);

                    if (!string.IsNullOrWhiteSpace(request.Name))
                        grid.Name = request.Name.Trim();

                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                Logger.Info($"Grid created: id={grid.Id}, name={grid.Name}");

                return Task.FromResult<object>(new
                {
                    success = true,
                    grid = BuildGridInfo(grid),
                    elementId = grid.Id.ToString(),
                    message = $"Grid '{grid.Name}' created successfully (id={grid.Id})."
                });
            }, ct);
        }

        public Task<object> UpdateGridAsync(UpdateGridRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var grid = ResolveGrid(doc, request.GridId);
                var changed = new List<string>();

                using (var tx = new Transaction(doc, "RevitMCP: Update Grid"))
                {
                    tx.Start();

                    if (!string.IsNullOrWhiteSpace(request.Name) &&
                        !string.Equals(grid.Name, request.Name.Trim(), StringComparison.Ordinal))
                    {
                        grid.Name = request.Name.Trim();
                        changed.Add("name");
                    }

                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                Logger.Info($"Grid updated: id={grid.Id}, changed={string.Join(",", changed)}");

                return Task.FromResult<object>(new
                {
                    success = true,
                    grid = BuildGridInfo(grid),
                    changed,
                    message = changed.Count == 0
                        ? $"Grid '{grid.Name}' had no requested changes."
                        : $"Grid '{grid.Name}' updated successfully."
                });
            }, ct);
        }

        private static Grid ResolveGrid(Document doc, string? rawId)
        {
            if (string.IsNullOrWhiteSpace(rawId))
                throw new ArgumentException("'gridId' is required.");

            if (!int.TryParse(rawId.Trim(), out var intId))
                throw new ArgumentException($"Invalid gridId '{rawId}'. Expected an integer Revit ElementId.");

            var grid = doc.GetElement(new ElementId(intId)) as Grid;
            return grid ?? throw new InvalidOperationException($"Grid id '{rawId}' was not found.");
        }

        private static GridInfo BuildGridInfo(Grid grid)
        {
            var curve = grid.Curve;
            var info = new GridInfo
            {
                Id = grid.Id.ToString(),
                Name = grid.Name,
                CurveType = curve.GetType().Name
            };

            if (curve is Line line)
            {
                info.CurveType = "Line";
                info.Start = BuildPointInfo(line.GetEndPoint(0));
                info.End = BuildPointInfo(line.GetEndPoint(1));
            }
            else if (curve is Arc arc)
            {
                info.CurveType = "Arc";
                info.Start = BuildPointInfo(arc.GetEndPoint(0));
                info.End = BuildPointInfo(arc.GetEndPoint(1));
                info.Center = BuildPointInfo(arc.Center);
                info.Radius = arc.Radius;
            }

            return info;
        }

        private static PointInfo BuildPointInfo(XYZ point)
        {
            return new PointInfo
            {
                X = point.X,
                Y = point.Y,
                Z = point.Z
            };
        }
    }
}
