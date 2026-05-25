using Newtonsoft.Json;

namespace RevitMcpAddin.Models
{
    /// <summary>
    /// Input parameters for the <c>export_sheet_to_cad</c> MCP tool.
    /// </summary>
    public class ExportCadRequest
    {
        /// <summary>
        /// Revit element ID of the ViewSheet to export (e.g. "123456").
        /// Required — if null or empty, the request is rejected.
        /// </summary>
        [JsonProperty("sheetId")]
        public string? SheetId { get; set; }

        /// <summary>
        /// Absolute folder path where DWG/DXF file(s) will be written.
        /// Defaults to the document's own folder, or Desktop if the document is unsaved.
        /// </summary>
        [JsonProperty("outputFolder")]
        public string? OutputFolder { get; set; }

        /// <summary>
        /// Name of the named export setting (template) stored in the Revit file.
        /// If omitted or not found, Revit's default DWG export options are used.
        /// Use list_cad_export_templates to discover available template names.
        /// </summary>
        [JsonProperty("templateName")]
        public string? TemplateName { get; set; }

        /// <summary>
        /// Output file format.
        /// Accepted values: "DWG" (default), "DXF".
        /// </summary>
        [JsonProperty("fileFormat")]
        public string? FileFormat { get; set; } = "DWG";
    }
}
