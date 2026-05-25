using Newtonsoft.Json;

namespace RevitMcpAddin.Models
{
    /// <summary>
    /// Input parameters for the <c>print_sheet_to_pdf</c> MCP tool.
    /// </summary>
    public class PrintSheetRequest
    {
        /// <summary>
        /// Revit element ID of the ViewSheet to print (e.g. "123456").
        /// Required — if null or empty, the request is rejected and nothing is printed.
        /// </summary>
        [JsonProperty("sheetId")]
        public string? SheetId { get; set; }

        /// <summary>
        /// Absolute folder path where PDF file(s) will be written.
        /// Defaults to the document's own folder, or Desktop if the document is unsaved.
        /// </summary>
        [JsonProperty("outputFolder")]
        public string? OutputFolder { get; set; }

        /// <summary>
        /// Color output mode.
        /// Accepted values: "Color" (default), "GrayScale", "BlackAndWhite".
        /// </summary>
        [JsonProperty("colorMode")]
        public string? ColorMode { get; set; } = "Color";

        /// <summary>
        /// Raster image quality.
        /// Accepted values: "Draft", "Low", "Medium", "High" (default), "Presentation".
        /// </summary>
        [JsonProperty("rasterQuality")]
        public string? RasterQuality { get; set; } = "High";
    }
}
