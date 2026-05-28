using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;

namespace RevitMcpAddin.Services
{
    public sealed partial class RevitService
    {
        public Task<object> CreateRoofAsync(CreateRoofRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var level = ResolveElement(doc, request.LevelId, "levelId") as Level
                    ?? throw new InvalidOperationException($"Level id '{request.LevelId}' was not found.");
                var roofType = ResolveElementType<RoofType>(doc, request.RoofTypeId, "roofTypeId");
                FootPrintRoof roof;

                using (var tx = new Transaction(doc, "RevitMCP: Create Roof"))
                {
                    tx.Start();
                    var profile = BuildCurveArray(request.Points, request.Unit);
                    ModelCurveArray mapping;
                    roof = doc.Create.NewFootPrintRoof(profile, level, roofType, out mapping);
                    foreach (var (name, value) in request.Parameters)
                        TrySetParameter(roof, name, value, "instance");
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    element = BuildElementDetailInfo(roof, false, false),
                    elementId = roof.Id.ToString(),
                    message = $"Roof created successfully (id={roof.Id})."
                });
            }, ct);
        }

        public Task<object> UpdateRoofAsync(UpdateBuildingElementRequest request, CancellationToken ct = default)
        {
            return UpdateTypedBuildingElementAsync<RoofBase>(request, "Roof", ct);
        }

        public Task<object> CreateOpeningAsync(CreateOpeningRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var host = ResolveElement(doc, request.HostId, "hostId");
                Opening opening;

                using (var tx = new Transaction(doc, "RevitMCP: Create Opening"))
                {
                    tx.Start();
                    opening = CreateOpeningOnHost(doc, host, request);
                    foreach (var (name, value) in request.Parameters)
                        TrySetParameter(opening, name, value, "instance");
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    element = BuildElementDetailInfo(opening, false, false),
                    elementId = opening.Id.ToString(),
                    hostId = host.Id.ToString(),
                    message = $"Opening created successfully (id={opening.Id})."
                });
            }, ct);
        }

        public Task<object> UpdateOpeningAsync(UpdateBuildingElementRequest request, CancellationToken ct = default)
        {
            return UpdateTypedBuildingElementAsync<Opening>(request, "Opening", ct);
        }

        public Task<object> CreateAreaAsync(CreateAreaRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var view = ResolveView(doc, request.ViewId) as ViewPlan
                    ?? throw new InvalidOperationException($"View id '{request.ViewId}' is not a plan view.");
                Area area;

                using (var tx = new Transaction(doc, "RevitMCP: Create Area"))
                {
                    tx.Start();
                    var point = new UV(ToInternalFeet(request.X, request.Unit), ToInternalFeet(request.Y, request.Unit));
                    area = doc.Create.NewArea(view, point);
                    ApplySpatialUpdates(area, request.Name, request.Number, request.Parameters);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    area = BuildSpatialInfo(area),
                    elementId = area.Id.ToString(),
                    message = $"Area created successfully (id={area.Id})."
                });
            }, ct);
        }

        public Task<object> UpdateAreaAsync(UpdateSpatialElementRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var area = ResolveElement(doc, request.ElementId, "elementId") as Area
                    ?? throw new InvalidOperationException($"Area id '{request.ElementId}' was not found.");

                using (var tx = new Transaction(doc, "RevitMCP: Update Area"))
                {
                    tx.Start();
                    ApplySpatialUpdates(area, request.Name, request.Number, request.Parameters);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    area = BuildSpatialInfo(area),
                    elementId = area.Id.ToString(),
                    message = $"Area {area.Id} updated successfully."
                });
            }, ct);
        }

        public Task<object> CreateSpaceAsync(CreateSpaceRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var level = ResolveElement(doc, request.LevelId, "levelId") as Level
                    ?? throw new InvalidOperationException($"Level id '{request.LevelId}' was not found.");
                Space space;

                using (var tx = new Transaction(doc, "RevitMCP: Create Space"))
                {
                    tx.Start();
                    var point = new UV(ToInternalFeet(request.X, request.Unit), ToInternalFeet(request.Y, request.Unit));
                    space = doc.Create.NewSpace(level, point);
                    ApplySpatialUpdates(space, request.Name, request.Number, request.Parameters);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    space = BuildSpatialInfo(space),
                    elementId = space.Id.ToString(),
                    message = $"Space created successfully (id={space.Id})."
                });
            }, ct);
        }

        public Task<object> UpdateSpaceAsync(UpdateSpatialElementRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var space = ResolveElement(doc, request.ElementId, "elementId") as Space
                    ?? throw new InvalidOperationException($"Space id '{request.ElementId}' was not found.");

                using (var tx = new Transaction(doc, "RevitMCP: Update Space"))
                {
                    tx.Start();
                    ApplySpatialUpdates(space, request.Name, request.Number, request.Parameters);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    space = BuildSpatialInfo(space),
                    elementId = space.Id.ToString(),
                    message = $"Space {space.Id} updated successfully."
                });
            }, ct);
        }

        public Task<object> CreateTagAsync(CreateTagRequest request, CancellationToken ct = default)
        {
            return CreateIndependentTagAsync(request, "Tag", ct);
        }

        public Task<object> PlaceRoomTagAsync(CreateTagRequest request, CancellationToken ct = default)
        {
            return CreateIndependentTagAsync(request, "Room tag", ct);
        }

        public Task<object> PlaceAreaTagAsync(CreateTagRequest request, CancellationToken ct = default)
        {
            return CreateIndependentTagAsync(request, "Area tag", ct);
        }

        public Task<object> PlaceSpaceTagAsync(CreateTagRequest request, CancellationToken ct = default)
        {
            return CreateIndependentTagAsync(request, "Space tag", ct);
        }

        public Task<object> CreateDimensionAsync(CreateDimensionRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var view = ResolveView(doc, request.ViewId);
                if (request.ElementIds.Count < 2)
                    throw new ArgumentException("'elementIds' must contain at least two elements.");
                Dimension dimension;

                using (var tx = new Transaction(doc, "RevitMCP: Create Dimension"))
                {
                    tx.Start();
                    var refs = new ReferenceArray();
                    foreach (var rawId in request.ElementIds)
                    {
                        var element = ResolveElement(doc, rawId, "elementIds");
                        refs.Append(new Reference(element));
                    }
                    var line = Line.CreateBound(
                        new XYZ(ToInternalFeet(request.StartX, request.Unit), ToInternalFeet(request.StartY, request.Unit), ToInternalFeet(request.StartZ, request.Unit)),
                        new XYZ(ToInternalFeet(request.EndX, request.Unit), ToInternalFeet(request.EndY, request.Unit), ToInternalFeet(request.EndZ, request.Unit)));
                    dimension = doc.Create.NewDimension(view, line, refs);
                    if (!string.IsNullOrWhiteSpace(request.DimensionTypeId))
                        dimension.ChangeTypeId(ResolveRequiredElementId(doc, request.DimensionTypeId, "dimensionTypeId"));
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    elementId = dimension.Id.ToString(),
                    viewId = view.Id.ToString(),
                    message = $"Dimension created successfully (id={dimension.Id})."
                });
            }, ct);
        }

        public Task<object> CreateFilledRegionAsync(CreateFilledRegionRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var view = ResolveView(doc, request.ViewId);
                var typeId = ResolveFilledRegionTypeId(doc, request.FilledRegionTypeId);
                FilledRegion region;

                using (var tx = new Transaction(doc, "RevitMCP: Create Filled Region"))
                {
                    tx.Start();
                    region = FilledRegion.Create(doc, typeId, view.Id, BuildCurveLoops(request.Points, request.Unit));
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    elementId = region.Id.ToString(),
                    viewId = view.Id.ToString(),
                    message = $"Filled region created successfully (id={region.Id})."
                });
            }, ct);
        }

        private Task<object> CreateIndependentTagAsync(CreateTagRequest request, string label, CancellationToken ct)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var view = ResolveView(doc, request.ViewId);
                var element = ResolveElement(doc, request.ElementId, "elementId");
                IndependentTag tag;

                using (var tx = new Transaction(doc, $"RevitMCP: Create {label}"))
                {
                    tx.Start();
                    var point = new XYZ(
                        ToInternalFeet(request.X, request.Unit),
                        ToInternalFeet(request.Y, request.Unit),
                        ToInternalFeet(request.Z, request.Unit));
                    tag = IndependentTag.Create(
                        doc,
                        view.Id,
                        new Reference(element),
                        request.AddLeader,
                        TagMode.TM_ADDBY_CATEGORY,
                        TagOrientation.Horizontal,
                        point);
                    if (!string.IsNullOrWhiteSpace(request.TagTypeId))
                        tag.ChangeTypeId(ResolveRequiredElementId(doc, request.TagTypeId, "tagTypeId"));
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    elementId = tag.Id.ToString(),
                    taggedElementId = element.Id.ToString(),
                    viewId = view.Id.ToString(),
                    message = $"{label} created successfully (id={tag.Id})."
                });
            }, ct);
        }

        private static CurveArray BuildCurveArray(IReadOnlyList<BoundaryPointRequest> points, string unit)
        {
            if (points.Count < 3)
                throw new ArgumentException("'points' must contain at least 3 boundary points.");

            var curves = new CurveArray();
            for (var i = 0; i < points.Count; i++)
            {
                var start = new XYZ(ToInternalFeet(points[i].X, unit), ToInternalFeet(points[i].Y, unit), ToInternalFeet(points[i].Z, unit));
                var endPoint = points[(i + 1) % points.Count];
                var end = new XYZ(ToInternalFeet(endPoint.X, unit), ToInternalFeet(endPoint.Y, unit), ToInternalFeet(endPoint.Z, unit));
                if (start.DistanceTo(end) < 1e-9)
                    throw new ArgumentException("Boundary contains duplicate adjacent points.");
                curves.Append(Line.CreateBound(start, end));
            }

            return curves;
        }

        private static Opening CreateOpeningOnHost(Document doc, Element host, CreateOpeningRequest request)
        {
            var min = new XYZ(
                ToInternalFeet(request.MinX, request.Unit),
                ToInternalFeet(request.MinY, request.Unit),
                ToInternalFeet(request.MinZ, request.Unit));
            var max = new XYZ(
                ToInternalFeet(request.MaxX, request.Unit),
                ToInternalFeet(request.MaxY, request.Unit),
                ToInternalFeet(request.MaxZ, request.Unit));

            if (host is Wall wall)
                return doc.Create.NewOpening(wall, min, max);

            var profile = new CurveArray();
            var p1 = new XYZ(min.X, min.Y, min.Z);
            var p2 = new XYZ(max.X, min.Y, min.Z);
            var p3 = new XYZ(max.X, max.Y, min.Z);
            var p4 = new XYZ(min.X, max.Y, min.Z);
            profile.Append(Line.CreateBound(p1, p2));
            profile.Append(Line.CreateBound(p2, p3));
            profile.Append(Line.CreateBound(p3, p4));
            profile.Append(Line.CreateBound(p4, p1));
            return doc.Create.NewOpening(host, profile, true);
        }

        private static void ApplySpatialUpdates(
            SpatialElement element,
            string? name,
            string? number,
            Dictionary<string, JToken?> parameters)
        {
            if (!string.IsNullOrWhiteSpace(name))
                TrySetParameter(element, "Name", JToken.FromObject(name.Trim()), "instance");
            if (!string.IsNullOrWhiteSpace(number))
                TrySetParameter(element, "Number", JToken.FromObject(number.Trim()), "instance");
            foreach (var (paramName, value) in parameters)
                TrySetParameter(element, paramName, value, "instance");
        }

        private static object BuildSpatialInfo(SpatialElement element)
        {
            return new
            {
                id = element.Id.ToString(),
                name = element.Name,
                number = element.LookupParameter("Number")?.AsString(),
                levelId = element.LevelId == ElementId.InvalidElementId ? null : element.LevelId.ToString(),
                area = element.Area,
                volume = element is Room room ? room.Volume : element is Space space ? space.Volume : (double?)null,
                category = element.Category?.Name
            };
        }

        private static TType ResolveElementType<TType>(Document doc, string? rawId, string paramName)
            where TType : ElementType
        {
            if (!string.IsNullOrWhiteSpace(rawId))
            {
                var type = doc.GetElement(ResolveRequiredElementId(doc, rawId, paramName)) as TType;
                return type ?? throw new InvalidOperationException($"{typeof(TType).Name} id '{rawId}' was not found.");
            }

            return new FilteredElementCollector(doc)
                .OfClass(typeof(TType))
                .Cast<TType>()
                .FirstOrDefault()
                ?? throw new InvalidOperationException($"No {typeof(TType).Name} was found in the document.");
        }

        private static ElementId ResolveFilledRegionTypeId(Document doc, string? rawId)
        {
            if (!string.IsNullOrWhiteSpace(rawId))
                return ResolveRequiredElementId(doc, rawId, "filledRegionTypeId");

            var typeId = new FilteredElementCollector(doc)
                .OfClass(typeof(FilledRegionType))
                .FirstElementId();
            return typeId == ElementId.InvalidElementId
                ? throw new InvalidOperationException("No FilledRegionType was found in the document.")
                : typeId;
        }
    }
}
