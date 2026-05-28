using Newtonsoft.Json;

namespace RevitMcpAddin.Models
{
    public sealed class LevelInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("elevation")]
        public double Elevation { get; set; }

        [JsonProperty("elevationMeters")]
        public double ElevationMeters { get; set; }
    }

    public sealed class CreateLevelRequest
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("elevation")]
        public double Elevation { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";
    }

    public sealed class UpdateLevelRequest
    {
        [JsonProperty("levelId")]
        public string? LevelId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("elevation")]
        public double? Elevation { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";
    }
}
