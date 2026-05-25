using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;

namespace RevitMcpAddin.Mcp.Handlers
{
    /// <summary>
    /// MCP tool: <b>export_sheet_to_cad</b>
    /// Exports one or more Revit ViewSheets to DWG or DXF using a named export
    /// template from the Revit file, or Revit's default options when none is given.
    ///
    /// Input example:
    /// <code>
    /// {
    ///   "sheetIds":    ["123456", "789012"],  // required — Revit element IDs
    ///   "outputFolder": "C:\\Output\\CAD",    // omit = document folder
    ///   "templateName": "My DWG Template",   // named ExportDWGSettings in Revit
    ///   "fileFormat":  "DWG"                 // "DWG" (default) or "DXF"
    /// }
    /// </code>
    ///
    /// Output example:
    /// <code>
    /// {
    ///   "success": true,
    ///   "sheetCount": 2,
    ///   "exportedCount": 2,
    ///   "fileFormat": "DWG",
    ///   "template": "My DWG Template",
    ///   "sheets": [{ "number": "A-001", "name": "Floor Plan", "file": "C:\\...\\A-001.dwg", "exported": true }],
    ///   "outputFolder": "C:\\Output\\CAD"
    /// }
    /// </code>
    /// </summary>
    public sealed class ExportCadHandler : IToolHandler
    {
        #region Fields

        private readonly RevitService _revitService;

        #endregion

        #region Constructor

        public ExportCadHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        #endregion

        #region IToolHandler

        public string ToolName => "export_sheet_to_cad";

        public McpToolDefinition GetDefinition() => new()
        {
            Name        = ToolName,
            Description = "Export a single Revit sheet to a DWG or DXF file. " +
                          "Supports named export templates (ExportDWGSettings) stored in the Revit file. " +
                          "The sheet is identified by its Revit element ID — use get_elements with category='Sheets'. " +
                          "Use list_cad_export_templates to discover available template names. " +
                          "The output file is named after the sheet's SheetNumber.",
            InputSchema = new
            {
                type       = "object",
                properties = new
                {
                    sheetId = new
                    {
                        type        = "string",
                        description = "Revit element ID of the sheet to export (e.g. \"123456\"). " +
                                      "Required — use get_elements with category='Sheets' to discover IDs."
                    },
                    outputFolder = new
                    {
                        type        = "string",
                        description = "Absolute path to the output folder. " +
                                      "Defaults to the document's own folder, or Desktop if unsaved."
                    },
                    templateName = new
                    {
                        type        = "string",
                        description = "Name of a named DWG export template stored in the Revit file. " +
                                      "Use list_cad_export_templates to see available names. " +
                                      "If omitted or not found, Revit default export options are used."
                    },
                    fileFormat = new
                    {
                        type        = "string",
                        description = "Output file format: \"DWG\" (default) or \"DXF\".",
                        @default    = "DWG"
                    }
                },
                required = new[] { "sheetId" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken ct = default)
        {
            try
            {
                var request = new ExportCadRequest
                {
                    SheetId      = arguments?["sheetId"]?.Value<string>(),
                    OutputFolder = arguments?["outputFolder"]?.Value<string>(),
                    TemplateName = arguments?["templateName"]?.Value<string>(),
                    FileFormat   = arguments?["fileFormat"]?.Value<string>() ?? "DWG",
                };

                var result = await _revitService.ExportSheetToCadAsync(request, ct);
                return ToolHandlerResult.FromJson(result);
            }
            catch (ArgumentException ex)
            {
                return ToolHandlerResult.FromError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ToolHandlerResult.FromError(ex.Message);
            }
            catch (Exception ex)
            {
                return ToolHandlerResult.FromError($"Failed to export sheets to CAD: {ex.Message}");
            }
        }

        #endregion
    }

    // ────────────────────────────────────────────────────────────────────────
    //  list_cad_export_templates
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// MCP tool: <b>list_cad_export_templates</b>
    /// Lists all named DWG export templates (ExportDWGSettings) stored in the
    /// active Revit document. Use the returned names with <c>export_sheet_to_cad</c>.
    /// </summary>
    public sealed class ListCadExportTemplatesHandler : IToolHandler
    {
        #region Fields

        private readonly RevitService _revitService;

        #endregion

        #region Constructor

        public ListCadExportTemplatesHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        #endregion

        #region IToolHandler

        public string ToolName => "list_cad_export_templates";

        public McpToolDefinition GetDefinition() => new()
        {
            Name        = ToolName,
            Description = "Lists all named DWG export templates (ExportDWGSettings) stored in the " +
                          "active Revit document. Use the returned template names with export_sheet_to_cad.",
            InputSchema = new
            {
                type       = "object",
                properties = new { },
                required   = new string[] { }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken ct = default)
        {
            try
            {
                var result = await _revitService.ListCadExportTemplatesAsync(ct);
                return ToolHandlerResult.FromJson(result);
            }
            catch (InvalidOperationException ex)
            {
                return ToolHandlerResult.FromError(ex.Message);
            }
            catch (Exception ex)
            {
                return ToolHandlerResult.FromError($"Failed to list CAD export templates: {ex.Message}");
            }
        }

        #endregion
    }
}
