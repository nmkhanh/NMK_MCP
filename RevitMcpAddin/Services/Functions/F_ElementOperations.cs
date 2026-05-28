using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Services
{
    public sealed partial class RevitService
    {
        private const int DefaultElementOperationLimit = 100;
        private const int HardElementOperationLimit = 500;

        public Task<object> GetElementAsync(GetElementRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var element = ResolveElement(doc, request.ElementId, "elementId");
                var info = BuildElementDetailInfo(element, request.IncludeParameters, request.IncludeTypeParameters);

                return Task.FromResult<object>(new
                {
                    success = true,
                    element = info,
                    message = $"Read element {element.Id}."
                });
            }, ct);
        }

        public Task<object> GetElementLocationAsync(ElementLocationRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var element = ResolveElement(doc, request.ElementId, "elementId");
                var location = BuildLocationInfo(element);

                return Task.FromResult<object>(new
                {
                    success = true,
                    elementId = element.Id.ToString(),
                    location,
                    message = location == null
                        ? $"Element {element.Id} has no Location object."
                        : $"Read location for element {element.Id}."
                });
            }, ct);
        }

        public Task<object> GetElementGeometrySummaryAsync(
            ElementGeometrySummaryRequest request,
            CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var element = ResolveElement(doc, request.ElementId, "elementId");
                var counters = new GeometryCounters();
                var options = new Options
                {
                    ComputeReferences = false,
                    IncludeNonVisibleObjects = request.IncludeNonVisibleObjects,
                    DetailLevel = ParseViewDetailLevel(request.DetailLevel)
                };

                var geometry = element.get_Geometry(options);
                if (geometry != null)
                    CountGeometry(geometry, counters);

                return Task.FromResult<object>(new
                {
                    success = true,
                    elementId = element.Id.ToString(),
                    detailLevel = options.DetailLevel.ToString(),
                    includeNonVisibleObjects = request.IncludeNonVisibleObjects,
                    boundingBox = BuildBoundingBoxInfo(element.get_BoundingBox(null)),
                    counters.SolidCount,
                    counters.CurveCount,
                    counters.MeshCount,
                    counters.InstanceCount,
                    counters.PointCount,
                    solidVolume = counters.SolidVolume,
                    message = $"Read geometry summary for element {element.Id}."
                });
            }, ct);
        }

        public Task<object> UpdateElementAsync(UpdateElementRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var element = ResolveElement(doc, request.ElementId, "elementId");
                var changed = new List<string>();
                var parameterResults = new List<object>();

                using (var tx = new Transaction(doc, "RevitMCP: Update Element"))
                {
                    tx.Start();

                    if (!string.IsNullOrWhiteSpace(request.Name) &&
                        !string.Equals(element.Name, request.Name.Trim(), StringComparison.Ordinal))
                    {
                        element.Name = request.Name.Trim();
                        changed.Add("name");
                    }

                    if (!string.IsNullOrWhiteSpace(request.TypeId))
                    {
                        var typeId = ResolveRequiredElementId(doc, request.TypeId, "typeId");
                        element.ChangeTypeId(typeId);
                        changed.Add("typeId");
                    }

                    if (request.Pinned.HasValue && element.Pinned != request.Pinned.Value)
                    {
                        element.Pinned = request.Pinned.Value;
                        changed.Add("pinned");
                    }

                    foreach (var (name, value) in request.Parameters)
                    {
                        var result = TrySetParameter(element, name, value, request.ParameterTarget);
                        parameterResults.Add(result);
                    }
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
                    parameterResults,
                    message = changed.Count == 0
                        ? $"Element {element.Id} had no requested changes."
                        : $"Element {element.Id} updated successfully."
                });
            }, ct);
        }

        public Task<object> ChangeElementTypeAsync(ChangeElementTypeRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var element = ResolveElement(doc, request.ElementId, "elementId");
                var typeId = ResolveRequiredElementId(doc, request.TypeId, "typeId");

                using (var tx = new Transaction(doc, "RevitMCP: Change Element Type"))
                {
                    tx.Start();
                    element.ChangeTypeId(typeId);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    element = BuildElementDetailInfo(element, false, false),
                    elementId = element.Id.ToString(),
                    typeId = typeId.ToString(),
                    message = $"Changed type for element {element.Id}."
                });
            }, ct);
        }

        public Task<object> CreateFamilyInstanceAsync(
            CreateFamilyInstanceRequest request,
            CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                FamilyInstance instance;
                using (var tx = new Transaction(doc, "RevitMCP: Create Family Instance"))
                {
                    tx.Start();
                    instance = CreateFamilyInstanceCore(doc, request);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    element = BuildElementDetailInfo(instance, false, false),
                    elementId = instance.Id.ToString(),
                    message = $"Family instance created successfully (id={instance.Id})."
                });
            }, ct);
        }

        public Task<object> PlaceCategoryInstanceAsync(
            CreateFamilyInstanceRequest request,
            string toolLabel,
            BuiltInCategory? expectedCategory,
            string? defaultStructuralType,
            CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                if (!string.IsNullOrWhiteSpace(defaultStructuralType) &&
                    (string.IsNullOrWhiteSpace(request.StructuralType) ||
                     string.Equals(request.StructuralType, "NonStructural", StringComparison.OrdinalIgnoreCase)))
                {
                    request.StructuralType = defaultStructuralType;
                }

                FamilyInstance instance;
                using (var tx = new Transaction(doc, $"RevitMCP: {toolLabel}"))
                {
                    tx.Start();
                    instance = CreateFamilyInstanceCore(doc, request, expectedCategory);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    element = BuildElementDetailInfo(instance, false, false),
                    elementId = instance.Id.ToString(),
                    message = $"{toolLabel} created successfully (id={instance.Id})."
                });
            }, ct);
        }

        public Task<object> DeleteElementsAsync(ElementIdsRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var ids = ResolveElementIds(doc, request.ElementIds, request.MaxItems);
                if (request.DryRun)
                {
                    return Task.FromResult<object>(new
                    {
                        success = true,
                        dryRun = true,
                        requestedCount = ids.Count,
                        elementIds = ids.Select(id => id.ToString()).ToList(),
                        message = $"Dry run validated {ids.Count} element(s) for deletion."
                    });
                }

                ICollection<ElementId> deleted;
                using (var tx = new Transaction(doc, "RevitMCP: Delete Elements"))
                {
                    tx.Start();
                    deleted = doc.Delete(ids);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    requestedCount = ids.Count,
                    deletedCount = deleted.Count,
                    deletedElementIds = deleted.Select(id => id.ToString()).ToList(),
                    message = $"Deleted {deleted.Count} element(s)."
                });
            }, ct, timeoutMs: 120_000);
        }

        public Task<object> MoveElementsAsync(MoveElementsRequest request, CancellationToken ct = default)
        {
            return TransformElementsAsync(
                request,
                "Move Elements",
                (doc, ids) =>
                {
                    var translation = new XYZ(
                        ToInternalFeet(request.X, request.Unit),
                        ToInternalFeet(request.Y, request.Unit),
                        ToInternalFeet(request.Z, request.Unit));
                    ElementTransformUtils.MoveElements(doc, ids, translation);
                    return new { translation = BuildPointInfo(translation) };
                },
                ct);
        }

        public Task<object> CopyElementsAsync(MoveElementsRequest request, CancellationToken ct = default)
        {
            return TransformElementsAsync(
                request,
                "Copy Elements",
                (doc, ids) =>
                {
                    var translation = new XYZ(
                        ToInternalFeet(request.X, request.Unit),
                        ToInternalFeet(request.Y, request.Unit),
                        ToInternalFeet(request.Z, request.Unit));
                    var copiedIds = ElementTransformUtils.CopyElements(doc, ids, translation);
                    return new
                    {
                        translation = BuildPointInfo(translation),
                        copiedElementIds = copiedIds.Select(id => id.ToString()).ToList(),
                        copiedCount = copiedIds.Count
                    };
                },
                ct);
        }

        public Task<object> RotateElementsAsync(RotateElementsRequest request, CancellationToken ct = default)
        {
            return TransformElementsAsync(
                request,
                "Rotate Elements",
                (doc, ids) =>
                {
                    var origin = new XYZ(
                        ToInternalFeet(request.OriginX, request.Unit),
                        ToInternalFeet(request.OriginY, request.Unit),
                        ToInternalFeet(request.OriginZ, request.Unit));
                    var axisVector = new XYZ(request.AxisX, request.AxisY, request.AxisZ);
                    if (axisVector.GetLength() < 1e-9)
                        throw new ArgumentException("Rotation axis vector cannot be zero.");

                    var axis = Line.CreateUnbound(origin, axisVector.Normalize());
                    var angleRadians = request.AngleDegrees * Math.PI / 180.0;
                    ElementTransformUtils.RotateElements(doc, ids, axis, angleRadians);
                    return new
                    {
                        origin = BuildPointInfo(origin),
                        axis = BuildPointInfo(axisVector.Normalize()),
                        angleDegrees = request.AngleDegrees
                    };
                },
                ct);
        }

        public Task<object> MirrorElementsAsync(MirrorElementsRequest request, CancellationToken ct = default)
        {
            return TransformElementsAsync(
                request,
                "Mirror Elements",
                (doc, ids) =>
                {
                    var origin = new XYZ(
                        ToInternalFeet(request.OriginX, request.Unit),
                        ToInternalFeet(request.OriginY, request.Unit),
                        ToInternalFeet(request.OriginZ, request.Unit));
                    var normal = new XYZ(request.NormalX, request.NormalY, request.NormalZ);
                    if (normal.GetLength() < 1e-9)
                        throw new ArgumentException("Mirror plane normal vector cannot be zero.");

                    var plane = Plane.CreateByNormalAndOrigin(normal.Normalize(), origin);
                    ElementTransformUtils.MirrorElements(doc, ids, plane, request.Copy);
                    return new
                    {
                        origin = BuildPointInfo(origin),
                        normal = BuildPointInfo(normal.Normalize()),
                        copy = request.Copy
                    };
                },
                ct);
        }

        public Task<object> PinElementsAsync(ElementIdsRequest request, CancellationToken ct = default)
        {
            return SetPinnedStateAsync(request, true, ct);
        }

        public Task<object> UnpinElementsAsync(ElementIdsRequest request, CancellationToken ct = default)
        {
            return SetPinnedStateAsync(request, false, ct);
        }

        public Task<object> HideElementsInViewAsync(ViewElementIdsRequest request, CancellationToken ct = default)
        {
            return SetViewHiddenStateAsync(request, hidden: true, ct);
        }

        public Task<object> UnhideElementsInViewAsync(ViewElementIdsRequest request, CancellationToken ct = default)
        {
            return SetViewHiddenStateAsync(request, hidden: false, ct);
        }

        public Task<object> ValidateBatchOperationsAsync(
            BatchOperationsRequest request,
            CancellationToken ct = default)
        {
            return ExecuteBatchOperationsAsync(request, validateOnly: true, ct);
        }

        public Task<object> ApplyBatchOperationsAsync(
            BatchOperationsRequest request,
            CancellationToken ct = default)
        {
            return ExecuteBatchOperationsAsync(request, validateOnly: request.DryRun, ct);
        }

        private Task<object> ExecuteBatchOperationsAsync(
            BatchOperationsRequest request,
            bool validateOnly,
            CancellationToken ct)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var uiDoc = uiApp.ActiveUIDocument
                    ?? throw new InvalidOperationException("No active document.");
                var doc = uiDoc.Document;

                ValidateBatchRequest(request);
                var results = new List<object>();
                var successCount = 0;
                var failureCount = 0;

                for (var i = 0; i < request.Operations.Count; i++)
                {
                    var item = request.Operations[i];
                    var operation = NormalizeBatchOperation(item.Operation);
                    var args = BuildBatchArguments(item);

                    try
                    {
                        object result;
                        if (validateOnly)
                        {
                            result = ExecuteSingleBatchOperation(doc, uiDoc.ActiveView, operation, args, apply: false);
                        }
                        else
                        {
                            using var tx = new Transaction(doc, $"RevitMCP: Batch {operation} #{i + 1}");
                            tx.Start();
                            result = ExecuteSingleBatchOperation(doc, uiDoc.ActiveView, operation, args, apply: true);
                            var status = tx.Commit();
                            if (status != TransactionStatus.Committed)
                                throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                        }

                        successCount++;
                        results.Add(new
                        {
                            index = i,
                            operation,
                            success = true,
                            result
                        });
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        results.Add(new
                        {
                            index = i,
                            operation,
                            success = false,
                            error = ex.Message
                        });

                        if (!request.ContinueOnError)
                            break;
                    }
                }

                return Task.FromResult<object>(new
                {
                    success = failureCount == 0 && successCount == request.Operations.Count,
                    dryRun = validateOnly,
                    requestedCount = request.Operations.Count,
                    successCount,
                    failureCount,
                    results,
                    message = validateOnly
                        ? $"Validated {successCount} of {request.Operations.Count} batch operation(s)."
                        : $"Applied {successCount} of {request.Operations.Count} batch operation(s)."
                });
            }, ct, timeoutMs: 180_000);
        }

        private Task<object> TransformElementsAsync<TRequest>(
            TRequest request,
            string operationName,
            Func<Document, ICollection<ElementId>, object> operation,
            CancellationToken ct)
            where TRequest : ElementIdsRequest
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var ids = ResolveElementIds(doc, request.ElementIds, request.MaxItems);
                if (request.DryRun)
                {
                    return Task.FromResult<object>(new
                    {
                        success = true,
                        dryRun = true,
                        operation = operationName,
                        requestedCount = ids.Count,
                        elementIds = ids.Select(id => id.ToString()).ToList(),
                        message = $"Dry run validated {ids.Count} element(s) for {operationName.ToLowerInvariant()}."
                    });
                }

                object operationResult;
                using (var tx = new Transaction(doc, $"RevitMCP: {operationName}"))
                {
                    tx.Start();
                    operationResult = operation(doc, ids);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    operation = operationName,
                    requestedCount = ids.Count,
                    result = operationResult,
                    message = $"{operationName} completed for {ids.Count} element(s)."
                });
            }, ct, timeoutMs: 120_000);
        }

        private Task<object> SetPinnedStateAsync(
            ElementIdsRequest request,
            bool pinned,
            CancellationToken ct)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var ids = ResolveElementIds(doc, request.ElementIds, request.MaxItems);
                if (request.DryRun)
                {
                    return Task.FromResult<object>(new
                    {
                        success = true,
                        dryRun = true,
                        requestedCount = ids.Count,
                        message = $"Dry run validated {ids.Count} element(s)."
                    });
                }

                var results = new List<object>();
                using (var tx = new Transaction(doc, pinned ? "RevitMCP: Pin Elements" : "RevitMCP: Unpin Elements"))
                {
                    tx.Start();
                    foreach (var id in ids)
                    {
                        var element = doc.GetElement(id);
                        try
                        {
                            element.Pinned = pinned;
                            results.Add(new { elementId = id.ToString(), success = true });
                        }
                        catch (Exception ex)
                        {
                            results.Add(new { elementId = id.ToString(), success = false, error = ex.Message });
                        }
                    }

                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                var successCount = results.Count(r => (bool)r.GetType().GetProperty("success")!.GetValue(r)!);
                return Task.FromResult<object>(new
                {
                    success = successCount == ids.Count,
                    requestedCount = ids.Count,
                    successCount,
                    failureCount = ids.Count - successCount,
                    results,
                    message = $"{(pinned ? "Pinned" : "Unpinned")} {successCount} of {ids.Count} element(s)."
                });
            }, ct, timeoutMs: 120_000);
        }

        private Task<object> SetViewHiddenStateAsync(
            ViewElementIdsRequest request,
            bool hidden,
            CancellationToken ct)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var uiDoc = uiApp.ActiveUIDocument
                    ?? throw new InvalidOperationException("No active document.");
                var doc = uiDoc.Document;
                var view = string.IsNullOrWhiteSpace(request.ViewId)
                    ? uiDoc.ActiveView
                    : ResolveView(doc, request.ViewId);

                if (view == null)
                    throw new InvalidOperationException("No active view.");
                if (view.IsTemplate)
                    throw new InvalidOperationException("Cannot hide or unhide elements in a view template.");

                var ids = ResolveElementIds(doc, request.ElementIds, request.MaxItems);
                if (request.DryRun)
                {
                    return Task.FromResult<object>(new
                    {
                        success = true,
                        dryRun = true,
                        viewId = view.Id.ToString(),
                        requestedCount = ids.Count,
                        message = $"Dry run validated {ids.Count} element(s) for view '{view.Name}'."
                    });
                }

                var eligibleIds = hidden
                    ? ids.Where(id => CanBeHidden(doc.GetElement(id), view)).ToList()
                    : ids;

                using (var tx = new Transaction(doc, hidden
                    ? "RevitMCP: Hide Elements In View"
                    : "RevitMCP: Unhide Elements In View"))
                {
                    tx.Start();
                    if (hidden)
                        view.HideElements(eligibleIds);
                    else
                        view.UnhideElements(eligibleIds);

                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = eligibleIds.Count == ids.Count,
                    viewId = view.Id.ToString(),
                    viewName = view.Name,
                    requestedCount = ids.Count,
                    changedCount = eligibleIds.Count,
                    skippedCount = ids.Count - eligibleIds.Count,
                    changedElementIds = eligibleIds.Select(id => id.ToString()).ToList(),
                    message = $"{(hidden ? "Hid" : "Unhid")} {eligibleIds.Count} of {ids.Count} element(s) in view '{view.Name}'."
                });
            }, ct, timeoutMs: 120_000);
        }

        private static List<ElementId> ResolveElementIds(
            Document doc,
            IReadOnlyList<string> rawIds,
            int requestedMaxItems)
        {
            if (rawIds.Count == 0)
                throw new ArgumentException("'elementIds' must contain at least one id.");

            var maxItems = requestedMaxItems <= 0 ? DefaultElementOperationLimit : requestedMaxItems;
            maxItems = Math.Min(maxItems, HardElementOperationLimit);
            if (rawIds.Count > maxItems)
                throw new ArgumentException(
                    $"Batch contains {rawIds.Count} element(s), above maxItems={maxItems}. " +
                    $"Increase maxItems up to {HardElementOperationLimit} or split the batch.");

            return rawIds
                .Select(raw => ResolveElement(doc, raw, "elementIds"))
                .Select(e => e.Id)
                .ToList();
        }

        private static FamilyInstance CreateFamilyInstanceCore(
            Document doc,
            CreateFamilyInstanceRequest request,
            BuiltInCategory? expectedCategory = null)
        {
            var symbol = ResolveElement(doc, request.SymbolId, "symbolId") as FamilySymbol
                ?? throw new InvalidOperationException($"FamilySymbol id '{request.SymbolId}' was not found.");
            if (expectedCategory.HasValue && !SymbolMatchesCategory(symbol, expectedCategory.Value))
                throw new InvalidOperationException(
                    $"FamilySymbol id '{request.SymbolId}' is category '{symbol.Category?.Name}', " +
                    $"not expected category '{expectedCategory.Value}'.");

            var point = new XYZ(
                ToInternalFeet(request.X, request.Unit),
                ToInternalFeet(request.Y, request.Unit),
                ToInternalFeet(request.Z, request.Unit));
            var structuralType = ParseStructuralType(request.StructuralType);

            if (!symbol.IsActive)
            {
                symbol.Activate();
                doc.Regenerate();
            }

            FamilyInstance instance;
            if (!string.IsNullOrWhiteSpace(request.HostId))
            {
                var host = ResolveElement(doc, request.HostId, "hostId");
                instance = doc.Create.NewFamilyInstance(point, symbol, host, structuralType);
            }
            else if (!string.IsNullOrWhiteSpace(request.LevelId))
            {
                var level = doc.GetElement(ResolveRequiredElementId(doc, request.LevelId, "levelId")) as Level
                    ?? throw new InvalidOperationException($"Level id '{request.LevelId}' was not found.");
                instance = doc.Create.NewFamilyInstance(point, symbol, level, structuralType);
            }
            else
            {
                instance = doc.Create.NewFamilyInstance(point, symbol, structuralType);
            }

            foreach (var (name, value) in request.Parameters)
                TrySetParameter(instance, name, value, "instance");

            return instance;
        }

        private static bool SymbolMatchesCategory(FamilySymbol symbol, BuiltInCategory category)
        {
            try { return symbol.Category != null && symbol.Category.Id.IntegerValue == (int)category; }
            catch { return true; }
        }

        private static StructuralType ParseStructuralType(string? value)
        {
            var normalized = value?.Trim().Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
            return normalized switch
            {
                null or "" or "nonstructural" => StructuralType.NonStructural,
                "beam" => StructuralType.Beam,
                "brace" => StructuralType.Brace,
                "column" => StructuralType.Column,
                "footing" => StructuralType.Footing,
                _ => Enum.TryParse<StructuralType>(value, ignoreCase: true, out var parsed)
                    ? parsed
                    : throw new ArgumentException(
                        $"Unsupported structuralType '{value}'. Use NonStructural, Beam, Brace, Column, or Footing.")
            };
        }

        private static void ValidateBatchRequest(BatchOperationsRequest request)
        {
            if (request.Operations.Count == 0)
                throw new ArgumentException("'operations' must contain at least one operation.");

            var max = request.MaxOperations <= 0 ? 25 : request.MaxOperations;
            max = Math.Min(max, 100);
            if (request.Operations.Count > max)
                throw new ArgumentException(
                    $"Batch contains {request.Operations.Count} operation(s), above maxOperations={max}. " +
                    "Split the batch or increase maxOperations up to 100.");
        }

        private static string NormalizeBatchOperation(string? operation)
        {
            if (string.IsNullOrWhiteSpace(operation))
                throw new ArgumentException("Each batch item requires an 'operation'.");

            return operation.Trim().ToLowerInvariant() switch
            {
                "create_family_instance" or "create_instance" => "create_family_instance",
                "update_element" or "update" => "update_element",
                "change_element_type" or "change_type" => "change_element_type",
                "delete_elements" or "delete" => "delete_elements",
                "move_elements" or "move" => "move_elements",
                "copy_elements" or "copy" => "copy_elements",
                "rotate_elements" or "rotate" => "rotate_elements",
                "mirror_elements" or "mirror" => "mirror_elements",
                "pin_elements" or "pin" => "pin_elements",
                "unpin_elements" or "unpin" => "unpin_elements",
                "hide_elements_in_view" or "hide_in_view" or "hide" => "hide_elements_in_view",
                "unhide_elements_in_view" or "unhide_in_view" or "unhide" => "unhide_elements_in_view",
                _ => throw new ArgumentException($"Unsupported batch operation '{operation}'.")
            };
        }

        private static JObject BuildBatchArguments(BatchOperationItem item)
        {
            var args = item.Arguments != null ? new JObject(item.Arguments) : new JObject();
            if (item.ExtraArguments == null)
                return args;

            foreach (var (key, value) in item.ExtraArguments)
            {
                if (string.Equals(key, "operation", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "arguments", StringComparison.OrdinalIgnoreCase) ||
                    args.ContainsKey(key))
                    continue;

                args[key] = value.DeepClone();
            }

            return args;
        }

        private static object ExecuteSingleBatchOperation(
            Document doc,
            View? activeView,
            string operation,
            JObject args,
            bool apply)
        {
            switch (operation)
            {
                case "create_family_instance":
                {
                    var request = args.ToObject<CreateFamilyInstanceRequest>() ?? new CreateFamilyInstanceRequest();
                    var symbol = ResolveElement(doc, request.SymbolId, "symbolId") as FamilySymbol
                        ?? throw new InvalidOperationException($"FamilySymbol id '{request.SymbolId}' was not found.");
                    if (!string.IsNullOrWhiteSpace(request.LevelId))
                        _ = doc.GetElement(ResolveRequiredElementId(doc, request.LevelId, "levelId")) as Level
                            ?? throw new InvalidOperationException($"Level id '{request.LevelId}' was not found.");
                    if (!string.IsNullOrWhiteSpace(request.HostId))
                        _ = ResolveElement(doc, request.HostId, "hostId");
                    _ = ParseStructuralType(request.StructuralType);

                    if (!apply)
                    {
                        return new
                        {
                            symbolId = symbol.Id.ToString(),
                            familyName = symbol.FamilyName,
                            typeName = symbol.Name
                        };
                    }

                    var instance = CreateFamilyInstanceCore(doc, request);
                    return new { elementId = instance.Id.ToString(), name = instance.Name };
                }

                case "update_element":
                {
                    var request = args.ToObject<UpdateElementRequest>() ?? new UpdateElementRequest();
                    var element = ResolveElement(doc, request.ElementId, "elementId");
                    if (!string.IsNullOrWhiteSpace(request.TypeId))
                        _ = ResolveRequiredElementId(doc, request.TypeId, "typeId");

                    if (!apply)
                        return new { elementId = element.Id.ToString(), parameterCount = request.Parameters.Count };

                    var changed = ApplyElementUpdates(doc, element, request);
                    return new { elementId = element.Id.ToString(), changed };
                }

                case "change_element_type":
                {
                    var request = args.ToObject<ChangeElementTypeRequest>() ?? new ChangeElementTypeRequest();
                    var element = ResolveElement(doc, request.ElementId, "elementId");
                    var typeId = ResolveRequiredElementId(doc, request.TypeId, "typeId");
                    if (apply)
                        element.ChangeTypeId(typeId);
                    return new { elementId = element.Id.ToString(), typeId = typeId.ToString() };
                }

                case "delete_elements":
                {
                    var request = args.ToObject<ElementIdsRequest>() ?? new ElementIdsRequest();
                    var ids = ResolveElementIds(doc, request.ElementIds, request.MaxItems);
                    if (!apply)
                        return new { elementCount = ids.Count };
                    var deleted = doc.Delete(ids);
                    return new { deletedCount = deleted.Count, deletedElementIds = deleted.Select(id => id.ToString()).ToList() };
                }

                case "move_elements":
                case "copy_elements":
                {
                    var request = args.ToObject<MoveElementsRequest>() ?? new MoveElementsRequest();
                    var ids = ResolveElementIds(doc, request.ElementIds, request.MaxItems);
                    var translation = new XYZ(
                        ToInternalFeet(request.X, request.Unit),
                        ToInternalFeet(request.Y, request.Unit),
                        ToInternalFeet(request.Z, request.Unit));

                    if (!apply)
                        return new { elementCount = ids.Count, translation = BuildPointInfo(translation) };

                    if (operation == "copy_elements")
                    {
                        var copied = ElementTransformUtils.CopyElements(doc, ids, translation);
                        return new { copiedCount = copied.Count, copiedElementIds = copied.Select(id => id.ToString()).ToList() };
                    }

                    ElementTransformUtils.MoveElements(doc, ids, translation);
                    return new { movedCount = ids.Count };
                }

                case "rotate_elements":
                {
                    var request = args.ToObject<RotateElementsRequest>() ?? new RotateElementsRequest();
                    var ids = ResolveElementIds(doc, request.ElementIds, request.MaxItems);
                    var origin = new XYZ(
                        ToInternalFeet(request.OriginX, request.Unit),
                        ToInternalFeet(request.OriginY, request.Unit),
                        ToInternalFeet(request.OriginZ, request.Unit));
                    var axisVector = new XYZ(request.AxisX, request.AxisY, request.AxisZ);
                    if (axisVector.GetLength() < 1e-9)
                        throw new ArgumentException("Rotation axis vector cannot be zero.");
                    var angleRadians = request.AngleDegrees * Math.PI / 180.0;

                    if (apply)
                    {
                        var axis = Line.CreateUnbound(origin, axisVector.Normalize());
                        ElementTransformUtils.RotateElements(doc, ids, axis, angleRadians);
                    }

                    return new { elementCount = ids.Count, angleDegrees = request.AngleDegrees };
                }

                case "mirror_elements":
                {
                    var request = args.ToObject<MirrorElementsRequest>() ?? new MirrorElementsRequest();
                    var ids = ResolveElementIds(doc, request.ElementIds, request.MaxItems);
                    var origin = new XYZ(
                        ToInternalFeet(request.OriginX, request.Unit),
                        ToInternalFeet(request.OriginY, request.Unit),
                        ToInternalFeet(request.OriginZ, request.Unit));
                    var normal = new XYZ(request.NormalX, request.NormalY, request.NormalZ);
                    if (normal.GetLength() < 1e-9)
                        throw new ArgumentException("Mirror plane normal vector cannot be zero.");

                    if (apply)
                    {
                        var plane = Plane.CreateByNormalAndOrigin(normal.Normalize(), origin);
                        ElementTransformUtils.MirrorElements(doc, ids, plane, request.Copy);
                    }

                    return new { elementCount = ids.Count, copy = request.Copy };
                }

                case "pin_elements":
                case "unpin_elements":
                {
                    var request = args.ToObject<ElementIdsRequest>() ?? new ElementIdsRequest();
                    var ids = ResolveElementIds(doc, request.ElementIds, request.MaxItems);
                    if (apply)
                    {
                        var pinned = operation == "pin_elements";
                        foreach (var id in ids)
                            doc.GetElement(id).Pinned = pinned;
                    }
                    return new { elementCount = ids.Count };
                }

                case "hide_elements_in_view":
                case "unhide_elements_in_view":
                {
                    var request = args.ToObject<ViewElementIdsRequest>() ?? new ViewElementIdsRequest();
                    var view = string.IsNullOrWhiteSpace(request.ViewId)
                        ? activeView
                        : ResolveView(doc, request.ViewId);
                    if (view == null)
                        throw new InvalidOperationException("No active view.");
                    if (view.IsTemplate)
                        throw new InvalidOperationException("Cannot hide or unhide elements in a view template.");

                    var ids = ResolveElementIds(doc, request.ElementIds, request.MaxItems);
                    var eligibleIds = operation == "hide_elements_in_view"
                        ? ids.Where(id => CanBeHidden(doc.GetElement(id), view)).ToList()
                        : ids;

                    if (apply && eligibleIds.Count > 0)
                    {
                        if (operation == "hide_elements_in_view")
                            view.HideElements(eligibleIds);
                        else
                            view.UnhideElements(eligibleIds);
                    }

                    return new { viewId = view.Id.ToString(), elementCount = ids.Count, eligibleCount = eligibleIds.Count };
                }

                default:
                    throw new ArgumentException($"Unsupported batch operation '{operation}'.");
            }
        }

        private static List<string> ApplyElementUpdates(
            Document doc,
            Element element,
            UpdateElementRequest request)
        {
            var changed = new List<string>();

            if (!string.IsNullOrWhiteSpace(request.Name) &&
                !string.Equals(element.Name, request.Name.Trim(), StringComparison.Ordinal))
            {
                element.Name = request.Name.Trim();
                changed.Add("name");
            }

            if (!string.IsNullOrWhiteSpace(request.TypeId))
            {
                var typeId = ResolveRequiredElementId(doc, request.TypeId, "typeId");
                element.ChangeTypeId(typeId);
                changed.Add("typeId");
            }

            if (request.Pinned.HasValue && element.Pinned != request.Pinned.Value)
            {
                element.Pinned = request.Pinned.Value;
                changed.Add("pinned");
            }

            foreach (var (name, value) in request.Parameters)
                TrySetParameter(element, name, value, request.ParameterTarget);
            if (request.Parameters.Count > 0)
                changed.Add("parameters");

            return changed;
        }

        private static ElementDetailInfo BuildElementDetailInfo(
            Element element,
            bool includeParameters,
            bool includeTypeParameters)
        {
            var typeElement = GetTypeElement(element);
            var typeId = SafeTypeId(element);
            var info = new ElementDetailInfo
            {
                Id = element.Id.ToString(),
                UniqueId = element.UniqueId,
                Name = element.Name ?? string.Empty,
                ClassName = element.GetType().Name,
                Category = element.Category?.Name ?? string.Empty,
                CategoryId = element.Category?.Id.ToString(),
                TypeId = typeId == ElementId.InvalidElementId ? null : typeId.ToString(),
                TypeName = typeElement?.Name,
                LevelId = element.LevelId == ElementId.InvalidElementId ? null : element.LevelId.ToString(),
                WorksetId = element.WorksetId.ToString(),
                IsPinned = SafeIsPinned(element),
                IsElementType = element is ElementType,
                BoundingBox = BuildBoundingBoxInfo(element.get_BoundingBox(null))
            };

            if (includeParameters)
            {
                info.Parameters = new
                {
                    instance = ReadParameters(element, "instance"),
                    type = includeTypeParameters && typeElement != null
                        ? ReadParameters(typeElement, "type")
                        : new List<RevitParameterInfo>()
                };
            }

            return info;
        }

        private static ElementId SafeTypeId(Element element)
        {
            try { return element.GetTypeId(); }
            catch { return ElementId.InvalidElementId; }
        }

        private static bool SafeIsPinned(Element element)
        {
            try { return element.Pinned; }
            catch { return false; }
        }

        private static object? BuildLocationInfo(Element element)
        {
            return element.Location switch
            {
                LocationPoint point => new
                {
                    type = "point",
                    point = BuildPointInfo(point.Point),
                    rotation = point.Rotation
                },
                LocationCurve curve => new
                {
                    type = "curve",
                    curve = BuildCurveInfo(curve.Curve)
                },
                null => null,
                _ => new
                {
                    type = element.Location.GetType().Name
                }
            };
        }

        private static object BuildCurveInfo(Curve curve)
        {
            var result = new Dictionary<string, object?>
            {
                ["curveType"] = curve.GetType().Name,
                ["length"] = curve.Length,
                ["start"] = BuildPointInfo(curve.GetEndPoint(0)),
                ["end"] = BuildPointInfo(curve.GetEndPoint(1))
            };

            if (curve is Arc arc)
            {
                result["center"] = BuildPointInfo(arc.Center);
                result["radius"] = arc.Radius;
            }

            return result;
        }

        private static BoundingBoxInfo? BuildBoundingBoxInfo(BoundingBoxXYZ? box)
        {
            if (box == null)
                return null;

            return new BoundingBoxInfo
            {
                Min = BuildPointInfo(box.Min),
                Max = BuildPointInfo(box.Max)
            };
        }

        private static ViewDetailLevel ParseViewDetailLevel(string? value)
        {
            return value?.Trim().ToLowerInvariant() switch
            {
                "coarse" => ViewDetailLevel.Coarse,
                "fine" => ViewDetailLevel.Fine,
                "undefined" => ViewDetailLevel.Undefined,
                _ => ViewDetailLevel.Medium
            };
        }

        private static bool CanBeHidden(Element element, View view)
        {
            try { return element.CanBeHidden(view); }
            catch { return false; }
        }

        private static void CountGeometry(GeometryElement geometry, GeometryCounters counters)
        {
            foreach (GeometryObject obj in geometry)
                CountGeometryObject(obj, counters);
        }

        private static void CountGeometryObject(GeometryObject obj, GeometryCounters counters)
        {
            switch (obj)
            {
                case Solid solid:
                    if (solid.Faces.Size > 0 || solid.Edges.Size > 0)
                    {
                        counters.SolidCount++;
                        counters.SolidVolume += SafeSolidVolume(solid);
                    }
                    break;

                case Curve:
                    counters.CurveCount++;
                    break;

                case Mesh:
                    counters.MeshCount++;
                    break;

                case GeometryInstance instance:
                    counters.InstanceCount++;
                    var instanceGeometry = instance.GetInstanceGeometry();
                    if (instanceGeometry != null)
                        CountGeometry(instanceGeometry, counters);
                    break;

                case Point:
                    counters.PointCount++;
                    break;
            }
        }

        private static double SafeSolidVolume(Solid solid)
        {
            try { return solid.Volume; }
            catch { return 0; }
        }

        private sealed class GeometryCounters
        {
            public int SolidCount { get; set; }
            public int CurveCount { get; set; }
            public int MeshCount { get; set; }
            public int InstanceCount { get; set; }
            public int PointCount { get; set; }
            public double SolidVolume { get; set; }
        }
    }
}
