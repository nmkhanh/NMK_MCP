using Newtonsoft.Json;

namespace RevitMcpAddin.Models
{
    /// <summary>
    /// Input parameters for the <c>create_wall</c> MCP tool.
    /// All coordinates are in Revit internal units (decimal feet).
    /// </summary>
    public class CreateWallRequest
    {
        /// <summary>Start point X (feet)</summary>
        [JsonProperty("startX")]
        public double StartX { get; set; }

        /// <summary>Start point Y (feet)</summary>
        [JsonProperty("startY")]
        public double StartY { get; set; }

        /// <summary>End point X (feet)</summary>
        [JsonProperty("endX")]
        public double EndX { get; set; }

        /// <summary>End point Y (feet)</summary>
        [JsonProperty("endY")]
        public double EndY { get; set; }

        /// <summary>Wall height (feet)</summary>
        [JsonProperty("height")]
        public double Height { get; set; }

        /// <summary>Level name, e.g. "Level 1". Defaults to first available level.</summary>
        [JsonProperty("levelName")]
        public string LevelName { get; set; } = "Level 1";

        /// <summary>Optional wall type name. Uses project default when omitted.</summary>
        [JsonProperty("wallTypeName")]
        public string? WallTypeName { get; set; }
    }

    /// <summary>
    /// Result of the <c>create_wall</c> operation.
    /// </summary>
    public class WallCreationResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("elementId")]
        public string ElementId { get; set; } = string.Empty;

        [JsonProperty("wallTypeName")]
        public string WallTypeName { get; set; } = string.Empty;

        [JsonProperty("levelName")]
        public string LevelName { get; set; } = string.Empty;

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;
    }
}
