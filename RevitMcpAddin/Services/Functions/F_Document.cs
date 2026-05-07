using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcpAddin.Models;

namespace RevitMcpAddin.Services
{
    // ═══════════════════════════════════════════════════════════════════════
    //  F_Document — Document information queries
    //  Part of the RevitService partial class.
    // ═══════════════════════════════════════════════════════════════════════

    public sealed partial class RevitService
    {
        /// <summary>Returns metadata about the active Revit document.</summary>
        public Task<DocumentInfo> GetDocumentInfoAsync(CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(async uiApp =>
            {
                var uidoc = uiApp.ActiveUIDocument
                    ?? throw new InvalidOperationException("No active UIDocument.");
                var doc = uidoc.Document;

                var info = new DocumentInfo
                {
                    Title          = doc.Title,
                    FilePath       = string.IsNullOrEmpty(doc.PathName) ? "(unsaved)" : doc.PathName,
                    IsModified     = doc.IsModified,
                    IsWorkshared   = doc.IsWorkshared,
                    ActiveViewName = uidoc.ActiveView?.Name     ?? "None",
                    ActiveViewType = uidoc.ActiveView?.ViewType.ToString() ?? "None",
                    ElementCount   = new FilteredElementCollector(doc)
                                         .WhereElementIsNotElementType()
                                         .GetElementCount(),
                    RevitVersion   = uiApp.Application.VersionName
                };

                return await Task.FromResult(info);
            }, ct);
        }
    }
}
