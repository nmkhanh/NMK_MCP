using Autodesk.Revit.DB;
using RevitMcpAddin.Models;
using Color = Autodesk.Revit.DB.Color;
using View = Autodesk.Revit.DB.View;

namespace RevitMcpAddin.Services
{
    public sealed partial class RevitService
    {
        public Task<object> CreateMaterialAsync(CreateMaterialRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                if (string.IsNullOrWhiteSpace(request.Name))
                    throw new ArgumentException("'name' is required.");

                Material material;
                using (var tx = new Transaction(doc, "RevitMCP: Create Material"))
                {
                    tx.Start();
                    var id = Material.Create(doc, request.Name.Trim());
                    material = doc.GetElement(id) as Material
                        ?? throw new InvalidOperationException("Material.Create did not return a material.");
                    ApplyMaterialUpdates(material, null, request.Color, request.Transparency);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    material = BuildMaterialInfo(material),
                    elementId = material.Id.ToString(),
                    message = $"Material '{material.Name}' created successfully."
                });
            }, ct);
        }

        public Task<object> UpdateMaterialAsync(UpdateMaterialRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var material = ResolveMaterial(doc, request.MaterialId, request.Name);
                var changed = new List<string>();

                using (var tx = new Transaction(doc, "RevitMCP: Update Material"))
                {
                    tx.Start();
                    ApplyMaterialUpdates(material, request.NewName, request.Color, request.Transparency, changed);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    material = BuildMaterialInfo(material),
                    changed,
                    message = changed.Count == 0
                        ? $"Material '{material.Name}' had no requested changes."
                        : $"Material '{material.Name}' updated successfully."
                });
            }, ct);
        }

        public Task<object> DuplicateElementTypeAsync(DuplicateElementTypeRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                if (string.IsNullOrWhiteSpace(request.Name))
                    throw new ArgumentException("'name' is required.");

                var type = ResolveElement(doc, request.TypeId, "typeId") as ElementType
                    ?? throw new InvalidOperationException($"ElementType id '{request.TypeId}' was not found.");
                ElementType duplicate;

                using (var tx = new Transaction(doc, "RevitMCP: Duplicate Element Type"))
                {
                    tx.Start();
                    duplicate = type.Duplicate(request.Name.Trim());
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    type = BuildElementTypeSummary(duplicate),
                    elementId = duplicate.Id.ToString(),
                    message = $"Element type duplicated as '{duplicate.Name}'."
                });
            }, ct);
        }

        public Task<object> RenameElementTypeAsync(RenameElementTypeRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                if (string.IsNullOrWhiteSpace(request.Name))
                    throw new ArgumentException("'name' is required.");

                var type = ResolveElement(doc, request.TypeId, "typeId") as ElementType
                    ?? throw new InvalidOperationException($"ElementType id '{request.TypeId}' was not found.");

                using (var tx = new Transaction(doc, "RevitMCP: Rename Element Type"))
                {
                    tx.Start();
                    type.Name = request.Name.Trim();
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    type = BuildElementTypeSummary(type),
                    elementId = type.Id.ToString(),
                    message = $"Element type renamed to '{type.Name}'."
                });
            }, ct);
        }

        public Task<object> SetTypeParameterAsync(SetTypeParameterRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                if (string.IsNullOrWhiteSpace(request.ParameterName))
                    throw new ArgumentException("'parameterName' is required.");

                var type = ResolveElement(doc, request.TypeId, "typeId") as ElementType
                    ?? throw new InvalidOperationException($"ElementType id '{request.TypeId}' was not found.");
                object result;

                using (var tx = new Transaction(doc, "RevitMCP: Set Type Parameter"))
                {
                    tx.Start();
                    result = TrySetParameter(type, request.ParameterName, request.Value, "instance");
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    typeId = type.Id.ToString(),
                    result,
                    message = $"Parameter '{request.ParameterName}' processed on type '{type.Name}'."
                });
            }, ct);
        }

        public Task<object> SetTypeParametersAsync(SetTypeParametersRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                if (request.Parameters.Count == 0)
                    throw new ArgumentException("'parameters' must contain at least one parameter.");

                var type = ResolveElement(doc, request.TypeId, "typeId") as ElementType
                    ?? throw new InvalidOperationException($"ElementType id '{request.TypeId}' was not found.");
                var results = new List<object>();

                using (var tx = new Transaction(doc, "RevitMCP: Set Type Parameters"))
                {
                    tx.Start();
                    foreach (var (name, value) in request.Parameters)
                        results.Add(TrySetParameter(type, name, value, "instance"));
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    typeId = type.Id.ToString(),
                    requestedCount = results.Count,
                    results,
                    message = $"Processed {results.Count} parameter(s) on type '{type.Name}'."
                });
            }, ct);
        }

        public Task<object> ApplyViewTemplateAsync(ApplyViewTemplateRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var view = ResolveView(doc, request.ViewId);
                var template = ResolveView(doc, request.ViewTemplateId);
                if (!template.IsTemplate)
                    throw new InvalidOperationException($"View id '{template.Id}' is not a view template.");

                using (var tx = new Transaction(doc, "RevitMCP: Apply View Template"))
                {
                    tx.Start();
                    view.ViewTemplateId = template.Id;
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    view = BuildViewInfo(view),
                    message = $"Applied view template '{template.Name}' to view '{view.Name}'."
                });
            }, ct);
        }

        public Task<object> CreateViewTemplateAsync(CreateViewTemplateRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var source = ResolveView(doc, request.SourceViewId);
                View template;

                using (var tx = new Transaction(doc, "RevitMCP: Create View Template"))
                {
                    tx.Start();
                    template = source.CreateViewTemplate();
                    if (!string.IsNullOrWhiteSpace(request.Name))
                        template.Name = request.Name.Trim();
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    viewTemplate = BuildViewInfo(template),
                    elementId = template.Id.ToString(),
                    message = $"View template '{template.Name}' created from '{source.Name}'."
                });
            }, ct);
        }

        public Task<object> CreateTextNoteAsync(CreateTextNoteRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var view = ResolveView(doc, request.ViewId);
                if (string.IsNullOrWhiteSpace(request.Text))
                    throw new ArgumentException("'text' is required.");

                TextNote note;
                using (var tx = new Transaction(doc, "RevitMCP: Create Text Note"))
                {
                    tx.Start();
                    var point = new XYZ(
                        ToInternalFeet(request.X, request.Unit),
                        ToInternalFeet(request.Y, request.Unit),
                        ToInternalFeet(request.Z, request.Unit));
                    var typeId = ResolveTextNoteTypeId(doc, request.TextNoteTypeId);
                    note = TextNote.Create(doc, view.Id, point, request.Text, typeId);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    textNote = BuildTextNoteInfo(note),
                    elementId = note.Id.ToString(),
                    message = $"Text note created in view '{view.Name}'."
                });
            }, ct);
        }

        public Task<object> UpdateTextNoteAsync(UpdateTextNoteRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var note = ResolveElement(doc, request.TextNoteId, "textNoteId") as TextNote
                    ?? throw new InvalidOperationException($"TextNote id '{request.TextNoteId}' was not found.");
                var changed = new List<string>();

                using (var tx = new Transaction(doc, "RevitMCP: Update Text Note"))
                {
                    tx.Start();
                    if (request.Text != null && note.Text != request.Text)
                    {
                        note.Text = request.Text;
                        changed.Add("text");
                    }
                    if (request.X.HasValue || request.Y.HasValue || request.Z.HasValue)
                    {
                        var current = note.Coord;
                        note.Coord = new XYZ(
                            request.X.HasValue ? ToInternalFeet(request.X.Value, request.Unit) : current.X,
                            request.Y.HasValue ? ToInternalFeet(request.Y.Value, request.Unit) : current.Y,
                            request.Z.HasValue ? ToInternalFeet(request.Z.Value, request.Unit) : current.Z);
                        changed.Add("coord");
                    }
                    if (!string.IsNullOrWhiteSpace(request.TextNoteTypeId))
                    {
                        note.ChangeTypeId(ResolveTextNoteTypeId(doc, request.TextNoteTypeId));
                        changed.Add("textNoteTypeId");
                    }
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    textNote = BuildTextNoteInfo(note),
                    changed,
                    message = $"Text note {note.Id} updated."
                });
            }, ct);
        }

        public Task<object> CreateDetailLineAsync(CreateLineRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var view = ResolveView(doc, request.ViewId);
                DetailCurve curve;

                using (var tx = new Transaction(doc, "RevitMCP: Create Detail Line"))
                {
                    tx.Start();
                    curve = doc.Create.NewDetailCurve(view, BuildLine(request));
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    elementId = curve.Id.ToString(),
                    message = $"Detail line created in view '{view.Name}'."
                });
            }, ct);
        }

        public Task<object> CreateModelLineAsync(CreateLineRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                ModelCurve curve;

                using (var tx = new Transaction(doc, "RevitMCP: Create Model Line"))
                {
                    tx.Start();
                    var line = BuildLine(request);
                    var plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, line.GetEndPoint(0));
                    var sketchPlane = SketchPlane.Create(doc, plane);
                    curve = doc.Create.NewModelCurve(line, sketchPlane);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    elementId = curve.Id.ToString(),
                    message = $"Model line created successfully."
                });
            }, ct);
        }

        private static Material ResolveMaterial(Document doc, string? rawId, string? name)
        {
            if (!string.IsNullOrWhiteSpace(rawId))
                return ResolveElement(doc, rawId, "materialId") as Material
                    ?? throw new InvalidOperationException($"Material id '{rawId}' was not found.");

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Either 'materialId' or 'name' is required.");

            return new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .FirstOrDefault(m => string.Equals(m.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Material '{name}' was not found.");
        }

        private static void ApplyMaterialUpdates(
            Material material,
            string? newName,
            RgbColorRequest? color,
            int? transparency,
            List<string>? changed = null)
        {
            if (!string.IsNullOrWhiteSpace(newName) &&
                !string.Equals(material.Name, newName.Trim(), StringComparison.Ordinal))
            {
                material.Name = newName.Trim();
                changed?.Add("name");
            }
            if (color != null)
            {
                material.Color = new Color(ClampByte(color.R), ClampByte(color.G), ClampByte(color.B));
                changed?.Add("color");
            }
            if (transparency.HasValue)
            {
                material.Transparency = ClampInt(transparency.Value, 0, 100);
                changed?.Add("transparency");
            }
        }

        private static byte ClampByte(int value)
        {
            return (byte)ClampInt(value, 0, 255);
        }

        private static int ClampInt(int value, int min, int max)
        {
            return Math.Min(Math.Max(value, min), max);
        }

        private static object BuildMaterialInfo(Material material)
        {
            return new
            {
                id = material.Id.ToString(),
                name = material.Name,
                color = material.Color == null ? null : new
                {
                    r = material.Color.Red,
                    g = material.Color.Green,
                    b = material.Color.Blue
                },
                transparency = material.Transparency
            };
        }

        private static ElementId ResolveTextNoteTypeId(Document doc, string? rawId)
        {
            if (!string.IsNullOrWhiteSpace(rawId))
                return ResolveRequiredElementId(doc, rawId, "textNoteTypeId");

            var type = new FilteredElementCollector(doc)
                .OfClass(typeof(TextNoteType))
                .FirstElementId();
            return type == ElementId.InvalidElementId
                ? throw new InvalidOperationException("No TextNoteType was found in the document.")
                : type;
        }

        private static object BuildTextNoteInfo(TextNote note)
        {
            return new
            {
                id = note.Id.ToString(),
                text = note.Text,
                viewId = note.OwnerViewId.ToString(),
                typeId = note.GetTypeId().ToString(),
                coord = BuildPointInfo(note.Coord)
            };
        }

        private static Line BuildLine(CreateLineRequest request)
        {
            var start = new XYZ(
                ToInternalFeet(request.StartX, request.Unit),
                ToInternalFeet(request.StartY, request.Unit),
                ToInternalFeet(request.StartZ, request.Unit));
            var end = new XYZ(
                ToInternalFeet(request.EndX, request.Unit),
                ToInternalFeet(request.EndY, request.Unit),
                ToInternalFeet(request.EndZ, request.Unit));
            if (start.DistanceTo(end) < 1e-9)
                throw new ArgumentException("Line start and end points must be different.");
            return Line.CreateBound(start, end);
        }
    }
}
