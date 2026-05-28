using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using RevitMcpAddin.Models;

namespace RevitMcpAddin.Services
{
    public sealed partial class RevitService
    {
        public Task<object> GetConnectorsAsync(GetConnectorsRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var element = ResolveElement(doc, request.ElementId, "elementId");
                var connectors = GetConnectors(element)
                    .Select((connector, index) => BuildConnectorInfo(connector, index))
                    .ToList();

                return Task.FromResult<object>(new
                {
                    success = true,
                    elementId = element.Id.ToString(),
                    count = connectors.Count,
                    connectors,
                    message = $"Found {connectors.Count} connector(s) on element {element.Id}."
                });
            }, ct);
        }

        public Task<object> CreatePipeAsync(CreateMepCurveRequest request, CancellationToken ct = default)
        {
            return CreateMepCurveAsync(
                request,
                "Pipe",
                doc => ResolveElementTypeId<PipeType>(doc, request.TypeId, "typeId"),
                doc => ResolveSystemTypeId<PipingSystemType>(doc, request.SystemTypeId, "systemTypeId"),
                (doc, typeId, systemTypeId, levelId, start, end) => Pipe.Create(doc, systemTypeId, typeId, levelId, start, end),
                ct);
        }

        public Task<object> CreateDuctAsync(CreateMepCurveRequest request, CancellationToken ct = default)
        {
            return CreateMepCurveAsync(
                request,
                "Duct",
                doc => ResolveElementTypeId<DuctType>(doc, request.TypeId, "typeId"),
                doc => ResolveSystemTypeId<MechanicalSystemType>(doc, request.SystemTypeId, "systemTypeId"),
                (doc, typeId, systemTypeId, levelId, start, end) => Duct.Create(doc, systemTypeId, typeId, levelId, start, end),
                ct);
        }

        public Task<object> CreateConduitAsync(CreateMepCurveRequest request, CancellationToken ct = default)
        {
            return CreateMepCurveAsync(
                request,
                "Conduit",
                doc => ResolveElementTypeId<ConduitType>(doc, request.TypeId, "typeId"),
                doc => ElementId.InvalidElementId,
                (doc, typeId, _, levelId, start, end) => Conduit.Create(doc, typeId, start, end, levelId),
                ct);
        }

        public Task<object> CreateCableTrayAsync(CreateMepCurveRequest request, CancellationToken ct = default)
        {
            return CreateMepCurveAsync(
                request,
                "Cable Tray",
                doc => ResolveElementTypeId<CableTrayType>(doc, request.TypeId, "typeId"),
                doc => ElementId.InvalidElementId,
                (doc, typeId, _, levelId, start, end) => CableTray.Create(doc, typeId, start, end, levelId),
                ct);
        }

        public Task<object> UpdatePipeAsync(UpdateMepCurveRequest request, CancellationToken ct = default)
        {
            return UpdateMepCurveAsync<Pipe>(request, "Pipe", ct);
        }

        public Task<object> UpdateDuctAsync(UpdateMepCurveRequest request, CancellationToken ct = default)
        {
            return UpdateMepCurveAsync<Duct>(request, "Duct", ct);
        }

        public Task<object> UpdateConduitAsync(UpdateMepCurveRequest request, CancellationToken ct = default)
        {
            return UpdateMepCurveAsync<Conduit>(request, "Conduit", ct);
        }

        public Task<object> UpdateCableTrayAsync(UpdateMepCurveRequest request, CancellationToken ct = default)
        {
            return UpdateMepCurveAsync<CableTray>(request, "Cable Tray", ct);
        }

        public Task<object> ConnectMepElementsAsync(ConnectMepElementsRequest request, CancellationToken ct = default)
        {
            return SetConnectorConnectionAsync(request, connect: true, ct);
        }

        public Task<object> DisconnectMepElementsAsync(ConnectMepElementsRequest request, CancellationToken ct = default)
        {
            return SetConnectorConnectionAsync(request, connect: false, ct);
        }

        private Task<object> CreateMepCurveAsync(
            CreateMepCurveRequest request,
            string label,
            Func<Document, ElementId> resolveTypeId,
            Func<Document, ElementId> resolveSystemTypeId,
            Func<Document, ElementId, ElementId, ElementId, XYZ, XYZ, Element> create,
            CancellationToken ct)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var typeId = resolveTypeId(doc);
                var systemTypeId = resolveSystemTypeId(doc);
                var levelId = ResolveRequiredElementId(doc, request.LevelId, "levelId");
                var start = new XYZ(
                    ToInternalFeet(request.StartX, request.Unit),
                    ToInternalFeet(request.StartY, request.Unit),
                    ToInternalFeet(request.StartZ, request.Unit));
                var end = new XYZ(
                    ToInternalFeet(request.EndX, request.Unit),
                    ToInternalFeet(request.EndY, request.Unit),
                    ToInternalFeet(request.EndZ, request.Unit));
                if (start.DistanceTo(end) < 1e-9)
                    throw new ArgumentException($"{label} start and end points must be different.");

                Element element;
                using (var tx = new Transaction(doc, $"RevitMCP: Create {label}"))
                {
                    tx.Start();
                    element = create(doc, typeId, systemTypeId, levelId, start, end);
                    foreach (var (name, value) in request.Parameters)
                        TrySetParameter(element, name, value, "instance");
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    element = BuildElementDetailInfo(element, false, false),
                    elementId = element.Id.ToString(),
                    message = $"{label} created successfully (id={element.Id})."
                });
            }, ct);
        }

        private Task<object> UpdateMepCurveAsync<TElement>(
            UpdateMepCurveRequest request,
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

                    if (request.StartX.HasValue || request.StartY.HasValue || request.StartZ.HasValue ||
                        request.EndX.HasValue || request.EndY.HasValue || request.EndZ.HasValue)
                    {
                        UpdateCurveEndpoints(element, request);
                        changed.Add("curve");
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

        private Task<object> SetConnectorConnectionAsync(
            ConnectMepElementsRequest request,
            bool connect,
            CancellationToken ct)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var firstElement = ResolveElement(doc, request.First.ElementId, "first.elementId");
                var secondElement = ResolveElement(doc, request.Second.ElementId, "second.elementId");
                var first = ResolveConnector(firstElement, request.First, request.Unit);
                var second = ResolveConnector(secondElement, request.Second, request.Unit);

                using (var tx = new Transaction(doc, connect ? "RevitMCP: Connect MEP Elements" : "RevitMCP: Disconnect MEP Elements"))
                {
                    tx.Start();
                    if (connect)
                        first.ConnectTo(second);
                    else if (first.IsConnectedTo(second))
                        first.DisconnectFrom(second);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    firstElementId = firstElement.Id.ToString(),
                    secondElementId = secondElement.Id.ToString(),
                    firstConnector = BuildConnectorInfo(first, request.First.ConnectorIndex ?? -1),
                    secondConnector = BuildConnectorInfo(second, request.Second.ConnectorIndex ?? -1),
                    message = connect ? "Connectors connected successfully." : "Connectors disconnected successfully."
                });
            }, ct);
        }

        private static ElementId ResolveSystemTypeId<TSystemType>(
            Document doc,
            string? rawId,
            string paramName)
            where TSystemType : Element
        {
            if (!string.IsNullOrWhiteSpace(rawId))
            {
                var systemType = doc.GetElement(ResolveRequiredElementId(doc, rawId, paramName)) as TSystemType;
                return systemType?.Id ?? throw new InvalidOperationException(
                    $"{typeof(TSystemType).Name} id '{rawId}' was not found.");
            }

            var defaultType = new FilteredElementCollector(doc)
                .OfClass(typeof(TSystemType))
                .Cast<TSystemType>()
                .FirstOrDefault();
            return defaultType?.Id ?? throw new InvalidOperationException(
                $"No {typeof(TSystemType).Name} was found in the document.");
        }

        private static void UpdateCurveEndpoints(Element element, UpdateMepCurveRequest request)
        {
            if (element.Location is not LocationCurve locationCurve)
                throw new InvalidOperationException($"Element {element.Id} does not have a LocationCurve.");

            var current = locationCurve.Curve;
            var start = current.GetEndPoint(0);
            var end = current.GetEndPoint(1);
            var newStart = new XYZ(
                request.StartX.HasValue ? ToInternalFeet(request.StartX.Value, request.Unit) : start.X,
                request.StartY.HasValue ? ToInternalFeet(request.StartY.Value, request.Unit) : start.Y,
                request.StartZ.HasValue ? ToInternalFeet(request.StartZ.Value, request.Unit) : start.Z);
            var newEnd = new XYZ(
                request.EndX.HasValue ? ToInternalFeet(request.EndX.Value, request.Unit) : end.X,
                request.EndY.HasValue ? ToInternalFeet(request.EndY.Value, request.Unit) : end.Y,
                request.EndZ.HasValue ? ToInternalFeet(request.EndZ.Value, request.Unit) : end.Z);
            if (newStart.DistanceTo(newEnd) < 1e-9)
                throw new ArgumentException("Curve start and end points must be different.");

            locationCurve.Curve = Line.CreateBound(newStart, newEnd);
        }

        private static List<Connector> GetConnectors(Element element)
        {
            ConnectorManager? manager = element switch
            {
                MEPCurve curve => curve.ConnectorManager,
                FamilyInstance fi => fi.MEPModel?.ConnectorManager,
                _ => null
            };

            if (manager == null)
                return new List<Connector>();

            return manager.Connectors.Cast<Connector>().ToList();
        }

        private static Connector ResolveConnector(
            Element element,
            ConnectorRefRequest reference,
            string unit)
        {
            var connectors = GetConnectors(element);
            if (connectors.Count == 0)
                throw new InvalidOperationException($"Element {element.Id} has no connectors.");

            if (reference.ConnectorIndex.HasValue)
            {
                var index = reference.ConnectorIndex.Value;
                if (index < 0 || index >= connectors.Count)
                    throw new ArgumentException($"connectorIndex {index} is out of range for element {element.Id}.");
                return connectors[index];
            }

            if (reference.X.HasValue && reference.Y.HasValue && reference.Z.HasValue)
            {
                var target = new XYZ(
                    ToInternalFeet(reference.X.Value, unit),
                    ToInternalFeet(reference.Y.Value, unit),
                    ToInternalFeet(reference.Z.Value, unit));
                return connectors
                    .OrderBy(c => SafeConnectorOrigin(c).DistanceTo(target))
                    .First();
            }

            return connectors[0];
        }

        private static XYZ SafeConnectorOrigin(Connector connector)
        {
            try { return connector.Origin; }
            catch { return XYZ.Zero; }
        }

        private static object BuildConnectorInfo(Connector connector, int index)
        {
            return new
            {
                index,
                ownerId = connector.Owner?.Id.ToString(),
                origin = BuildPointInfo(SafeConnectorOrigin(connector)),
                domain = connector.Domain.ToString(),
                connectorType = connector.ConnectorType.ToString(),
                shape = connector.Shape.ToString(),
                isConnected = connector.IsConnected,
                radius = SafeConnectorDouble(() => connector.Radius),
                width = SafeConnectorDouble(() => connector.Width),
                height = SafeConnectorDouble(() => connector.Height)
            };
        }

        private static double? SafeConnectorDouble(Func<double> getValue)
        {
            try { return getValue(); }
            catch { return null; }
        }
    }
}
