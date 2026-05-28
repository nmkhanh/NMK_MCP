using Newtonsoft.Json;

namespace RevitMcpAddin.Models
{
    public sealed class GridInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("curveType")]
        public string CurveType { get; set; } = string.Empty;

        [JsonProperty("start")]
        public PointInfo? Start { get; set; }

        [JsonProperty("end")]
        public PointInfo? End { get; set; }

        [JsonProperty("center")]
        public PointInfo? Center { get; set; }

        [JsonProperty("radius")]
        public double? Radius { get; set; }
    }

    public sealed class PointInfo
    {
        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("z")]
        public double Z { get; set; }
    }

    public sealed class CreateGridRequest
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("startX")]
        public double StartX { get; set; }

        [JsonProperty("startY")]
        public double StartY { get; set; }

        [JsonProperty("endX")]
        public double EndX { get; set; }

        [JsonProperty("endY")]
        public double EndY { get; set; }

        [JsonProperty("z")]
        public double Z { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";
    }

    public sealed class UpdateGridRequest
    {
        [JsonProperty("gridId")]
        public string? GridId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }
    }
}
