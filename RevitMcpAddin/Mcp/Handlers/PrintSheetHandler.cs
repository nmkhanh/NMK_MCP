using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;

namespace RevitMcpAddin.Mcp.Handlers
{
    /// <summary>
    /// MCP tool: <b>print_sheet_to_pdf</b>
    /// Exports one or more Revit ViewSheets to PDF using a virtual PDF printer (PDF24).
    /// Each sheet is printed as a separate PDF file named after its SheetNumber.
    /// Paper size is auto-detected from the sheet's title block dimensions.
    ///
    /// Input example:
    /// <code>
    /// {
    ///   "sheetIds":    ["123456", "789012"],  // required — Revit element IDs
    ///   "outputFolder": "C:\\Output\\PDFs",    // omit = document folder
    ///   "colorMode":   "Color",               // "Color","GrayScale","BlackAndWhite"
    ///   "rasterQuality": "High"               // "Draft","Low","Medium","High","Presentation"
    /// }
    /// </code>
    ///
    /// Output example:
    /// <code>
    /// {
    ///   "success": true,
    ///   "sheetCount": 2,
    ///   "sheets": [{ "id": "123456", "number": "A-001", "name": "Floor Plan", "file": "C:\\...\\A-001.pdf" }],
    ///   "outputFolder": "C:\\Output\\PDFs"
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
            Description = "Export Revit sheets to individual PDF files using PDF24 virtual printer. " +
                          "Sheets are identified by their Revit element ID (use get_elements with category='Sheets'). " +
                          "Each PDF is named after the sheet's SheetNumber. " +
                          "Paper size is auto-detected from the title block dimensions.",
            InputSchema = new
            {
                type       = "object",
                properties = new
                {
                    sheetIds = new
                    {
                        type        = "array",
                        items       = new { type = "string" },
                        description = "Revit element IDs of the sheets to print (e.g. [\"123456\",\"789012\"]). " +
                                      "Required — use get_elements with category='Sheets' to discover IDs."
                    },
                    outputFolder = new
                    {
                        type        = "string",
                        description = "Absolute path to the output folder. " +
                                      "Defaults to the document's own folder, or Desktop if unsaved."
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
                required = new[] { "sheetIds" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken ct = default)
        {
            try
            {
                var request = new PrintSheetRequest
                {
                    SheetIds      = (arguments?["sheetIds"] as JArray)
                                        ?.Select(t => t.Value<string>()!)
                                        .Where(s => !string.IsNullOrWhiteSpace(s))
                                        .ToList(),
                    OutputFolder   = arguments?["outputFolder"]?.Value<string>(),
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
