using Newtonsoft.Json;

namespace RevitMcpAddin.Models
{
    /// <summary>
    /// Summary information about the active Revit document.
    /// Returned by the <c>get_active_document</c> MCP tool.
    /// </summary>
    public class DocumentInfo
    {
        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("filePath")]
        public string FilePath { get; set; } = string.Empty;

        [JsonProperty("isModified")]
        public bool IsModified { get; set; }

        [JsonProperty("isWorkshared")]
        public bool IsWorkshared { get; set; }

        [JsonProperty("activeViewName")]
        public string ActiveViewName { get; set; } = string.Empty;

        [JsonProperty("activeViewType")]
        public string ActiveViewType { get; set; } = string.Empty;

        [JsonProperty("elementCount")]
        public int ElementCount { get; set; }

        [JsonProperty("revitVersion")]
        public string RevitVersion { get; set; } = string.Empty;
    }
}
