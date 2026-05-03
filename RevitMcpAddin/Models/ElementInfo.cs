using Newtonsoft.Json;

namespace RevitMcpAddin.Models
{
    /// <summary>
    /// Lightweight representation of a Revit element.
    /// Returned by the <c>get_elements</c> MCP tool.
    /// </summary>
    public class ElementInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("category")]
        public string Category { get; set; } = string.Empty;

        [JsonProperty("levelId")]
        public string? LevelId { get; set; }

        [JsonProperty("familyName")]
        public string FamilyName { get; set; } = string.Empty;

        [JsonProperty("typeName")]
        public string TypeName { get; set; } = string.Empty;

        /// <summary>Key Revit parameters (best-effort extraction).</summary>
        [JsonProperty("parameters")]
        public Dictionary<string, string> Parameters { get; set; } = new();
    }
}
