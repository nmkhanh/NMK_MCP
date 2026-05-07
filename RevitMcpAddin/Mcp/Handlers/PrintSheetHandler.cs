using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;

namespace RevitMcpAddin.Mcp.Handlers
{
    /// <summary>
    /// MCP tool: <b>print_sheet_to_pdf</b>
    /// Exports one or more Revit ViewSheets to PDF using the native Revit PDF engine.
    /// Paper size is auto-detected from each sheet's title block dimensions
    /// (A0 / A1 / A2 / A3 / A4 / Letter / Tabloid) or can be specified explicitly.
    ///
    /// Input example:
    /// <code>
    /// {
    ///   "sheetNumbers":  ["A-001", "S-101"],  // omit = all sheets
    ///   "outputFolder":  "C:\\Output\\PDFs",   // omit = document folder
    ///   "outputFileName": "MyProject",          // omit = document title (combined PDF only)
    ///   "combine":       true,                  // merge into one PDF (default: true)
    ///   "paperSize":     "Auto",                // "Auto","A0","A1","A2","A3","A4","Letter","Tabloid"
    ///   "colorMode":     "Color",               // "Color","GrayScale","BlackAndWhite"
    ///   "rasterQuality": "High"                 // "Draft","Low","Medium","High","Presentation"
    /// }
    /// </code>
    ///
    /// Output example:
    /// <code>
    /// {
    ///   "success": true,
    ///   "sheetCount": 2,
    ///   "sheets": [{ "number": "A-001", "name": "Floor Plan", "widthMm": 841, "heightMm": 594, "paperSize": "A1" }],
    ///   "outputFolder": "C:\\Output\\PDFs",
    ///   "outputFile": "C:\\Output\\PDFs\\MyProject.pdf",
    ///   "message": "Exported 2 sheet(s) to 'C:\\Output\\PDFs'."
    /// }
    /// </code>
    /// </summary>
    public sealed class PrintSheetHandler : IToolHandler
    {
        #region Fields

        private readonly RevitService _revitService;

        #endregion

        #region Constructor

        public PrintSheetHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        #endregion

        #region IToolHandler

        public string ToolName => "print_sheet_to_pdf";

        public McpToolDefinition GetDefinition() => new()
        {
            Name        = ToolName,
            Description = "Export Revit sheets to PDF using a virtual PDF printer (PDF24 preferred). " +
                          "Paper size is auto-detected per sheet from the title block dimensions " +
                          "(A0–A4, Letter, Tabloid) and matched to the printer's available sizes. " +
                          "Supports combining all sheets into a single PDF or exporting each separately.",
            InputSchema = new
            {
                type       = "object",
                properties = new
                {
                    sheetNumbers = new
                    {
                        type        = "array",
                        items       = new { type = "string" },
                        description = "List of sheet numbers to export (e.g. [\"A-001\",\"S-101\"]). " +
                                      "Omit or pass an empty array to export ALL sheets in the document."
                    },
                    outputFolder = new
                    {
                        type        = "string",
                        description = "Absolute path to the output folder. " +
                                      "Defaults to the document's own folder, or Desktop if unsaved."
                    },
                    outputFileName = new
                    {
                        type        = "string",
                        description = "Base file name (without .pdf extension) for the combined PDF. " +
                                      "Ignored when combine=false. Defaults to the document title."
                    },
                    combine = new
                    {
                        type        = "boolean",
                        description = "true (default) = merge all sheets into one PDF. " +
                                      "false = one PDF file per sheet.",
                        @default    = true
                    },
                    colorMode = new
                    {
                        type        = "string",
                        description = "Color output mode: \"Color\" (default), \"GrayScale\", \"BlackAndWhite\".",
                        @default    = "Color"
                    },
                    rasterQuality = new
                    {
                        type        = "string",
                        description = "Raster image quality: \"Draft\", \"Low\", \"Medium\", \"High\" (default), \"Presentation\".",
                        @default    = "High"
                    }
                },
                required = Array.Empty<string>()
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken ct = default)
        {
            try
            {
                var request = new PrintSheetRequest
                {
                    SheetNumbers  = (arguments?["sheetNumbers"] as JArray)
                                        ?.Select(t => t.Value<string>()!)
                                        .Where(s => !string.IsNullOrWhiteSpace(s))
                                        .ToList(),
                    OutputFolder   = arguments?["outputFolder"]?.Value<string>(),
                    OutputFileName = arguments?["outputFileName"]?.Value<string>(),
                    Combine        = arguments?["combine"]?.Value<bool>()         ?? true,
                    ColorMode      = arguments?["colorMode"]?.Value<string>()     ?? "Color",
                    RasterQuality  = arguments?["rasterQuality"]?.Value<string>() ?? "High",
                };

                var result = await _revitService.PrintSheetToPdfAsync(request, ct);
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
                return ToolHandlerResult.FromError($"Failed to print sheets: {ex.Message}");
            }
        }

        #endregion
    }
}
