using Newtonsoft.Json;

namespace RevitMcpAddin.Models
{
    /// <summary>
    /// Input parameters for the <c>print_sheet_to_pdf</c> MCP tool.
    /// </summary>
    public class PrintSheetRequest
    {
        /// <summary>
        /// Sheet numbers to export (e.g. "A-001", "S-101").
        /// Null or empty list = all sheets in the document.
        /// </summary>
        [JsonProperty("sheetNumbers")]
        public List<string>? SheetNumbers { get; set; }

        /// <summary>
        /// Absolute folder path where PDF file(s) will be written.
        /// Defaults to the document's own folder, or Desktop if the document is unsaved.
        /// </summary>
        [JsonProperty("outputFolder")]
        public string? OutputFolder { get; set; }

        /// <summary>
        /// Base file name (without .pdf extension) used for the combined PDF.
        /// Defaults to the document title.
        /// </summary>
        [JsonProperty("outputFileName")]
        public string? OutputFileName { get; set; }

        /// <summary>
        /// When true (default), all sheets are merged into a single PDF.
        /// When false, each sheet produces its own PDF file.
        /// </summary>
        [JsonProperty("combine")]
        public bool Combine { get; set; } = true;

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
