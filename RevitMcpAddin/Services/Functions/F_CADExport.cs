using System.Diagnostics;
using System.IO;
using Autodesk.Revit.DB;
using RevitMcpAddin.Models;

namespace RevitMcpAddin.Services
{
  // ═══════════════════════════════════════════════════════════════════════
  //  F_CADExport — Sheet to DWG/DXF via Revit Export API
  //  Part of the RevitService partial class.
  //
  //  Exports ViewSheets to DWG or DXF using Revit's built-in Export API.
  //  Supports named export templates (ExportDWGSettings) stored inside
  //  the Revit file. Falls back to default export options when no template
  //  name is given or the named template is not found.
  //
  //  Also exposes a helper to list available template names so the MCP
  //  client can pick one before calling the export tool.
  // ═══════════════════════════════════════════════════════════════════════

  public sealed partial class RevitService
  {
    private const int CadExportTimeoutMs = 180_000; // 3 minutes

    // ── Public methods ────────────────────────────────────────────────

    /// <summary>
    /// Exports one or more ViewSheets to DWG or DXF using a named export
    /// template from the Revit file, or Revit defaults when none is specified.
    /// Each sheet produces one file named after its SheetNumber.
    /// </summary>
    public Task<object> ExportSheetToCadAsync(ExportCadRequest request, CancellationToken ct = default)
    {
      return _queue.EnqueueAsync(async uiApp =>
      {
        var doc = uiApp.ActiveUIDocument?.Document
                  ?? throw new InvalidOperationException("No active document.");

        // ── 1. Resolve output folder ──────────────────────────────
        var outputFolder = string.IsNullOrWhiteSpace(request.OutputFolder)
                  ? (!string.IsNullOrEmpty(doc.PathName)
                      ? Path.GetDirectoryName(doc.PathName)!
                      : Environment.GetFolderPath(Environment.SpecialFolder.Desktop))
                  : request.OutputFolder;
        Directory.CreateDirectory(outputFolder);

        // ── 2. Resolve target sheet by element ID ─────────────────
        if (string.IsNullOrWhiteSpace(request.SheetId))
          return new
          {
            success = false,
            message = "'sheetId' is required. Provide the Revit element ID of the sheet to export. " +
                      "Use get_elements with category='Sheets' to discover sheet IDs."
          };

        var allSheets = new FilteredElementCollector(doc)
                  .OfClass(typeof(ViewSheet))
                  .Cast<ViewSheet>()
                  .Where(s => !s.IsTemplate)
                  .ToDictionary(s => s.Id.ToString(), StringComparer.OrdinalIgnoreCase);

        if (!allSheets.TryGetValue(request.SheetId.Trim(), out var targetSheet))
          return new
          {
            success     = false,
            requestedId = request.SheetId,
            message     = "The specified sheet ID was not found in the active document. " +
                          "Use get_elements with category='Sheets' to get valid element IDs."
          };

        // ── 3. Resolve export format ──────────────────────────────
        var isDxf = string.Equals(
            request.FileFormat?.Trim(), "DXF", StringComparison.OrdinalIgnoreCase);
        var ext = isDxf ? ".dxf" : ".dwg";

        // ── 4. Find named DWG export template (if specified) ──────
        ExportDWGSettings? exportSettings = null;
        var templateName = request.TemplateName?.Trim();
        if (!string.IsNullOrEmpty(templateName))
        {
          exportSettings = new FilteredElementCollector(doc)
                    .OfClass(typeof(ExportDWGSettings))
                    .Cast<ExportDWGSettings>()
                    .FirstOrDefault(s =>
                        string.Equals(s.Name, templateName, StringComparison.OrdinalIgnoreCase));

          if (exportSettings == null)
            Trace.WriteLine(
                $"[RevitMCP][CAD] Template '{templateName}' not found — using default options.");
          else
            Trace.WriteLine($"[RevitMCP][CAD] Using export template: '{exportSettings.Name}'");
        }

        // ── 5. Export the single sheet ────────────────────────────
        var safeName   = SanitizeFileName(targetSheet.SheetNumber);
        var outputFile = Path.Combine(outputFolder, safeName + ext);
        if (File.Exists(outputFile)) File.Delete(outputFile);

        var viewIds = new List<ElementId> { targetSheet.Id };
        bool exported;

        if (isDxf)
        {
          var opts = new DXFExportOptions();
          exported = doc.Export(outputFolder, safeName, viewIds, opts);
        }
        else
        {
          var opts = exportSettings?.GetDWGExportOptions() ?? new DWGExportOptions();
          opts.MergedViews = true;
          exported = doc.Export(outputFolder, safeName, viewIds, opts);
        }

        if (exported)
          Trace.WriteLine($"[RevitMCP][CAD] Sheet {targetSheet.SheetNumber} → {outputFile}");
        else
          Trace.WriteLine($"[RevitMCP][CAD] Export returned false for sheet {targetSheet.SheetNumber}.");

        var usedTemplate = exportSettings?.Name ?? (isDxf ? "(DXF default)" : "(DWG default)");

        return (object)new
        {
          success      = true,
          exported,
          fileFormat   = isDxf ? "DXF" : "DWG",
          template     = usedTemplate,
          sheet        = new
          {
            number   = targetSheet.SheetNumber,
            name     = targetSheet.Name,
            file     = exported ? outputFile : (string?)null,
            exported
          },
          outputFolder,
          outputFile   = exported ? outputFile : (string?)null,
          message      = exported
              ? $"Exported sheet '{targetSheet.SheetNumber}' to {(isDxf ? "DXF" : "DWG")} using template '{usedTemplate}'."
              : $"Export failed for sheet '{targetSheet.SheetNumber}'."
        };

      }, ct, timeoutMs: CadExportTimeoutMs);
    }

    /// <summary>
    /// Returns the names of all named DWG export templates (ExportDWGSettings)
    /// stored in the active Revit document.
    /// </summary>
    public Task<object> ListCadExportTemplatesAsync(CancellationToken ct = default)
    {
      return _queue.EnqueueAsync(async uiApp =>
      {
        var doc = uiApp.ActiveUIDocument?.Document
                  ?? throw new InvalidOperationException("No active document.");

        var templates = new FilteredElementCollector(doc)
                  .OfClass(typeof(ExportDWGSettings))
                  .Cast<ExportDWGSettings>()
                  .Select(s => s.Name)
                  .OrderBy(n => n)
                  .ToList();

        return (object)new
        {
          success       = true,
          templateCount = templates.Count,
          templates,
          message = templates.Count > 0
              ? $"Found {templates.Count} DWG export template(s) in the document."
              : "No named DWG export templates found. A default export will be used."
        };

      }, ct, timeoutMs: 30_000);
    }
  }
}
