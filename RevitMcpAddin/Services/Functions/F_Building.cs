using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using RevitMcpAddin.Models;

namespace RevitMcpAddin.Services
{
    public sealed partial class RevitService
    {
        public Task<object> UpdateWallAsync(UpdateBuildingElementRequest request, CancellationToken ct = default)
        {
            return UpdateTypedBuildingElementAsync<Wall>(request, "Wall", ct);
        }

        public Task<object> UpdateFloorAsync(UpdateBuildingElementRequest request, CancellationToken ct = default)
        {
            return UpdateTypedBuildingElementAsync<Floor>(request, "Floor", ct);
        }

        public Task<object> UpdateCeilingAsync(UpdateBuildingElementRequest request, CancellationToken ct = default)
        {
            return UpdateTypedBuildingElementAsync<Ceiling>(request, "Ceiling", ct);
        }

        public Task<object> CreateFloorAsync(CreateFloorRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var levelId = ResolveRequiredElementId(doc, request.LevelId, "levelId");
                var typeId = ResolveElementTypeId<FloorType>(doc, request.TypeId, "typeId");
                Floor floor;

                using (var tx = new Transaction(doc, "RevitMCP: Create Floor"))
                {
                    tx.Start();
                    floor = Floor.Create(doc, BuildCurveLoops(request.Points, request.Unit), typeId, levelId);
                    foreach (var (name, value) in request.Parameters)
                        TrySetParameter(floor, name, value, "instance");
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    element = BuildElementDetailInfo(floor, false, false),
                    elementId = floor.Id.ToString(),
                    message = $"Floor created successfully (id={floor.Id})."
                });
            }, ct);
        }

        public Task<object> CreateCeilingAsync(CreateCeilingRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var levelId = ResolveRequiredElementId(doc, request.LevelId, "levelId");
                var typeId = ResolveElementTypeId<CeilingType>(doc, request.TypeId, "typeId");
                Ceiling ceiling;

                using (var tx = new Transaction(doc, "RevitMCP: Create Ceiling"))
                {
                    tx.Start();
                    ceiling = Ceiling.Create(doc, BuildCurveLoops(request.Points, request.Unit), typeId, levelId);
                    foreach (var (name, value) in request.Parameters)
                        TrySetParameter(ceiling, name, value, "instance");
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    element = BuildElementDetailInfo(ceiling, false, false),
                    elementId = ceiling.Id.ToString(),
                    message = $"Ceiling created successfully (id={ceiling.Id})."
                });
            }, ct);
        }

        public Task<object> CreateRoomAsync(CreateRoomRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var level = ResolveElement(doc, request.LevelId, "levelId") as Level
                    ?? throw new InvalidOperationException($"Level id '{request.LevelId}' was not found.");
                Room room;

                using (var tx = new Transaction(doc, "RevitMCP: Create Room"))
                {
                    tx.Start();
                    var point = new UV(ToInternalFeet(request.X, request.Unit), ToInternalFeet(request.Y, request.Unit));
                    room = doc.Create.NewRoom(level, point);
                    ApplyRoomUpdates(room, request.Name, request.Number, request.Parameters);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    room = BuildRoomInfo(room),
                    elementId = room.Id.ToString(),
                    message = $"Room created successfully (id={room.Id})."
                });
            }, ct);
        }

        public Task<object> UpdateRoomAsync(UpdateRoomRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var room = ResolveElement(doc, request.RoomId, "roomId") as Room
                    ?? throw new InvalidOperationException($"Room id '{request.RoomId}' was not found.");

                using (var tx = new Transaction(doc, "RevitMCP: Update Room"))
                {
                    tx.Start();
                    ApplyRoomUpdates(room, request.Name, request.Number, request.Parameters);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    room = BuildRoomInfo(room),
                    elementId = room.Id.ToString(),
                    message = $"Room {room.Id} updated successfully."
                });
            }, ct);
        }

        private Task<object> UpdateTypedBuildingElementAsync<TElement>(
            UpdateBuildingElementRequest request,
            string label,
            CancellationToken ct)
            where TElement : Element
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var element = ResolveElement(doc, request.ElementId, "elementId") as TElement
                    ?? throw new InvalidOperationException($"{label} id '{request.ElementId}' was not found.");
                var changed = new List<string>();

                using (var tx = new Transaction(doc, $"RevitMCP: Update {label}"))
                {
                    tx.Start();
                    if (!string.IsNullOrWhiteSpace(request.TypeId))
                    {
                        element.ChangeTypeId(ResolveRequiredElementId(doc, request.TypeId, "typeId"));
                        changed.Add("typeId");
                    }
                    foreach (var (name, value) in request.Parameters)
                        TrySetParameter(element, name, value, "instance");
                    if (request.Parameters.Count > 0)
                        changed.Add("parameters");
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    element = BuildElementDetailInfo(element, false, false),
                    changed,
                    elementId = element.Id.ToString(),
                    message = $"{label} {element.Id} updated successfully."
                });
            }, ct);
        }

        private static IList<CurveLoop> BuildCurveLoops(
            IReadOnlyList<BoundaryPointRequest> points,
            string unit)
        {
            if (points.Count < 3)
                throw new ArgumentException("'points' must contain at least 3 boundary points.");

            var loop = new CurveLoop();
            for (var i = 0; i < points.Count; i++)
            {
                var start = ToXyz(points[i], unit);
                var end = ToXyz(points[(i + 1) % points.Count], unit);
                if (start.DistanceTo(end) < 1e-9)
                    throw new ArgumentException("Boundary contains duplicate adjacent points.");
                loop.Append(Line.CreateBound(start, end));
            }

            return new List<CurveLoop> { loop };
        }

        private static XYZ ToXyz(BoundaryPointRequest point, string unit)
        {
            return new XYZ(
                ToInternalFeet(point.X, unit),
                ToInternalFeet(point.Y, unit),
                ToInternalFeet(point.Z, unit));
        }

        private static ElementId ResolveElementTypeId<TType>(
            Document doc,
            string? rawId,
            string paramName)
            where TType : ElementType
        {
            if (!string.IsNullOrWhiteSpace(rawId))
            {
                var type = doc.GetElement(ResolveRequiredElementId(doc, rawId, paramName)) as TType;
                return type?.Id ?? throw new InvalidOperationException(
                    $"{typeof(TType).Name} id '{rawId}' was not found.");
            }

            var defaultType = new FilteredElementCollector(doc)
                .OfClass(typeof(TType))
                .Cast<TType>()
                .FirstOrDefault();
            return defaultType?.Id ?? throw new InvalidOperationException(
                $"No {typeof(TType).Name} was found in the document.");
        }

        private static void ApplyRoomUpdates(
            Room room,
            string? name,
            string? number,
            Dictionary<string, Newtonsoft.Json.Linq.JToken?> parameters)
        {
            if (!string.IsNullOrWhiteSpace(name))
                room.Name = name.Trim();
            if (!string.IsNullOrWhiteSpace(number))
                room.Number = number.Trim();
            foreach (var (paramName, value) in parameters)
                TrySetParameter(room, paramName, value, "instance");
        }

        private static object BuildRoomInfo(Room room)
        {
            return new
            {
                id = room.Id.ToString(),
                name = room.Name,
                number = room.Number,
                levelId = room.LevelId == ElementId.InvalidElementId ? null : room.LevelId.ToString(),
                area = room.Area,
                volume = room.Volume
            };
        }
    }
}
