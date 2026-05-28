using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;

namespace RevitMcpAddin.Services
{
    public sealed partial class RevitService
    {
        private const int DefaultRebarLimit = 500;
        private const int HardRebarLimit = 2_000;

        public Task<object> GetRebarsAsync(RebarListRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var maxItems = NormalizeRebarLimit(request.MaxItems);
                var hostId = string.IsNullOrWhiteSpace(request.HostId)
                    ? ElementId.InvalidElementId
                    : ResolveRequiredElementId(doc, request.HostId, "hostId");

                var rebars = new FilteredElementCollector(doc)
                    .OfClass(typeof(Rebar))
                    .Cast<Rebar>()
                    .Where(r => hostId == ElementId.InvalidElementId || SafeElementId(() => r.GetHostId()) == hostId)
                    .Take(maxItems)
                    .Select(r => BuildRebarInfo(r, request.IncludeParameters))
                    .ToList();

                return Task.FromResult<object>(new
                {
                    success = true,
                    count = rebars.Count,
                    maxItems,
                    hostId = hostId == ElementId.InvalidElementId ? null : hostId.ToString(),
                    rebars,
                    message = $"Found {rebars.Count} rebar element(s)."
                });
            }, ct, timeoutMs: 120_000);
        }

        public Task<object> GetRebarHostCandidatesAsync(RebarHostCandidatesRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var maxItems = NormalizeRebarLimit(request.MaxItems, hardMax: 1_000);
                var collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();
                if (!string.IsNullOrWhiteSpace(request.Category))
                {
                    if (!TryResolveCategory(request.Category, out var bic))
                        throw new ArgumentException($"Unknown or unsupported category: '{request.Category}'.");
                    collector = collector.OfCategory(bic);
                }

                var hosts = new List<object>();
                foreach (var element in collector.Cast<Element>())
                {
                    if (!IsValidRebarHost(element))
                        continue;

                    var hostData = RebarHostData.GetRebarHostData(element);
                    var cover = Safe(() => hostData?.GetCommonCoverType(), null);
                    hosts.Add(new
                    {
                        element = BuildElementDetailInfo(element, includeParameters: false, includeTypeParameters: false),
                        commonCoverType = cover == null ? null : BuildTypeLikeInfo(cover)
                    });

                    if (hosts.Count >= maxItems)
                        break;
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    count = hosts.Count,
                    maxItems,
                    hosts,
                    message = $"Found {hosts.Count} rebar host candidate(s)."
                });
            }, ct, timeoutMs: 120_000);
        }

        public Task<object> GetRebarBarTypesAsync(RebarTypeListRequest request, CancellationToken ct = default)
            => ListRebarTypeElementsAsync<RebarBarType>(request, "barTypes", ct);

        public Task<object> GetRebarShapesAsync(RebarTypeListRequest request, CancellationToken ct = default)
            => ListRebarTypeElementsAsync<RebarShape>(request, "shapes", ct);

        public Task<object> GetRebarHookTypesAsync(RebarTypeListRequest request, CancellationToken ct = default)
            => ListRebarTypeElementsAsync<RebarHookType>(request, "hookTypes", ct);

        public Task<object> GetRebarCoverTypesAsync(RebarTypeListRequest request, CancellationToken ct = default)
            => ListRebarTypeElementsAsync<RebarCoverType>(request, "coverTypes", ct, e => new
            {
                id = e.Id.ToString(),
                name = e.Name ?? string.Empty,
                className = e.GetType().Name,
                category = e.Category?.Name ?? string.Empty,
                coverDistance = e.CoverDistance
            });

        public Task<object> GetRebarConstraintsAsync(RebarElementRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var rebar = ResolveRebar(doc, request.ElementId, "elementId");
                var accessor = Safe(() => rebar.GetShapeDrivenAccessor(), null);
                var manager = Safe(() => rebar.GetRebarConstraintsManager(), null);
                var handles = Safe(() => manager?.GetAllHandles(), null);
                var constrainedHandles = Safe(() => manager?.GetAllConstrainedHandles(), null);

                return Task.FromResult<object>(new
                {
                    success = true,
                    rebar = BuildRebarInfo(rebar, request.IncludeParameters),
                    shapeDriven = accessor == null ? null : BuildShapeDrivenAccessorInfo(accessor),
                    constraintsManager = manager == null ? null : new
                    {
                        isValidObject = manager.IsValidObject,
                        hasValidRebar = Safe(() => manager.HasValidRebar(), false),
                        allHandleCount = handles?.Count ?? 0,
                        constrainedHandleCount = constrainedHandles?.Count ?? 0,
                        isConstrainedPlacementEnabled = RebarConstraintsManager.IsRebarConstrainedPlacementEnabled
                    },
                    message = $"Read rebar constraints summary for {rebar.Id}."
                });
            }, ct);
        }

        public Task<object> GetRebarCenterlineCurvesAsync(RebarElementRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var rebar = ResolveRebar(doc, request.ElementId, "elementId");
                var curves = rebar
                    .GetCenterlineCurves(
                        false,
                        false,
                        false,
                        MultiplanarOption.IncludeAllMultiplanarCurves,
                        0)
                    .Select(BuildCurveInfo)
                    .ToList();

                return Task.FromResult<object>(new
                {
                    success = true,
                    rebarId = rebar.Id.ToString(),
                    curveCount = curves.Count,
                    curves,
                    message = $"Read {curves.Count} centerline curve(s) for rebar {rebar.Id}."
                });
            }, ct);
        }

        public Task<object> CreateRebarFromCurvesAsync(CreateRebarFromCurvesRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                Rebar rebar;

                using (var tx = new Transaction(doc, "RevitMCP: Create Rebar From Curves"))
                {
                    tx.Start();
                    rebar = CreateRebarFromCurvesCore(doc, request);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    rebar = BuildRebarInfo(rebar, includeParameters: false),
                    elementId = rebar.Id.ToString(),
                    message = $"Rebar {rebar.Id} created from {request.Curves.Count} curve(s)."
                });
            }, ct);
        }

        public Task<object> CreateRebarFromShapeAsync(CreateRebarFromShapeRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var host = ResolveValidRebarHost(doc, request.HostId, "hostId");
                var shape = ResolveElement(doc, request.ShapeId, "shapeId") as RebarShape
                    ?? throw new InvalidOperationException($"RebarShape id '{request.ShapeId}' was not found.");
                var barType = ResolveRebarTypeOrDefault<RebarBarType>(doc, request.BarTypeId, "barTypeId");
                var origin = ToXyz(request.Origin, request.Unit);
                var xVector = NormalizeVector(ToRawXyz(request.XVector), "xVector");
                var yVector = NormalizeVector(ToRawXyz(request.YVector), "yVector");
                Rebar rebar;

                using (var tx = new Transaction(doc, "RevitMCP: Create Rebar From Shape"))
                {
                    tx.Start();
                    rebar = Rebar.CreateFromRebarShape(doc, shape, barType, host, origin, xVector, yVector);
                    ApplyInstanceParameters(rebar, request.Parameters);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    rebar = BuildRebarInfo(rebar, includeParameters: false),
                    elementId = rebar.Id.ToString(),
                    message = $"Rebar {rebar.Id} created from shape '{shape.Name}'."
                });
            }, ct);
        }

        public Task<object> UpdateRebarLayoutAsync(RebarLayoutRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var rebar = ResolveRebar(doc, request.RebarId, "rebarId");

                using (var tx = new Transaction(doc, "RevitMCP: Update Rebar Layout"))
                {
                    tx.Start();
                    ApplyLayoutToRebar(rebar, request);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    rebar = BuildRebarInfo(rebar, includeParameters: false),
                    message = $"Rebar {rebar.Id} layout updated."
                });
            }, ct);
        }

        public Task<object> UpdateRebarHooksAsync(RebarHooksRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var rebar = ResolveRebar(doc, request.RebarId, "rebarId");
                var changed = new List<string>();

                using (var tx = new Transaction(doc, "RevitMCP: Update Rebar Hooks"))
                {
                    tx.Start();
                    if (!string.IsNullOrWhiteSpace(request.StartHookTypeId))
                    {
                        rebar.SetHookTypeId(0, ResolveRequiredElementId(doc, request.StartHookTypeId, "startHookTypeId"));
                        changed.Add("startHookTypeId");
                    }
                    if (!string.IsNullOrWhiteSpace(request.EndHookTypeId))
                    {
                        rebar.SetHookTypeId(1, ResolveRequiredElementId(doc, request.EndHookTypeId, "endHookTypeId"));
                        changed.Add("endHookTypeId");
                    }
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    rebar = BuildRebarInfo(rebar, includeParameters: false),
                    changed,
                    message = $"Rebar {rebar.Id} hook settings updated."
                });
            }, ct);
        }

        public Task<object> UpdateRebarConstraintsAsync(RebarConstraintsRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var rebar = ResolveRebar(doc, request.RebarId, "rebarId");
                var changed = new List<string>();

                using (var tx = new Transaction(doc, "RevitMCP: Update Rebar Constraints"))
                {
                    tx.Start();
                    if (request.UseRebarConstraintsToProduceVaryingBars.HasValue)
                    {
                        var accessor = rebar.GetShapeDrivenAccessor();
                        accessor.UseRebarConstraintsToProduceVaryingBars =
                            request.UseRebarConstraintsToProduceVaryingBars.Value;
                        changed.Add("useRebarConstraintsToProduceVaryingBars");
                    }
                    var manager = Safe(() => rebar.GetRebarConstraintsManager(), null);
                    RecomputeConstraintsIfAvailable(manager);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    changed,
                    constraints = BuildRebarConstraintSummary(rebar),
                    message = changed.Count == 0
                        ? $"Rebar {rebar.Id} constraints were recomputed/read; no explicit setting was changed."
                        : $"Rebar {rebar.Id} constraints updated."
                });
            }, ct);
        }

        public Task<object> SetRebarCoverAsync(RebarCoverRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var host = ResolveValidRebarHost(doc, request.HostId, "hostId");
                var coverType = ResolveRebarTypeOrDefault<RebarCoverType>(doc, request.CoverTypeId, "coverTypeId");

                using (var tx = new Transaction(doc, "RevitMCP: Set Rebar Cover"))
                {
                    tx.Start();
                    var hostData = RebarHostData.GetRebarHostData(host)
                        ?? throw new InvalidOperationException($"Element {host.Id} has no RebarHostData.");
                    hostData.SetCommonCoverType(coverType);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    host = BuildElementDetailInfo(host, includeParameters: false, includeTypeParameters: false),
                    coverType = BuildTypeLikeInfo(coverType),
                    message = $"Common rebar cover set on host {host.Id}."
                });
            }, ct);
        }

        public Task<object> SetRebarVisibilityInViewAsync(RebarVisibilityRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var element = ResolveElement(doc, request.ElementId, "elementId");
                var view = string.IsNullOrWhiteSpace(request.ViewId)
                    ? uiApp.ActiveUIDocument?.ActiveView ?? throw new InvalidOperationException("No active view.")
                    : ResolveView(doc, request.ViewId);
                var changed = new List<string>();

                using (var tx = new Transaction(doc, "RevitMCP: Set Rebar Visibility"))
                {
                    tx.Start();
                    if (request.Unobscured.HasValue)
                    {
                        if (element is Rebar rebar)
                            rebar.SetUnobscuredInView(view, request.Unobscured.Value);
                        else if (element is RebarCoupler coupler)
                            coupler.SetUnobscuredInView(view, request.Unobscured.Value);
                        else if (element is AreaReinforcement area)
                            area.SetUnobscuredInView(view, request.Unobscured.Value);
                        else
                            throw new InvalidOperationException($"Element {element.Id} is not a supported reinforcement visibility element.");
                        changed.Add("unobscured");
                    }

                    if (!string.IsNullOrWhiteSpace(request.PresentationMode))
                    {
                        var rebar = element as Rebar
                            ?? throw new InvalidOperationException("'presentationMode' is only supported for Rebar.");
                        rebar.SetPresentationMode(view, ParseEnum<RebarPresentationMode>(request.PresentationMode, "presentationMode"));
                        changed.Add("presentationMode");
                    }

                    if (request.BarIndex.HasValue && request.Hidden.HasValue)
                    {
                        var rebar = element as Rebar
                            ?? throw new InvalidOperationException("'barIndex'/'hidden' is only supported for Rebar.");
                        rebar.SetBarHiddenStatus(view, request.BarIndex.Value, request.Hidden.Value);
                        changed.Add("barHiddenStatus");
                    }

                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    elementId = element.Id.ToString(),
                    viewId = view.Id.ToString(),
                    changed,
                    message = $"Visibility updated for element {element.Id} in view '{view.Name}'."
                });
            }, ct);
        }

        public Task<object> DeleteRebarsAsync(RebarIdsRequest request, CancellationToken ct = default)
            => DeleteReinforcementElementsAsync(request, "Rebar", e => e is Rebar, ct);

        public Task<object> CreateAreaReinforcementAsync(RebarSystemRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var host = ResolveValidRebarHost(doc, request.HostId, "hostId");
                var typeId = ResolveRebarTypeIdOrDefault<AreaReinforcementType>(doc, request.TypeId, "typeId");
                var barTypeId = ResolveRebarTypeIdOrDefault<RebarBarType>(doc, request.BarTypeId, "barTypeId");
                var hookId = ResolveOptionalRebarTypeId<RebarHookType>(doc, request.HookTypeId, "hookTypeId");
                var curves = ResolveRebarSystemCurves(host, request);
                var direction = NormalizeVector(new XYZ(request.DirectionX, request.DirectionY, request.DirectionZ), "direction");
                AreaReinforcement area;

                using (var tx = new Transaction(doc, "RevitMCP: Create Area Reinforcement"))
                {
                    tx.Start();
                    area = AreaReinforcement.Create(doc, host, curves, direction, typeId, barTypeId, hookId);
                    ApplyInstanceParameters(area, request.Parameters);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    element = BuildReinforcementElementInfo(area, includeParameters: false),
                    elementId = area.Id.ToString(),
                    message = $"Area reinforcement {area.Id} created."
                });
            }, ct);
        }

        public Task<object> UpdateAreaReinforcementAsync(UpdateRebarSystemRequest request, CancellationToken ct = default)
            => UpdateReinforcementElementAsync<AreaReinforcement>(request, "Area Reinforcement", ct);

        public Task<object> CreatePathReinforcementAsync(RebarSystemRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var host = ResolveValidRebarHost(doc, request.HostId, "hostId");
                var typeId = ResolveRebarTypeIdOrDefault<PathReinforcementType>(doc, request.TypeId, "typeId");
                var barTypeId = ResolveRebarTypeIdOrDefault<RebarBarType>(doc, request.BarTypeId, "barTypeId");
                var startHookId = ResolveOptionalRebarTypeId<RebarHookType>(doc, request.StartHookTypeId ?? request.HookTypeId, "startHookTypeId");
                var endHookId = ResolveOptionalRebarTypeId<RebarHookType>(doc, request.EndHookTypeId ?? request.HookTypeId, "endHookTypeId");
                var curves = request.Curves.Count > 0
                    ? BuildCurveList(request.Curves, request.Unit)
                    : BuildDefaultPathCurves(host);
                PathReinforcement path;

                using (var tx = new Transaction(doc, "RevitMCP: Create Path Reinforcement"))
                {
                    tx.Start();
                    path = PathReinforcement.Create(doc, host, curves, request.Flip, typeId, barTypeId, startHookId, endHookId);
                    ApplyInstanceParameters(path, request.Parameters);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    element = BuildReinforcementElementInfo(path, includeParameters: false),
                    elementId = path.Id.ToString(),
                    message = $"Path reinforcement {path.Id} created."
                });
            }, ct);
        }

        public Task<object> UpdatePathReinforcementAsync(UpdateRebarSystemRequest request, CancellationToken ct = default)
            => UpdateReinforcementElementAsync<PathReinforcement>(request, "Path Reinforcement", ct);

        public Task<object> CreateFabricAreaAsync(RebarSystemRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var host = ResolveValidRebarHost(doc, request.HostId, "hostId");
                var fabricAreaTypeId = ResolveRebarTypeIdOrDefault<FabricAreaType>(doc, request.TypeId, "typeId");
                var fabricSheetTypeId = ResolveRebarTypeIdOrDefault<FabricSheetType>(
                    doc,
                    request.FabricSheetTypeId ?? request.BarTypeId,
                    "fabricSheetTypeId");
                var direction = NormalizeVector(new XYZ(request.DirectionX, request.DirectionY, request.DirectionZ), "direction");
                FabricArea area;

                using (var tx = new Transaction(doc, "RevitMCP: Create Fabric Area"))
                {
                    tx.Start();
                    if (request.Boundary.Count >= 3)
                    {
                        var loops = new List<CurveLoop> { BuildClosedCurveLoop(request.Boundary, request.Unit) };
                        area = FabricArea.Create(doc, host, loops, direction, ToHostBoxOrigin(host), fabricAreaTypeId, fabricSheetTypeId);
                    }
                    else
                    {
                        area = FabricArea.Create(doc, host, direction, fabricAreaTypeId, fabricSheetTypeId);
                    }
                    ApplyInstanceParameters(area, request.Parameters);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    element = BuildReinforcementElementInfo(area, includeParameters: false),
                    elementId = area.Id.ToString(),
                    message = $"Fabric area {area.Id} created."
                });
            }, ct);
        }

        public Task<object> UpdateFabricAreaAsync(UpdateRebarSystemRequest request, CancellationToken ct = default)
            => UpdateReinforcementElementAsync<FabricArea>(request, "Fabric Area", ct);

        public Task<object> PlaceFabricSheetAsync(RebarSystemRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var host = ResolveValidRebarHost(doc, request.HostId, "hostId");
                var sheetTypeId = ResolveRebarTypeIdOrDefault<FabricSheetType>(
                    doc,
                    request.FabricSheetTypeId ?? request.TypeId,
                    "fabricSheetTypeId");
                FabricSheet sheet;

                using (var tx = new Transaction(doc, "RevitMCP: Place Fabric Sheet"))
                {
                    tx.Start();
                    sheet = request.Boundary.Count >= 2
                        ? FabricSheet.Create(doc, host.Id, sheetTypeId, BuildOpenCurveLoop(request.Boundary, request.Unit))
                        : FabricSheet.Create(doc, host, sheetTypeId);
                    ApplyInstanceParameters(sheet, request.Parameters);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    element = BuildReinforcementElementInfo(sheet, includeParameters: false),
                    elementId = sheet.Id.ToString(),
                    message = $"Fabric sheet {sheet.Id} placed."
                });
            }, ct);
        }

        public Task<object> UpdateFabricSheetAsync(UpdateRebarSystemRequest request, CancellationToken ct = default)
            => UpdateReinforcementElementAsync<FabricSheet>(request, "Fabric Sheet", ct);

        public Task<object> GetRebarCouplerTypesAsync(RebarTypeListRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var maxItems = NormalizeRebarLimit(request.MaxItems);
                var types = new FilteredElementCollector(doc)
                    .WhereElementIsElementType()
                    .Cast<Element>()
                    .OfType<ElementType>()
                    .Where(LooksLikeCouplerElement)
                    .Where(e => MatchesNameFilter(e, request.NameContains))
                    .OrderBy(e => e.Category?.Name ?? string.Empty)
                    .ThenBy(e => e.Name)
                    .Take(maxItems)
                    .Select(BuildTypeLikeInfo)
                    .ToList();

                return Task.FromResult<object>(new
                {
                    success = true,
                    count = types.Count,
                    maxItems,
                    couplerTypes = types,
                    message = $"Found {types.Count} coupler type candidate(s)."
                });
            }, ct, timeoutMs: 120_000);
        }

        public Task<object> GetRebarCouplersAsync(RebarListRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var maxItems = NormalizeRebarLimit(request.MaxItems);
                var couplers = new FilteredElementCollector(doc)
                    .OfClass(typeof(RebarCoupler))
                    .Cast<RebarCoupler>()
                    .Take(maxItems)
                    .Select(c => BuildCouplerInfo(c, request.IncludeParameters))
                    .ToList();

                return Task.FromResult<object>(new
                {
                    success = true,
                    count = couplers.Count,
                    maxItems,
                    couplers,
                    message = $"Found {couplers.Count} rebar coupler(s)."
                });
            }, ct, timeoutMs: 120_000);
        }

        public Task<object> GetRebarCouplerAsync(RebarElementRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var coupler = ResolveElement(doc, request.ElementId, "elementId") as RebarCoupler
                    ?? throw new InvalidOperationException($"RebarCoupler id '{request.ElementId}' was not found.");

                return Task.FromResult<object>(new
                {
                    success = true,
                    coupler = BuildCouplerInfo(coupler, request.IncludeParameters),
                    message = $"Read rebar coupler {coupler.Id}."
                });
            }, ct);
        }

        public Task<object> CreateRebarCouplerAsync(CreateRebarCouplerRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var couplerTypeId = ResolveCouplerTypeId(doc, request.CouplerTypeId);
                var firstRebar = ResolveRebar(doc, request.FirstRebarId, "firstRebarId");
                var firstData = RebarReinforcementData.Create(firstRebar.Id, request.FirstEnd)
                    ?? throw new InvalidOperationException("Could not create reinforcement data for first rebar.");
                ReinforcementData? secondData = null;
                if (!string.IsNullOrWhiteSpace(request.SecondRebarId))
                {
                    var secondRebar = ResolveRebar(doc, request.SecondRebarId, "secondRebarId");
                    secondData = RebarReinforcementData.Create(secondRebar.Id, request.SecondEnd)
                        ?? throw new InvalidOperationException("Could not create reinforcement data for second rebar.");
                }
                RebarCoupler? coupler;
                RebarCouplerError error;

                using (var tx = new Transaction(doc, "RevitMCP: Create Rebar Coupler"))
                {
                    tx.Start();
                    coupler = RebarCoupler.Create(doc, couplerTypeId, firstData, secondData, out error);
                    if (coupler != null)
                        ApplyInstanceParameters(coupler, request.Parameters);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = coupler != null,
                    error = error.ToString(),
                    coupler = coupler == null ? null : BuildCouplerInfo(coupler, includeParameters: false),
                    elementId = coupler?.Id.ToString(),
                    message = coupler == null
                        ? $"Coupler creation failed with error '{error}'."
                        : $"Rebar coupler {coupler.Id} created."
                });
            }, ct);
        }

        public Task<object> UpdateRebarCouplerAsync(UpdateRebarCouplerRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var coupler = ResolveElement(doc, request.CouplerId, "couplerId") as RebarCoupler
                    ?? throw new InvalidOperationException($"RebarCoupler id '{request.CouplerId}' was not found.");
                var changed = new List<string>();
                var parameterResults = new List<object>();

                using (var tx = new Transaction(doc, "RevitMCP: Update Rebar Coupler"))
                {
                    tx.Start();
                    if (!string.IsNullOrWhiteSpace(request.CouplerMark))
                    {
                        coupler.CouplerMark = request.CouplerMark.Trim();
                        changed.Add("couplerMark");
                    }
                    if (request.RotationAngleDegrees.HasValue)
                    {
                        coupler.RotationAngle = request.RotationAngleDegrees.Value * Math.PI / 180.0;
                        changed.Add("rotationAngleDegrees");
                    }
                    foreach (var (name, value) in request.Parameters)
                        parameterResults.Add(TrySetParameter(coupler, name, value, "instance"));
                    if (request.Parameters.Count > 0)
                        changed.Add("parameters");
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    coupler = BuildCouplerInfo(coupler, includeParameters: false),
                    changed,
                    parameterResults,
                    message = $"Rebar coupler {coupler.Id} updated."
                });
            }, ct);
        }

        public Task<object> ChangeRebarCouplerTypeAsync(ChangeRebarCouplerTypeRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var coupler = ResolveElement(doc, request.CouplerId, "couplerId") as RebarCoupler
                    ?? throw new InvalidOperationException($"RebarCoupler id '{request.CouplerId}' was not found.");
                var couplerTypeId = ResolveCouplerTypeId(doc, request.CouplerTypeId);

                using (var tx = new Transaction(doc, "RevitMCP: Change Rebar Coupler Type"))
                {
                    tx.Start();
                    coupler.ChangeTypeId(couplerTypeId);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    coupler = BuildCouplerInfo(coupler, includeParameters: false),
                    typeId = couplerTypeId.ToString(),
                    message = $"Rebar coupler {coupler.Id} type changed."
                });
            }, ct);
        }

        public Task<object> DeleteRebarCouplersAsync(RebarIdsRequest request, CancellationToken ct = default)
            => DeleteReinforcementElementsAsync(request, "Rebar Coupler", e => e is RebarCoupler, ct);

        public Task<object> GetRebarEndTreatmentsAsync(RebarTypeListRequest request, CancellationToken ct = default)
            => ListRebarTypeElementsAsync<EndTreatmentType>(request, "endTreatments", ct);

        public Task<object> SetRebarEndTreatmentAsync(EndTreatmentRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var rebar = ResolveRebar(doc, request.RebarId, "rebarId");
                var endTreatmentTypeId = ResolveRequiredElementId(doc, request.EndTreatmentTypeId, "endTreatmentTypeId");

                using (var tx = new Transaction(doc, "RevitMCP: Set Rebar End Treatment"))
                {
                    tx.Start();
                    rebar.SetEndTreatmentTypeId(request.End, endTreatmentTypeId);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    rebar = BuildRebarInfo(rebar, includeParameters: false),
                    end = request.End,
                    endTreatmentTypeId = endTreatmentTypeId.ToString(),
                    message = $"End treatment set for rebar {rebar.Id}, end {request.End}."
                });
            }, ct);
        }

        public Task<object> ValidateRebarCouplerPlacementAsync(CreateRebarCouplerRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var issues = new List<string>();
                var couplerTypeId = ElementId.InvalidElementId;

                try { couplerTypeId = ResolveCouplerTypeId(doc, request.CouplerTypeId); }
                catch (Exception ex) { issues.Add(ex.Message); }

                Rebar? first = null;
                Rebar? second = null;
                try { first = ResolveRebar(doc, request.FirstRebarId, "firstRebarId"); }
                catch (Exception ex) { issues.Add(ex.Message); }
                if (!string.IsNullOrWhiteSpace(request.SecondRebarId))
                {
                    try { second = ResolveRebar(doc, request.SecondRebarId, "secondRebarId"); }
                    catch (Exception ex) { issues.Add(ex.Message); }
                }
                if (request.FirstEnd is < 0 or > 1)
                    issues.Add("'firstEnd' must be 0 or 1.");
                if (request.SecondEnd is < 0 or > 1)
                    issues.Add("'secondEnd' must be 0 or 1.");

                return Task.FromResult<object>(new
                {
                    success = issues.Count == 0,
                    canAttemptCreate = issues.Count == 0,
                    couplerTypeId = couplerTypeId == ElementId.InvalidElementId ? null : couplerTypeId.ToString(),
                    firstRebar = first == null ? null : BuildRebarInfo(first, includeParameters: false),
                    secondRebar = second == null ? null : BuildRebarInfo(second, includeParameters: false),
                    issues,
                    message = issues.Count == 0
                        ? "Coupler request is syntactically valid and can be attempted in a transaction."
                        : $"Coupler request has {issues.Count} issue(s)."
                });
            }, ct);
        }

        public Task<object> CreateRebarTagAsync(RebarTagRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var view = string.IsNullOrWhiteSpace(request.ViewId)
                    ? uiApp.ActiveUIDocument?.ActiveView ?? throw new InvalidOperationException("No active view.")
                    : ResolveView(doc, request.ViewId);
                var element = ResolveElement(doc, request.ElementId, "elementId");
                var point = new XYZ(
                    ToInternalFeet(request.X, request.Unit),
                    ToInternalFeet(request.Y, request.Unit),
                    ToInternalFeet(request.Z, request.Unit));
                IndependentTag tag;

                using (var tx = new Transaction(doc, "RevitMCP: Create Rebar Tag"))
                {
                    tx.Start();
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
                    tag = BuildElementDetailInfo(tag, includeParameters: false, includeTypeParameters: false),
                    elementId = tag.Id.ToString(),
                    taggedElementId = element.Id.ToString(),
                    message = $"Rebar tag {tag.Id} created."
                });
            }, ct);
        }

        public Task<object> CreateMultiRebarAnnotationAsync(MultiRebarAnnotationRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                if (request.ElementIds.Count == 0)
                    throw new ArgumentException("'elementIds' must contain at least one rebar id.");
                var view = string.IsNullOrWhiteSpace(request.ViewId)
                    ? uiApp.ActiveUIDocument?.ActiveView ?? throw new InvalidOperationException("No active view.")
                    : ResolveView(doc, request.ViewId);
                var elementIds = request.ElementIds
                    .Select(raw => ResolveRebar(doc, raw, "elementIds").Id)
                    .ToList();
                MultiReferenceAnnotation annotation;
                MultiReferenceAnnotationType annotationType;

                using (var tx = new Transaction(doc, "RevitMCP: Create Multi Rebar Annotation"))
                {
                    tx.Start();
                    annotationType = ResolveMultiReferenceAnnotationType(doc, request.TypeId);
                    var options = new MultiReferenceAnnotationOptions(annotationType)
                    {
                        TagHasLeader = request.AddLeader,
                        TagHeadPosition = new XYZ(
                            ToInternalFeet(request.TagHeadX, request.Unit),
                            ToInternalFeet(request.TagHeadY, request.Unit),
                            ToInternalFeet(request.TagHeadZ, request.Unit)),
                        DimensionLineOrigin = new XYZ(
                            ToInternalFeet(request.DimensionOriginX, request.Unit),
                            ToInternalFeet(request.DimensionOriginY, request.Unit),
                            ToInternalFeet(request.DimensionOriginZ, request.Unit)),
                        DimensionLineDirection = NormalizeVector(
                            new XYZ(request.DimensionDirectionX, request.DimensionDirectionY, request.DimensionDirectionZ),
                            "dimensionLineDirection"),
                        DimensionPlaneNormal = NormalizeVector(
                            new XYZ(request.DimensionPlaneNormalX, request.DimensionPlaneNormalY, request.DimensionPlaneNormalZ),
                            "dimensionPlaneNormal"),
                        DimensionStyleType = DimensionStyleType.Linear
                    };
                    options.SetElementsToDimension(elementIds);
                    annotation = MultiReferenceAnnotation.Create(doc, view.Id, options);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    annotation = BuildElementDetailInfo(annotation, includeParameters: false, includeTypeParameters: false),
                    elementId = annotation.Id.ToString(),
                    typeId = annotationType.Id.ToString(),
                    rebarCount = elementIds.Count,
                    message = $"Multi rebar annotation {annotation.Id} created."
                });
            }, ct);
        }

        public Task<object> CreateRebarScheduleAsync(RebarScheduleRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                ViewSchedule schedule;

                using (var tx = new Transaction(doc, "RevitMCP: Create Rebar Schedule"))
                {
                    tx.Start();
                    var categoryId = ResolveScheduleCategoryId(doc, null, "Rebar");
                    schedule = ViewSchedule.CreateSchedule(doc, categoryId);
                    schedule.Name = string.IsNullOrWhiteSpace(request.Name)
                        ? "Rebar Schedule"
                        : request.Name.Trim();
                    AddScheduleFields(doc, schedule, request.FieldNames);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    schedule = BuildScheduleInfo(schedule),
                    elementId = schedule.Id.ToString(),
                    message = $"Rebar schedule '{schedule.Name}' created."
                });
            }, ct);
        }

        public Task<object> GetRebarQuantitiesAsync(RebarQuantityRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var maxItems = NormalizeRebarLimit(request.MaxItems, hardMax: 5_000);
                var hostId = string.IsNullOrWhiteSpace(request.HostId)
                    ? ElementId.InvalidElementId
                    : ResolveRequiredElementId(doc, request.HostId, "hostId");

                var rebarRows = new FilteredElementCollector(doc)
                    .OfClass(typeof(Rebar))
                    .Cast<Rebar>()
                    .Where(r => hostId == ElementId.InvalidElementId || SafeElementId(() => r.GetHostId()) == hostId)
                    .Take(maxItems)
                    .Select(BuildRebarQuantityInfo)
                    .ToList();

                var couplerRows = request.IncludeCouplers
                    ? new FilteredElementCollector(doc)
                        .OfClass(typeof(RebarCoupler))
                        .Cast<RebarCoupler>()
                        .Take(maxItems)
                        .Select(BuildCouplerQuantityInfo)
                        .ToList()
                    : new List<object>();

                var totalBars = rebarRows.Sum(row => (int)row.GetType().GetProperty("quantity")!.GetValue(row)!);
                var totalCouplers = couplerRows.Sum(row => (int)row.GetType().GetProperty("quantity")!.GetValue(row)!);

                return Task.FromResult<object>(new
                {
                    success = true,
                    hostId = hostId == ElementId.InvalidElementId ? null : hostId.ToString(),
                    rebarCount = rebarRows.Count,
                    couplerCount = couplerRows.Count,
                    totalBars,
                    totalCouplers,
                    rebars = rebarRows,
                    couplers = couplerRows,
                    message = $"Quantified {rebarRows.Count} rebar set(s) and {couplerRows.Count} coupler set(s)."
                });
            }, ct, timeoutMs: 120_000);
        }

        public Task<object> SetRebarPartitionAsync(SetRebarPartitionRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                if (request.ElementIds.Count == 0)
                    throw new ArgumentException("'elementIds' must contain at least one id.");
                if (string.IsNullOrWhiteSpace(request.Partition))
                    throw new ArgumentException("'partition' is required.");
                var maxItems = NormalizeRebarLimit(request.MaxItems);
                if (request.ElementIds.Count > maxItems)
                    throw new ArgumentException($"Too many elementIds ({request.ElementIds.Count}); maxItems is {maxItems}.");
                var results = new List<object>();

                using (var tx = new Transaction(doc, "RevitMCP: Set Rebar Partition"))
                {
                    tx.Start();
                    foreach (var rawId in request.ElementIds)
                    {
                        var element = ResolveElement(doc, rawId, "elementIds");
                        if (element is not Rebar && element is not RebarCoupler)
                            throw new InvalidOperationException($"Element {rawId} is not a rebar or rebar coupler.");
                        var result = TrySetParameter(element, "Partition", JToken.FromObject(request.Partition.Trim()), "instance");
                        results.Add(result);
                    }
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                var successCount = results.Count(r => (bool)r.GetType().GetProperty("Success")!.GetValue(r)!);
                return Task.FromResult<object>(new
                {
                    success = successCount == results.Count,
                    requestedCount = request.ElementIds.Count,
                    successCount,
                    results,
                    message = $"Set partition on {successCount} of {request.ElementIds.Count} reinforcement element(s)."
                });
            }, ct);
        }

        public Task<object> CreateColumnVerticalRebarsAsync(RebarWorkflowRequest request, CancellationToken ct = default)
            => CreateHostBoxRebarWorkflowAsync(request, "Column Vertical Rebars", BuildColumnVerticalBars, ct);

        public Task<object> CreateColumnTiesAsync(RebarWorkflowRequest request, CancellationToken ct = default)
            => CreateHostBoxRebarWorkflowAsync(request, "Column Ties", BuildColumnTieSets, ct);

        public Task<object> CreateBeamLongitudinalRebarsAsync(RebarWorkflowRequest request, CancellationToken ct = default)
            => CreateHostBoxRebarWorkflowAsync(request, "Beam Longitudinal Rebars", BuildBeamLongitudinalBars, ct);

        public Task<object> CreateBeamStirrupsAsync(RebarWorkflowRequest request, CancellationToken ct = default)
            => CreateHostBoxRebarWorkflowAsync(request, "Beam Stirrups", BuildBeamStirrupSets, ct);

        public Task<object> CreateWallRebarGridAsync(RebarWorkflowRequest request, CancellationToken ct = default)
            => CreateHostBoxRebarWorkflowAsync(request, "Wall Rebar Grid", BuildWallGridBars, ct);

        public Task<object> CreateSlabRebarGridAsync(RebarWorkflowRequest request, CancellationToken ct = default)
            => CreateHostBoxRebarWorkflowAsync(request, "Slab Rebar Grid", BuildSlabGridBars, ct);

        private Task<object> ListRebarTypeElementsAsync<TElement>(
            RebarTypeListRequest request,
            string resultName,
            CancellationToken ct,
            Func<TElement, object>? projector = null)
            where TElement : Element
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var maxItems = NormalizeRebarLimit(request.MaxItems);
                var items = new FilteredElementCollector(doc)
                    .OfClass(typeof(TElement))
                    .Cast<TElement>()
                    .Where(e => MatchesNameFilter(e, request.NameContains))
                    .OrderBy(e => e.Name)
                    .Take(maxItems)
                    .Select(e => projector?.Invoke(e) ?? BuildTypeLikeInfo(e))
                    .ToList();

                return Task.FromResult<object>(new Dictionary<string, object?>
                {
                    ["success"] = true,
                    ["count"] = items.Count,
                    ["maxItems"] = maxItems,
                    [resultName] = items,
                    ["message"] = $"Found {items.Count} {typeof(TElement).Name} element(s)."
                });
            }, ct, timeoutMs: 120_000);
        }

        private Task<object> DeleteReinforcementElementsAsync(
            RebarIdsRequest request,
            string label,
            Func<Element, bool> predicate,
            CancellationToken ct)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                if (request.ElementIds.Count == 0)
                    throw new ArgumentException("'elementIds' must contain at least one id.");
                var maxItems = NormalizeRebarLimit(request.MaxItems);
                if (request.ElementIds.Count > maxItems)
                    throw new ArgumentException($"Too many elementIds ({request.ElementIds.Count}); maxItems is {maxItems}.");

                var ids = new List<ElementId>();
                foreach (var rawId in request.ElementIds)
                {
                    var element = ResolveElement(doc, rawId, "elementIds");
                    if (!predicate(element))
                        throw new InvalidOperationException($"Element {rawId} is not a {label}.");
                    ids.Add(element.Id);
                }

                ICollection<ElementId> deleted = Array.Empty<ElementId>();
                if (!request.DryRun)
                {
                    using var tx = new Transaction(doc, $"RevitMCP: Delete {label}");
                    tx.Start();
                    deleted = doc.Delete(ids);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    dryRun = request.DryRun,
                    requestedCount = ids.Count,
                    deletedCount = deleted.Count,
                    elementIds = ids.Select(id => id.ToString()).ToList(),
                    deletedIds = deleted.Select(id => id.ToString()).ToList(),
                    message = request.DryRun
                        ? $"Dry run validated {ids.Count} {label} element(s)."
                        : $"Deleted {deleted.Count} element(s)."
                });
            }, ct);
        }

        private Task<object> UpdateReinforcementElementAsync<TElement>(
            UpdateRebarSystemRequest request,
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
                var parameterResults = new List<object>();

                using (var tx = new Transaction(doc, $"RevitMCP: Update {label}"))
                {
                    tx.Start();
                    if (!string.IsNullOrWhiteSpace(request.TypeId))
                    {
                        element.ChangeTypeId(ResolveRequiredElementId(doc, request.TypeId, "typeId"));
                        changed.Add("typeId");
                    }
                    foreach (var (name, value) in request.Parameters)
                        parameterResults.Add(TrySetParameter(element, name, value, "instance"));
                    if (request.Parameters.Count > 0)
                        changed.Add("parameters");
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    element = BuildReinforcementElementInfo(element, includeParameters: false),
                    changed,
                    parameterResults,
                    message = $"{label} {element.Id} updated."
                });
            }, ct);
        }

        private Task<object> CreateHostBoxRebarWorkflowAsync(
            RebarWorkflowRequest request,
            string workflowName,
            Func<Document, Element, RebarWorkflowRequest, List<CreateRebarFromCurvesRequest>> builder,
            CancellationToken ct)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var host = ResolveValidRebarHost(doc, request.HostId, "hostId");
                var specs = builder(doc, host, request);
                var rebars = new List<Rebar>();

                using (var tx = new Transaction(doc, $"RevitMCP: {workflowName}"))
                {
                    tx.Start();
                    foreach (var spec in specs)
                        rebars.Add(CreateRebarFromCurvesCore(doc, spec));
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    workflow = workflowName,
                    hostId = host.Id.ToString(),
                    count = rebars.Count,
                    rebars = rebars.Select(r => BuildRebarInfo(r, includeParameters: false)).ToList(),
                    message = $"{workflowName} created {rebars.Count} rebar element(s) from host bounding box."
                });
            }, ct);
        }

        private static Rebar CreateRebarFromCurvesCore(Document doc, CreateRebarFromCurvesRequest request)
        {
            var host = ResolveValidRebarHost(doc, request.HostId, "hostId");
            var barType = ResolveRebarTypeOrDefault<RebarBarType>(doc, request.BarTypeId, "barTypeId");
            var startHook = ResolveOptionalRebarType<RebarHookType>(doc, request.StartHookTypeId, "startHookTypeId");
            var endHook = ResolveOptionalRebarType<RebarHookType>(doc, request.EndHookTypeId, "endHookTypeId");
            var curves = BuildCurveList(request.Curves, request.Unit);
            var normal = NormalizeVector(new XYZ(request.NormalX, request.NormalY, request.NormalZ), "normal");
            var style = ParseRebarStyle(request.Style);
            var startOrientation = ParseEnum<RebarHookOrientation>(request.StartHookOrientation, "startHookOrientation");
            var endOrientation = ParseEnum<RebarHookOrientation>(request.EndHookOrientation, "endHookOrientation");

            var rebar = Rebar.CreateFromCurves(
                doc,
                style,
                barType,
                startHook,
                endHook,
                host,
                normal,
                curves,
                startOrientation,
                endOrientation,
                request.UseExistingShapeIfPossible,
                request.CreateNewShape);

            if (!string.IsNullOrWhiteSpace(request.LayoutRule))
            {
                ApplyLayoutToRebar(rebar, new RebarLayoutRequest
                {
                    LayoutRule = request.LayoutRule,
                    Count = request.Count,
                    Spacing = request.Spacing,
                    ArrayLength = request.ArrayLength,
                    Unit = request.Unit
                });
            }

            ApplyInstanceParameters(rebar, request.Parameters);
            return rebar;
        }

        private static void ApplyLayoutToRebar(Rebar rebar, RebarLayoutRequest request)
        {
            var accessor = rebar.GetShapeDrivenAccessor();
            var rule = request.LayoutRule.Trim().ToLowerInvariant();
            var count = Math.Max(1, request.Count ?? 1);
            var spacing = request.Spacing.HasValue ? ToInternalFeet(request.Spacing.Value, request.Unit) : 1.0;
            var arrayLength = request.ArrayLength.HasValue ? ToInternalFeet(request.ArrayLength.Value, request.Unit) : Math.Max(spacing, 1.0);

            switch (rule)
            {
                case "single":
                    accessor.SetLayoutAsSingle();
                    break;
                case "number_with_spacing":
                case "numberwithspacing":
                case "spacing":
                    accessor.SetLayoutAsNumberWithSpacing(count, spacing, request.BarsOnNormalSide, request.IncludeFirstBar, request.IncludeLastBar);
                    break;
                case "fixed_number":
                case "fixednumber":
                    accessor.SetLayoutAsFixedNumber(count, arrayLength, request.BarsOnNormalSide, request.IncludeFirstBar, request.IncludeLastBar);
                    break;
                case "maximum_spacing":
                case "maximumspacing":
                case "max_spacing":
                    accessor.SetLayoutAsMaximumSpacing(spacing, arrayLength, request.BarsOnNormalSide, request.IncludeFirstBar, request.IncludeLastBar);
                    break;
                case "minimum_clear_spacing":
                case "minimumclearspacing":
                case "min_clear_spacing":
                    accessor.SetLayoutAsMinimumClearSpacing(spacing, arrayLength, request.BarsOnNormalSide, request.IncludeFirstBar, request.IncludeLastBar);
                    break;
                default:
                    throw new ArgumentException(
                        $"Unsupported layoutRule '{request.LayoutRule}'. Use single, number_with_spacing, fixed_number, maximum_spacing, or minimum_clear_spacing.");
            }
        }

        private static Element ResolveValidRebarHost(Document doc, string? rawId, string paramName)
        {
            var host = ResolveElement(doc, rawId, paramName);
            if (!IsValidRebarHost(host))
                throw new InvalidOperationException($"Element {host.Id} is not a valid rebar host.");
            return host;
        }

        private static bool IsValidRebarHost(Element element)
        {
            try { return RebarHostData.IsValidHost(element); }
            catch { return false; }
        }

        private static Rebar ResolveRebar(Document doc, string? rawId, string paramName)
        {
            return ResolveElement(doc, rawId, paramName) as Rebar
                ?? throw new InvalidOperationException($"Rebar id '{rawId}' was not found.");
        }

        private static TElement ResolveRebarTypeOrDefault<TElement>(
            Document doc,
            string? rawId,
            string paramName)
            where TElement : Element
        {
            if (!string.IsNullOrWhiteSpace(rawId))
                return ResolveElement(doc, rawId, paramName) as TElement
                    ?? throw new InvalidOperationException($"{typeof(TElement).Name} id '{rawId}' was not found.");

            return new FilteredElementCollector(doc)
                .OfClass(typeof(TElement))
                .Cast<TElement>()
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    $"No {typeof(TElement).Name} exists in the document. Provide '{paramName}' after loading/creating one.");
        }

        private static ElementId ResolveRebarTypeIdOrDefault<TElement>(
            Document doc,
            string? rawId,
            string paramName)
            where TElement : Element
            => ResolveRebarTypeOrDefault<TElement>(doc, rawId, paramName).Id;

        private static TElement? ResolveOptionalRebarType<TElement>(
            Document doc,
            string? rawId,
            string paramName)
            where TElement : Element
            => string.IsNullOrWhiteSpace(rawId)
                ? null
                : ResolveElement(doc, rawId, paramName) as TElement
                    ?? throw new InvalidOperationException($"{typeof(TElement).Name} id '{rawId}' was not found.");

        private static ElementId ResolveOptionalRebarTypeId<TElement>(
            Document doc,
            string? rawId,
            string paramName)
            where TElement : Element
            => ResolveOptionalRebarType<TElement>(doc, rawId, paramName)?.Id ?? ElementId.InvalidElementId;

        private static ElementId ResolveCouplerTypeId(Document doc, string? rawId)
        {
            if (!string.IsNullOrWhiteSpace(rawId))
                return ResolveRequiredElementId(doc, rawId, "couplerTypeId");

            var type = new FilteredElementCollector(doc)
                .WhereElementIsElementType()
                .Cast<Element>()
                .OfType<ElementType>()
                .FirstOrDefault(LooksLikeCouplerElement);

            return type?.Id ?? throw new ArgumentException(
                "'couplerTypeId' is required because no coupler type candidate could be inferred from the document.");
        }

        private static MultiReferenceAnnotationType ResolveMultiReferenceAnnotationType(Document doc, string? rawId)
        {
            if (!string.IsNullOrWhiteSpace(rawId))
                return ResolveElement(doc, rawId, "typeId") as MultiReferenceAnnotationType
                    ?? throw new InvalidOperationException($"MultiReferenceAnnotationType id '{rawId}' was not found.");

            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(MultiReferenceAnnotationType))
                .Cast<MultiReferenceAnnotationType>()
                .FirstOrDefault();

            return existing ?? MultiReferenceAnnotationType.CreateDefault(doc);
        }

        private static List<Curve> ResolveRebarSystemCurves(Element host, RebarSystemRequest request)
        {
            if (request.Curves.Count > 0)
                return BuildCurveList(request.Curves, request.Unit);
            if (request.Boundary.Count >= 3)
                return BuildClosedCurveList(request.Boundary, request.Unit);
            return BuildDefaultBoundaryCurves(host);
        }

        private static List<Curve> BuildCurveList(IReadOnlyList<RebarCurveRequest> curves, string unit)
        {
            if (curves.Count == 0)
                throw new ArgumentException("'curves' must contain at least one curve.");

            return curves.Select(c =>
            {
                var start = ToXyz(c.Start, unit);
                var end = ToXyz(c.End, unit);
                if (start.IsAlmostEqualTo(end))
                    throw new ArgumentException("Curve start and end points cannot be identical.");
                return (Curve)Line.CreateBound(start, end);
            }).ToList();
        }

        private static List<Curve> BuildClosedCurveList(IReadOnlyList<RebarPointRequest> points, string unit)
        {
            var curves = new List<Curve>();
            if (points.Count < 3)
                throw new ArgumentException("'boundary' must contain at least three points.");
            for (var i = 0; i < points.Count; i++)
            {
                var start = ToXyz(points[i], unit);
                var end = ToXyz(points[(i + 1) % points.Count], unit);
                if (!start.IsAlmostEqualTo(end))
                    curves.Add(Line.CreateBound(start, end));
            }
            return curves;
        }

        private static CurveLoop BuildClosedCurveLoop(IReadOnlyList<RebarPointRequest> points, string unit)
        {
            var loop = new CurveLoop();
            foreach (var curve in BuildClosedCurveList(points, unit))
                loop.Append(curve);
            return loop;
        }

        private static CurveLoop BuildOpenCurveLoop(IReadOnlyList<RebarPointRequest> points, string unit)
        {
            if (points.Count < 2)
                throw new ArgumentException("'boundary' must contain at least two points.");
            var loop = new CurveLoop();
            for (var i = 0; i < points.Count - 1; i++)
            {
                var start = ToXyz(points[i], unit);
                var end = ToXyz(points[i + 1], unit);
                if (!start.IsAlmostEqualTo(end))
                    loop.Append(Line.CreateBound(start, end));
            }
            return loop;
        }

        private static XYZ ToXyz(RebarPointRequest point, string unit)
            => new(
                ToInternalFeet(point.X, unit),
                ToInternalFeet(point.Y, unit),
                ToInternalFeet(point.Z, unit));

        private static XYZ ToRawXyz(RebarPointRequest point)
            => new(point.X, point.Y, point.Z);

        private static XYZ NormalizeVector(XYZ vector, string paramName)
        {
            if (vector.GetLength() < 1e-9)
                throw new ArgumentException($"'{paramName}' cannot be a zero vector.");
            return vector.Normalize();
        }

        private static RebarStyle ParseRebarStyle(string? value)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            return normalized is "stirrup" or "stirrup_tie" or "stirruptie" or "tie"
                ? RebarStyle.StirrupTie
                : RebarStyle.Standard;
        }

        private static TEnum ParseEnum<TEnum>(string? value, string paramName)
            where TEnum : struct
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                Enum.TryParse<TEnum>(value.Replace("_", string.Empty), ignoreCase: true, out var parsed))
                return parsed;

            if (!string.IsNullOrWhiteSpace(value) &&
                Enum.TryParse<TEnum>(value, ignoreCase: true, out parsed))
                return parsed;

            throw new ArgumentException($"Unsupported {paramName} '{value}'.");
        }

        private static int NormalizeRebarLimit(int requested, int hardMax = HardRebarLimit)
        {
            var value = requested <= 0 ? DefaultRebarLimit : requested;
            return Math.Min(value, hardMax);
        }

        private static bool MatchesNameFilter(Element element, string? nameContains)
            => string.IsNullOrWhiteSpace(nameContains) ||
               SafeElementName(element).Contains(nameContains.Trim(), StringComparison.OrdinalIgnoreCase);

        private static bool LooksLikeCouplerElement(Element element)
        {
            if (ContainsCouplerText(SafeElementName(element)) ||
                ContainsCouplerText(element.Category?.Name) ||
                ContainsCouplerText(element.GetType().Name))
                return true;

            try
            {
                foreach (Parameter parameter in element.Parameters)
                {
                    if (ContainsCouplerText(parameter.Definition?.Name))
                        return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool ContainsCouplerText(string? value)
            => !string.IsNullOrWhiteSpace(value) &&
               value.Contains("coupler", StringComparison.OrdinalIgnoreCase);

        private static void ApplyInstanceParameters(Element element, Dictionary<string, JToken?> parameters)
        {
            foreach (var (name, value) in parameters)
                TrySetParameter(element, name, value, "instance");
        }

        private static object BuildRebarInfo(Rebar rebar, bool includeParameters)
        {
            var typeElement = GetTypeElement(rebar);
            var shapeId = SafeElementId(() => rebar.GetShapeId());
            var hostId = SafeElementId(() => rebar.GetHostId());
            var startHookId = SafeElementId(() => rebar.GetHookTypeId(0));
            var endHookId = SafeElementId(() => rebar.GetHookTypeId(1));
            var startEndTreatmentId = SafeElementId(() => rebar.GetEndTreatmentTypeId(0));
            var endEndTreatmentId = SafeElementId(() => rebar.GetEndTreatmentTypeId(1));

            return new
            {
                id = rebar.Id.ToString(),
                uniqueId = rebar.UniqueId,
                name = rebar.Name ?? string.Empty,
                className = rebar.GetType().Name,
                category = rebar.Category?.Name ?? string.Empty,
                typeId = SafeTypeId(rebar) == ElementId.InvalidElementId ? null : SafeTypeId(rebar).ToString(),
                typeName = typeElement?.Name,
                hostId = hostId == ElementId.InvalidElementId ? null : hostId.ToString(),
                shapeId = shapeId == ElementId.InvalidElementId ? null : shapeId.ToString(),
                layoutRule = Safe(() => rebar.LayoutRule.ToString(), null),
                quantity = Safe(() => rebar.Quantity, 0),
                numberOfBarPositions = Safe(() => rebar.NumberOfBarPositions, 0),
                hookTypeIds = new
                {
                    start = startHookId == ElementId.InvalidElementId ? null : startHookId.ToString(),
                    end = endHookId == ElementId.InvalidElementId ? null : endHookId.ToString()
                },
                endTreatmentTypeIds = new
                {
                    start = startEndTreatmentId == ElementId.InvalidElementId ? null : startEndTreatmentId.ToString(),
                    end = endEndTreatmentId == ElementId.InvalidElementId ? null : endEndTreatmentId.ToString()
                },
                boundingBox = BuildBoundingBoxInfo(rebar.get_BoundingBox(null)),
                parameters = includeParameters ? ReadParameters(rebar, "instance") : null
            };
        }

        private static object BuildReinforcementElementInfo(Element element, bool includeParameters)
        {
            if (element is Rebar rebar)
                return BuildRebarInfo(rebar, includeParameters);
            if (element is RebarCoupler coupler)
                return BuildCouplerInfo(coupler, includeParameters);

            var hostId = SafeElementId(() => InvokeElementIdMethod(element, "GetHostId"));
            return new
            {
                id = element.Id.ToString(),
                uniqueId = element.UniqueId,
                name = element.Name ?? string.Empty,
                className = element.GetType().Name,
                category = element.Category?.Name ?? string.Empty,
                typeId = SafeTypeId(element) == ElementId.InvalidElementId ? null : SafeTypeId(element).ToString(),
                typeName = GetTypeElement(element)?.Name,
                hostId = hostId == ElementId.InvalidElementId ? null : hostId.ToString(),
                boundingBox = BuildBoundingBoxInfo(element.get_BoundingBox(null)),
                parameters = includeParameters ? ReadParameters(element, "instance") : null
            };
        }

        private static object BuildCouplerInfo(RebarCoupler coupler, bool includeParameters)
        {
            var coupledData = Safe(() => coupler.GetCoupledReinforcementData(), null);
            var points = Safe(() => coupler.GetPointsForPlacement(), null);

            return new
            {
                id = coupler.Id.ToString(),
                uniqueId = coupler.UniqueId,
                name = coupler.Name ?? string.Empty,
                className = coupler.GetType().Name,
                category = coupler.Category?.Name ?? string.Empty,
                typeId = SafeTypeId(coupler) == ElementId.InvalidElementId ? null : SafeTypeId(coupler).ToString(),
                typeName = GetTypeElement(coupler)?.Name,
                quantity = Safe(() => coupler.GetCouplerQuantity(), 0),
                couplerMark = Safe(() => coupler.CouplerMark, null),
                rotationAngle = Safe(() => coupler.RotationAngle, 0.0),
                linksTwoBars = Safe(() => coupler.CouplerLinkTwoBars(), false),
                coupledReinforcement = coupledData?.Select(BuildReinforcementDataInfo).ToList(),
                placementPoints = points?.Select(BuildPointInfo).ToList(),
                boundingBox = BuildBoundingBoxInfo(coupler.get_BoundingBox(null)),
                parameters = includeParameters ? ReadParameters(coupler, "instance") : null
            };
        }

        private static object? BuildReinforcementDataInfo(ReinforcementData? data)
        {
            if (data == null)
                return null;
            if (data is RebarReinforcementData rebarData)
            {
                return new
                {
                    type = data.GetType().Name,
                    rebarId = rebarData.RebarId.ToString(),
                    end = rebarData.End
                };
            }

            return new
            {
                type = data.GetType().Name,
                isValidObject = data.IsValidObject
            };
        }

        private static object BuildShapeDrivenAccessorInfo(RebarShapeDrivenAccessor accessor)
        {
            return new
            {
                useRebarConstraintsToProduceVaryingBars = Safe(() => accessor.UseRebarConstraintsToProduceVaryingBars, false),
                arrayLength = Safe(() => accessor.ArrayLength, 0.0),
                barsOnNormalSide = Safe(() => accessor.BarsOnNormalSide, false),
                normal = Safe(() => BuildPointInfo(accessor.Normal), null),
                distributionPath = Safe(() => BuildCurveInfo(accessor.GetDistributionPath()), null)
            };
        }

        private static object BuildRebarConstraintSummary(Rebar rebar)
        {
            var accessor = Safe(() => rebar.GetShapeDrivenAccessor(), null);
            var manager = Safe(() => rebar.GetRebarConstraintsManager(), null);
            return new
            {
                shapeDriven = accessor == null ? null : BuildShapeDrivenAccessorInfo(accessor),
                manager = manager == null ? null : new
                {
                    hasValidRebar = Safe(() => manager.HasValidRebar(), false),
                    allHandleCount = Safe(() => manager.GetAllHandles().Count, 0),
                    constrainedHandleCount = Safe(() => manager.GetAllConstrainedHandles().Count, 0)
                }
            };
        }

        private static void RecomputeConstraintsIfAvailable(RebarConstraintsManager? manager)
        {
            if (manager == null)
                return;

            var method = manager.GetType().GetMethod("RecomputeConstraints", new[] { typeof(bool) });
            method?.Invoke(manager, new object[] { false });
        }

        private static object BuildTypeLikeInfo(Element element)
        {
            return new
            {
                id = element.Id.ToString(),
                name = SafeElementName(element),
                familyName = element is ElementType type ? SafeFamilyName(type) : string.Empty,
                className = element.GetType().Name,
                category = element.Category?.Name ?? string.Empty
            };
        }

        private static object BuildRebarQuantityInfo(Rebar rebar)
        {
            var hostId = SafeElementId(() => rebar.GetHostId());
            return new
            {
                id = rebar.Id.ToString(),
                name = rebar.Name ?? string.Empty,
                hostId = hostId == ElementId.InvalidElementId ? null : hostId.ToString(),
                typeName = GetTypeElement(rebar)?.Name,
                quantity = Safe(() => rebar.Quantity, 0),
                numberOfBarPositions = Safe(() => rebar.NumberOfBarPositions, 0),
                layoutRule = Safe(() => rebar.LayoutRule.ToString(), null),
                totalLength = FindFirstParameterValue(rebar, "Total Bar Length", "Total Length", "Bar Length"),
                partition = FindFirstParameterValue(rebar, "Partition")
            };
        }

        private static object BuildCouplerQuantityInfo(RebarCoupler coupler)
        {
            return new
            {
                id = coupler.Id.ToString(),
                name = coupler.Name ?? string.Empty,
                typeName = GetTypeElement(coupler)?.Name,
                quantity = Safe(() => coupler.GetCouplerQuantity(), 0),
                mark = Safe(() => coupler.CouplerMark, null),
                linksTwoBars = Safe(() => coupler.CouplerLinkTwoBars(), false)
            };
        }

        private static string? FindFirstParameterValue(Element element, params string[] names)
        {
            foreach (var name in names)
            {
                var parameter = element.LookupParameter(name);
                if (parameter?.HasValue == true)
                    return GetParameterDisplayValue(parameter);
            }
            return null;
        }

        private static ElementId InvokeElementIdMethod(Element element, string methodName)
        {
            var method = element.GetType().GetMethod(methodName, Type.EmptyTypes);
            if (method == null)
                return ElementId.InvalidElementId;
            return method.Invoke(element, null) as ElementId ?? ElementId.InvalidElementId;
        }

        private static ElementId SafeElementId(Func<ElementId> getter)
            => Safe(getter, ElementId.InvalidElementId);

        private static T Safe<T>(Func<T> getter, T fallback)
        {
            try { return getter(); }
            catch { return fallback; }
        }

        private static string SafeElementName(Element element)
        {
            try { return element.Name ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static BoundingBoxXYZ RequireBoundingBox(Element host)
            => host.get_BoundingBox(null)
               ?? throw new InvalidOperationException($"Element {host.Id} has no bounding box.");

        private static XYZ ToHostBoxOrigin(Element host)
        {
            var box = RequireBoundingBox(host);
            return new XYZ(box.Min.X, box.Min.Y, box.Min.Z);
        }

        private static List<Curve> BuildDefaultBoundaryCurves(Element host)
        {
            var box = RequireBoundingBox(host);
            var z = box.Min.Z;
            var p1 = new XYZ(box.Min.X, box.Min.Y, z);
            var p2 = new XYZ(box.Max.X, box.Min.Y, z);
            var p3 = new XYZ(box.Max.X, box.Max.Y, z);
            var p4 = new XYZ(box.Min.X, box.Max.Y, z);
            return new List<Curve>
            {
                Line.CreateBound(p1, p2),
                Line.CreateBound(p2, p3),
                Line.CreateBound(p3, p4),
                Line.CreateBound(p4, p1)
            };
        }

        private static List<Curve> BuildDefaultPathCurves(Element host)
        {
            var box = RequireBoundingBox(host);
            var y = (box.Min.Y + box.Max.Y) / 2.0;
            var z = (box.Min.Z + box.Max.Z) / 2.0;
            return new List<Curve>
            {
                Line.CreateBound(new XYZ(box.Min.X, y, z), new XYZ(box.Max.X, y, z))
            };
        }

        private static List<CreateRebarFromCurvesRequest> BuildColumnVerticalBars(
            Document doc,
            Element host,
            RebarWorkflowRequest request)
        {
            var box = RequireBoundingBox(host);
            var cover = ToInternalFeet(request.Cover, request.Unit);
            var specs = new List<CreateRebarFromCurvesRequest>();
            var points = new[]
            {
                new XYZ(box.Min.X + cover, box.Min.Y + cover, box.Min.Z + cover),
                new XYZ(box.Max.X - cover, box.Min.Y + cover, box.Min.Z + cover),
                new XYZ(box.Max.X - cover, box.Max.Y - cover, box.Min.Z + cover),
                new XYZ(box.Min.X + cover, box.Max.Y - cover, box.Min.Z + cover)
            };
            foreach (var point in points.Take(Math.Max(1, request.Count)))
            {
                specs.Add(MakeSingleCurveRebarRequest(
                    request,
                    host.Id,
                    new XYZ(point.X, point.Y, box.Min.Z + cover),
                    new XYZ(point.X, point.Y, box.Max.Z - cover),
                    XYZ.BasisX,
                    "standard"));
            }
            return specs;
        }

        private static List<CreateRebarFromCurvesRequest> BuildColumnTieSets(
            Document doc,
            Element host,
            RebarWorkflowRequest request)
        {
            var box = RequireBoundingBox(host);
            var cover = ToInternalFeet(request.Cover, request.Unit);
            var count = Math.Max(1, request.Count);
            var specs = new List<CreateRebarFromCurvesRequest>();
            for (var i = 0; i < count; i++)
            {
                var t = count == 1 ? 0.5 : i / (double)(count - 1);
                var z = box.Min.Z + cover + t * Math.Max(0, box.Max.Z - box.Min.Z - 2 * cover);
                var p1 = new XYZ(box.Min.X + cover, box.Min.Y + cover, z);
                var p2 = new XYZ(box.Max.X - cover, box.Min.Y + cover, z);
                var p3 = new XYZ(box.Max.X - cover, box.Max.Y - cover, z);
                var p4 = new XYZ(box.Min.X + cover, box.Max.Y - cover, z);
                specs.Add(MakeClosedCurveRebarRequest(request, host.Id, new[] { p1, p2, p3, p4 }, XYZ.BasisZ, "stirrup_tie"));
            }
            return specs;
        }

        private static List<CreateRebarFromCurvesRequest> BuildBeamLongitudinalBars(
            Document doc,
            Element host,
            RebarWorkflowRequest request)
        {
            var box = RequireBoundingBox(host);
            var cover = ToInternalFeet(request.Cover, request.Unit);
            var yValues = new[] { box.Min.Y + cover, box.Max.Y - cover };
            var zValues = new[] { box.Min.Z + cover, box.Max.Z - cover };
            var specs = new List<CreateRebarFromCurvesRequest>();
            foreach (var y in yValues)
            foreach (var z in zValues)
            {
                specs.Add(MakeSingleCurveRebarRequest(
                    request,
                    host.Id,
                    new XYZ(box.Min.X + cover, y, z),
                    new XYZ(box.Max.X - cover, y, z),
                    XYZ.BasisZ,
                    "standard"));
                if (specs.Count >= Math.Max(1, request.Count))
                    return specs;
            }
            return specs;
        }

        private static List<CreateRebarFromCurvesRequest> BuildBeamStirrupSets(
            Document doc,
            Element host,
            RebarWorkflowRequest request)
        {
            var box = RequireBoundingBox(host);
            var cover = ToInternalFeet(request.Cover, request.Unit);
            var count = Math.Max(1, request.Count);
            var specs = new List<CreateRebarFromCurvesRequest>();
            for (var i = 0; i < count; i++)
            {
                var t = count == 1 ? 0.5 : i / (double)(count - 1);
                var x = box.Min.X + cover + t * Math.Max(0, box.Max.X - box.Min.X - 2 * cover);
                var p1 = new XYZ(x, box.Min.Y + cover, box.Min.Z + cover);
                var p2 = new XYZ(x, box.Max.Y - cover, box.Min.Z + cover);
                var p3 = new XYZ(x, box.Max.Y - cover, box.Max.Z - cover);
                var p4 = new XYZ(x, box.Min.Y + cover, box.Max.Z - cover);
                specs.Add(MakeClosedCurveRebarRequest(request, host.Id, new[] { p1, p2, p3, p4 }, XYZ.BasisX, "stirrup_tie"));
            }
            return specs;
        }

        private static List<CreateRebarFromCurvesRequest> BuildWallGridBars(
            Document doc,
            Element host,
            RebarWorkflowRequest request)
        {
            var box = RequireBoundingBox(host);
            var cover = ToInternalFeet(request.Cover, request.Unit);
            var count = Math.Max(1, request.Count);
            var specs = new List<CreateRebarFromCurvesRequest>();
            for (var i = 0; i < count; i++)
            {
                var t = count == 1 ? 0.5 : i / (double)(count - 1);
                var x = box.Min.X + cover + t * Math.Max(0, box.Max.X - box.Min.X - 2 * cover);
                specs.Add(MakeSingleCurveRebarRequest(
                    request,
                    host.Id,
                    new XYZ(x, box.Min.Y + cover, box.Min.Z + cover),
                    new XYZ(x, box.Min.Y + cover, box.Max.Z - cover),
                    XYZ.BasisY,
                    "standard"));
            }
            for (var i = 0; i < count; i++)
            {
                var t = count == 1 ? 0.5 : i / (double)(count - 1);
                var z = box.Min.Z + cover + t * Math.Max(0, box.Max.Z - box.Min.Z - 2 * cover);
                specs.Add(MakeSingleCurveRebarRequest(
                    request,
                    host.Id,
                    new XYZ(box.Min.X + cover, box.Min.Y + cover, z),
                    new XYZ(box.Max.X - cover, box.Min.Y + cover, z),
                    XYZ.BasisY,
                    "standard"));
            }
            return specs;
        }

        private static List<CreateRebarFromCurvesRequest> BuildSlabGridBars(
            Document doc,
            Element host,
            RebarWorkflowRequest request)
        {
            var box = RequireBoundingBox(host);
            var cover = ToInternalFeet(request.Cover, request.Unit);
            var z = box.Max.Z - cover;
            var count = Math.Max(1, request.Count);
            var specs = new List<CreateRebarFromCurvesRequest>();
            for (var i = 0; i < count; i++)
            {
                var t = count == 1 ? 0.5 : i / (double)(count - 1);
                var y = box.Min.Y + cover + t * Math.Max(0, box.Max.Y - box.Min.Y - 2 * cover);
                specs.Add(MakeSingleCurveRebarRequest(
                    request,
                    host.Id,
                    new XYZ(box.Min.X + cover, y, z),
                    new XYZ(box.Max.X - cover, y, z),
                    XYZ.BasisZ,
                    "standard"));
            }
            for (var i = 0; i < count; i++)
            {
                var t = count == 1 ? 0.5 : i / (double)(count - 1);
                var x = box.Min.X + cover + t * Math.Max(0, box.Max.X - box.Min.X - 2 * cover);
                specs.Add(MakeSingleCurveRebarRequest(
                    request,
                    host.Id,
                    new XYZ(x, box.Min.Y + cover, z),
                    new XYZ(x, box.Max.Y - cover, z),
                    XYZ.BasisZ,
                    "standard"));
            }
            return specs;
        }

        private static CreateRebarFromCurvesRequest MakeSingleCurveRebarRequest(
            RebarWorkflowRequest request,
            ElementId hostId,
            XYZ start,
            XYZ end,
            XYZ normal,
            string style)
            => new()
            {
                HostId = hostId.ToString(),
                BarTypeId = request.BarTypeId,
                StartHookTypeId = request.HookTypeId,
                EndHookTypeId = request.HookTypeId,
                Style = style,
                NormalX = normal.X,
                NormalY = normal.Y,
                NormalZ = normal.Z,
                Unit = "feet",
                Curves = new List<RebarCurveRequest>
                {
                    new()
                    {
                        Start = new RebarPointRequest { X = start.X, Y = start.Y, Z = start.Z },
                        End = new RebarPointRequest { X = end.X, Y = end.Y, Z = end.Z }
                    }
                },
                Parameters = request.Parameters
            };

        private static CreateRebarFromCurvesRequest MakeClosedCurveRebarRequest(
            RebarWorkflowRequest request,
            ElementId hostId,
            IReadOnlyList<XYZ> points,
            XYZ normal,
            string style)
        {
            var curves = new List<RebarCurveRequest>();
            for (var i = 0; i < points.Count; i++)
            {
                var start = points[i];
                var end = points[(i + 1) % points.Count];
                curves.Add(new RebarCurveRequest
                {
                    Start = new RebarPointRequest { X = start.X, Y = start.Y, Z = start.Z },
                    End = new RebarPointRequest { X = end.X, Y = end.Y, Z = end.Z }
                });
            }

            return new CreateRebarFromCurvesRequest
            {
                HostId = hostId.ToString(),
                BarTypeId = request.BarTypeId,
                StartHookTypeId = request.HookTypeId,
                EndHookTypeId = request.HookTypeId,
                Style = style,
                NormalX = normal.X,
                NormalY = normal.Y,
                NormalZ = normal.Z,
                Unit = "feet",
                Curves = curves,
                Parameters = request.Parameters
            };
        }
    }
}
