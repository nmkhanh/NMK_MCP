using Autodesk.Revit.DB;
using RevitMcpAddin.Models;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;
using IOFileNotFoundException = System.IO.FileNotFoundException;
using IOPath = System.IO.Path;

namespace RevitMcpAddin.Services
{
    public sealed partial class RevitService
    {
        public Task<object> CreateScheduleAsync(CreateScheduleRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var categoryId = ResolveScheduleCategoryId(doc, request.CategoryId, request.Category);
                ViewSchedule schedule;

                using (var tx = new Transaction(doc, "RevitMCP: Create Schedule"))
                {
                    tx.Start();
                    schedule = ViewSchedule.CreateSchedule(doc, categoryId);
                    if (!string.IsNullOrWhiteSpace(request.Name))
                        schedule.Name = request.Name.Trim();
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
                    message = $"Schedule '{schedule.Name}' created successfully."
                });
            }, ct);
        }

        public Task<object> UpdateScheduleAsync(UpdateScheduleRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var schedule = ResolveSchedule(doc, request.ScheduleId);
                var changed = new List<string>();

                using (var tx = new Transaction(doc, "RevitMCP: Update Schedule"))
                {
                    tx.Start();
                    if (!string.IsNullOrWhiteSpace(request.Name) &&
                        !string.Equals(schedule.Name, request.Name.Trim(), StringComparison.Ordinal))
                    {
                        schedule.Name = request.Name.Trim();
                        changed.Add("name");
                    }
                    var added = AddScheduleFields(doc, schedule, request.FieldNames);
                    if (added.Count > 0)
                        changed.Add("fieldNames");
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    schedule = BuildScheduleInfo(schedule),
                    changed,
                    message = $"Schedule '{schedule.Name}' updated successfully."
                });
            }, ct);
        }

        public Task<object> GetScheduleDataAsync(GetScheduleDataRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var schedule = ResolveSchedule(doc, request.ScheduleId);
                var maxRows = request.MaxRows <= 0 ? 500 : Math.Min(request.MaxRows, 5000);
                var maxColumns = request.MaxColumns <= 0 ? 100 : Math.Min(request.MaxColumns, 500);
                var data = ReadScheduleSection(schedule, SectionType.Body, maxRows, maxColumns);

                return Task.FromResult<object>(new
                {
                    success = true,
                    schedule = BuildScheduleInfo(schedule),
                    rowCount = data.Count,
                    maxRows,
                    maxColumns,
                    rows = data,
                    message = $"Read {data.Count} row(s) from schedule '{schedule.Name}'."
                });
            }, ct, timeoutMs: 120_000);
        }

        public Task<object> ExportScheduleAsync(ExportScheduleRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var schedule = ResolveSchedule(doc, request.ScheduleId);
                var folder = ResolveOutputFolder(doc, request.OutputFolder);
                var fileName = string.IsNullOrWhiteSpace(request.FileName)
                    ? $"{SanitizeFileName(schedule.Name)}.txt"
                    : request.FileName.Trim();

                schedule.Export(folder, fileName, new ViewScheduleExportOptions());

                return Task.FromResult<object>(new
                {
                    success = true,
                    path = IOPath.Combine(folder, fileName),
                    schedule = BuildScheduleInfo(schedule),
                    message = $"Schedule '{schedule.Name}' exported successfully."
                });
            }, ct, timeoutMs: 120_000);
        }

        public Task<object> LoadFamilyAsync(LoadFamilyRequest request, CancellationToken ct = default)
        {
            return LoadFamilyInternalAsync(request, "Load Family", ct);
        }

        public Task<object> ReloadFamilyAsync(LoadFamilyRequest request, CancellationToken ct = default)
        {
            request.OverwriteExisting = true;
            return LoadFamilyInternalAsync(request, "Reload Family", ct);
        }

        public Task<object> CreateGroupAsync(CreateGroupRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                if (request.ElementIds.Count == 0)
                    throw new ArgumentException("'elementIds' must contain at least one element.");
                Group group;

                using (var tx = new Transaction(doc, "RevitMCP: Create Group"))
                {
                    tx.Start();
                    var ids = request.ElementIds.Select(raw => ResolveRequiredElementId(doc, raw, "elementIds")).ToList();
                    group = doc.Create.NewGroup(ids);
                    if (!string.IsNullOrWhiteSpace(request.Name))
                        group.GroupType.Name = request.Name.Trim();
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    group = BuildGroupInfo(group),
                    elementId = group.Id.ToString(),
                    message = $"Group '{group.GroupType.Name}' created successfully."
                });
            }, ct);
        }

        public Task<object> UpdateGroupAsync(UpdateGroupRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var group = ResolveElement(doc, request.GroupId, "groupId") as Group
                    ?? throw new InvalidOperationException($"Group id '{request.GroupId}' was not found.");
                var changed = new List<string>();

                using (var tx = new Transaction(doc, "RevitMCP: Update Group"))
                {
                    tx.Start();
                    if (!string.IsNullOrWhiteSpace(request.Name) &&
                        !string.Equals(group.GroupType.Name, request.Name.Trim(), StringComparison.Ordinal))
                    {
                        group.GroupType.Name = request.Name.Trim();
                        changed.Add("name");
                    }
                    foreach (var (name, value) in request.Parameters)
                        TrySetParameter(group, name, value, "instance");
                    if (request.Parameters.Count > 0)
                        changed.Add("parameters");
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    group = BuildGroupInfo(group),
                    changed,
                    message = $"Group {group.Id} updated successfully."
                });
            }, ct);
        }

        public Task<object> CreateAssemblyAsync(CreateAssemblyRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                if (request.ElementIds.Count == 0)
                    throw new ArgumentException("'elementIds' must contain at least one element.");
                AssemblyInstance assembly;

                using (var tx = new Transaction(doc, "RevitMCP: Create Assembly"))
                {
                    tx.Start();
                    var ids = request.ElementIds.Select(raw => ResolveRequiredElementId(doc, raw, "elementIds")).ToList();
                    var namingCategoryId = ResolveAssemblyNamingCategoryId(doc, ids, request.NamingCategoryId);
                    assembly = AssemblyInstance.Create(doc, ids, namingCategoryId);
                    if (!string.IsNullOrWhiteSpace(request.Name))
                        assembly.AssemblyTypeName = request.Name.Trim();
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    assembly = BuildAssemblyInfo(assembly),
                    elementId = assembly.Id.ToString(),
                    message = $"Assembly '{assembly.AssemblyTypeName}' created successfully."
                });
            }, ct);
        }

        public Task<object> UpdateAssemblyAsync(UpdateAssemblyRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var assembly = ResolveElement(doc, request.AssemblyId, "assemblyId") as AssemblyInstance
                    ?? throw new InvalidOperationException($"Assembly id '{request.AssemblyId}' was not found.");
                var changed = new List<string>();

                using (var tx = new Transaction(doc, "RevitMCP: Update Assembly"))
                {
                    tx.Start();
                    if (!string.IsNullOrWhiteSpace(request.Name) &&
                        !string.Equals(assembly.AssemblyTypeName, request.Name.Trim(), StringComparison.Ordinal))
                    {
                        assembly.AssemblyTypeName = request.Name.Trim();
                        changed.Add("name");
                    }
                    if (request.AddElementIds.Count > 0)
                    {
                        assembly.AddMemberIds(request.AddElementIds.Select(raw => ResolveRequiredElementId(doc, raw, "addElementIds")).ToList());
                        changed.Add("addElementIds");
                    }
                    if (request.RemoveElementIds.Count > 0)
                    {
                        assembly.RemoveMemberIds(request.RemoveElementIds.Select(raw => ResolveRequiredElementId(doc, raw, "removeElementIds")).ToList());
                        changed.Add("removeElementIds");
                    }
                    foreach (var (name, value) in request.Parameters)
                        TrySetParameter(assembly, name, value, "instance");
                    if (request.Parameters.Count > 0)
                        changed.Add("parameters");
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    assembly = BuildAssemblyInfo(assembly),
                    changed,
                    message = $"Assembly {assembly.Id} updated successfully."
                });
            }, ct);
        }

        public Task<object> ReloadRevitLinkAsync(ReloadRevitLinkRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var linkType = ResolveRevitLinkType(doc, request);
                object? reloadResult = null;

                using (var tx = new Transaction(doc, "RevitMCP: Reload Revit Link"))
                {
                    tx.Start();
                    reloadResult = linkType.Reload();
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    linkTypeId = linkType.Id.ToString(),
                    name = linkType.Name,
                    reloadResult = reloadResult?.ToString(),
                    message = $"Revit link type '{linkType.Name}' reloaded."
                });
            }, ct, timeoutMs: 120_000);
        }

        public Task<object> ManageWorksetsAsync(ManageWorksetsRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var action = request.Action.Trim().ToLowerInvariant();

                if (action == "list")
                    return Task.FromResult<object>(new
                    {
                        success = true,
                        isWorkshared = doc.IsWorkshared,
                        worksets = ListUserWorksets(doc),
                        message = doc.IsWorkshared ? "Listed user worksets." : "Document is not workshared."
                    });

                if (!doc.IsWorkshared)
                    throw new InvalidOperationException("Document is not workshared.");

                using (var tx = new Transaction(doc, "RevitMCP: Manage Worksets"))
                {
                    tx.Start();
                    if (action == "create")
                    {
                        if (string.IsNullOrWhiteSpace(request.Name))
                            throw new ArgumentException("'name' is required for create.");
                        Workset.Create(doc, request.Name.Trim());
                    }
                    else if (action == "rename")
                    {
                        if (string.IsNullOrWhiteSpace(request.WorksetId))
                            throw new ArgumentException("'worksetId' is required for rename.");
                        if (string.IsNullOrWhiteSpace(request.Name))
                            throw new ArgumentException("'name' is required for rename.");
                        WorksetTable.RenameWorkset(doc, new WorksetId(int.Parse(request.WorksetId.Trim())), request.Name.Trim());
                    }
                    else
                    {
                        throw new ArgumentException("Unsupported action. Use list, create, or rename.");
                    }

                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    isWorkshared = doc.IsWorkshared,
                    worksets = ListUserWorksets(doc),
                    message = $"Workset action '{action}' completed."
                });
            }, ct);
        }

        public Task<object> SetElementDesignOptionAsync(SetElementDesignOptionRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                var element = ResolveElement(doc, request.ElementId, "elementId");
                var designOption = ResolveElement(doc, request.DesignOptionId, "designOptionId") as DesignOption
                    ?? throw new InvalidOperationException($"DesignOption id '{request.DesignOptionId}' was not found.");

                using (var tx = new Transaction(doc, "RevitMCP: Set Element Design Option"))
                {
                    tx.Start();
                    var parameter = element.get_Parameter(BuiltInParameter.DESIGN_OPTION_ID)
                        ?? throw new InvalidOperationException($"Element {element.Id} does not expose a design option parameter.");
                    if (parameter.IsReadOnly)
                        throw new InvalidOperationException($"Element {element.Id} design option parameter is read-only.");
                    parameter.Set(designOption.Id);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    element = BuildElementDetailInfo(element, false, false),
                    designOption = new { id = designOption.Id.ToString(), name = designOption.Name, isPrimary = designOption.IsPrimary },
                    message = $"Element {element.Id} assigned to design option '{designOption.Name}'."
                });
            }, ct);
        }

        private Task<object> LoadFamilyInternalAsync(LoadFamilyRequest request, string title, CancellationToken ct)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");
                if (string.IsNullOrWhiteSpace(request.Path))
                    throw new ArgumentException("'path' is required.");
                var path = request.Path.Trim();
                if (!IOFile.Exists(path))
                    throw new IOFileNotFoundException("Family file was not found.", path);
                Family family;
                bool loaded;

                using (var tx = new Transaction(doc, $"RevitMCP: {title}"))
                {
                    tx.Start();
                    loaded = doc.LoadFamily(path, new FamilyLoadOptions(request.OverwriteExisting, request.OverwriteParameterValues), out family);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                return Task.FromResult<object>(new
                {
                    success = loaded,
                    family = family == null ? null : new { id = family.Id.ToString(), name = family.Name },
                    path,
                    message = loaded ? $"Family '{family.Name}' loaded successfully." : "Family load returned false."
                });
            }, ct, timeoutMs: 120_000);
        }

        private static ElementId ResolveScheduleCategoryId(Document doc, string? rawId, string? category)
        {
            if (!string.IsNullOrWhiteSpace(rawId))
                return ResolveRequiredElementId(doc, rawId, "categoryId");
            if (!string.IsNullOrWhiteSpace(category))
            {
                if (!TryResolveCategory(category, out var bic))
                    throw new ArgumentException($"Unknown or unsupported category: '{category}'.");
                return Category.GetCategory(doc, bic)?.Id
                    ?? throw new InvalidOperationException($"Category '{category}' was not found in the document.");
            }
            throw new ArgumentException("Either 'categoryId' or 'category' is required.");
        }

        private static ViewSchedule ResolveSchedule(Document doc, string? rawId)
        {
            return ResolveElement(doc, rawId, "scheduleId") as ViewSchedule
                ?? throw new InvalidOperationException($"Schedule id '{rawId}' was not found.");
        }

        private static List<string> AddScheduleFields(Document doc, ViewSchedule schedule, IReadOnlyList<string> fieldNames)
        {
            var added = new List<string>();
            if (fieldNames.Count == 0)
                return added;

            var definition = schedule.Definition;
            var existingNames = definition.GetFieldOrder()
                .Cast<ScheduleFieldId>()
                .Select(id => definition.GetField(id).GetName())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var fields = definition.GetSchedulableFields();
            foreach (var requested in fieldNames.Where(f => !string.IsNullOrWhiteSpace(f)))
            {
                var fieldName = requested.Trim();
                if (existingNames.Contains(fieldName))
                    continue;
                var match = fields.FirstOrDefault(field =>
                    string.Equals(field.GetName(doc), fieldName, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    throw new InvalidOperationException($"Schedulable field '{fieldName}' was not found for schedule '{schedule.Name}'.");
                definition.AddField(match);
                existingNames.Add(fieldName);
                added.Add(fieldName);
            }

            return added;
        }

        private static object BuildScheduleInfo(ViewSchedule schedule)
        {
            var definition = schedule.Definition;
            var fields = definition.GetFieldOrder()
                .Cast<ScheduleFieldId>()
                .Select(id =>
                {
                    var field = definition.GetField(id);
                    return new { id = id.ToString(), name = field.GetName(), heading = field.ColumnHeading };
                })
                .ToList();

            return new
            {
                id = schedule.Id.ToString(),
                name = schedule.Name,
                categoryId = definition.CategoryId == ElementId.InvalidElementId ? null : definition.CategoryId.ToString(),
                fieldCount = fields.Count,
                fields
            };
        }

        private static List<List<string>> ReadScheduleSection(ViewSchedule schedule, SectionType sectionType, int maxRows, int maxColumns)
        {
            var section = schedule.GetTableData().GetSectionData(sectionType);
            var rows = new List<List<string>>();
            var rowCount = Math.Min(section.NumberOfRows, maxRows);
            var columnCount = Math.Min(section.NumberOfColumns, maxColumns);
            for (var r = 0; r < rowCount; r++)
            {
                var row = new List<string>();
                for (var c = 0; c < columnCount; c++)
                    row.Add(schedule.GetCellText(sectionType, r, c));
                rows.Add(row);
            }
            return rows;
        }

        private static string ResolveOutputFolder(Document doc, string? outputFolder)
        {
            var folder = outputFolder;
            if (string.IsNullOrWhiteSpace(folder))
                folder = string.IsNullOrWhiteSpace(doc.PathName)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                    : IOPath.GetDirectoryName(doc.PathName);
            if (string.IsNullOrWhiteSpace(folder))
                throw new InvalidOperationException("Could not resolve an output folder.");
            IODirectory.CreateDirectory(folder);
            return folder;
        }

        private static object BuildGroupInfo(Group group)
        {
            return new
            {
                id = group.Id.ToString(),
                name = group.GroupType.Name,
                typeId = group.GetTypeId().ToString(),
                memberIds = group.GetMemberIds().Select(id => id.ToString()).ToList()
            };
        }

        private static ElementId ResolveAssemblyNamingCategoryId(Document doc, IReadOnlyList<ElementId> memberIds, string? rawId)
        {
            if (!string.IsNullOrWhiteSpace(rawId))
                return ResolveRequiredElementId(doc, rawId, "namingCategoryId");
            foreach (var id in memberIds)
            {
                var element = doc.GetElement(id);
                if (element?.Category?.Id != null)
                    return element.Category.Id;
            }
            throw new InvalidOperationException("Could not infer an assembly naming category from the requested elements.");
        }

        private static object BuildAssemblyInfo(AssemblyInstance assembly)
        {
            return new
            {
                id = assembly.Id.ToString(),
                name = assembly.AssemblyTypeName,
                memberIds = assembly.GetMemberIds().Select(id => id.ToString()).ToList(),
                namingCategoryId = assembly.NamingCategoryId.ToString()
            };
        }

        private static RevitLinkType ResolveRevitLinkType(Document doc, ReloadRevitLinkRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.LinkTypeId))
                return ResolveElement(doc, request.LinkTypeId, "linkTypeId") as RevitLinkType
                    ?? throw new InvalidOperationException($"RevitLinkType id '{request.LinkTypeId}' was not found.");

            if (!string.IsNullOrWhiteSpace(request.LinkInstanceId))
            {
                var instance = ResolveElement(doc, request.LinkInstanceId, "linkInstanceId") as RevitLinkInstance
                    ?? throw new InvalidOperationException($"RevitLinkInstance id '{request.LinkInstanceId}' was not found.");
                return doc.GetElement(instance.GetTypeId()) as RevitLinkType
                    ?? throw new InvalidOperationException($"Could not resolve link type from instance '{request.LinkInstanceId}'.");
            }

            throw new ArgumentException("Either 'linkTypeId' or 'linkInstanceId' is required.");
        }

        private static List<object> ListUserWorksets(Document doc)
        {
            if (!doc.IsWorkshared)
                return new List<object>();

            return new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .Cast<Workset>()
                .OrderBy(w => w.Name)
                .Select(w => (object)new
                {
                    id = w.Id.ToString(),
                    name = w.Name,
                    kind = w.Kind.ToString(),
                    isOpen = w.IsOpen,
                    isVisibleByDefault = w.IsVisibleByDefault
                })
                .ToList();
        }

        private sealed class FamilyLoadOptions : IFamilyLoadOptions
        {
            private readonly bool _overwriteExisting;
            private readonly bool _overwriteParameterValues;

            public FamilyLoadOptions(bool overwriteExisting, bool overwriteParameterValues)
            {
                _overwriteExisting = overwriteExisting;
                _overwriteParameterValues = overwriteParameterValues;
            }

            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            {
                overwriteParameterValues = _overwriteParameterValues;
                return _overwriteExisting;
            }

            public bool OnSharedFamilyFound(
                Family sharedFamily,
                bool familyInUse,
                out FamilySource source,
                out bool overwriteParameterValues)
            {
                source = FamilySource.Family;
                overwriteParameterValues = _overwriteParameterValues;
                return _overwriteExisting;
            }
        }
    }
}
