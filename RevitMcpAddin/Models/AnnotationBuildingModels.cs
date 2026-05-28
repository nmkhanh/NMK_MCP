using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMcpAddin.Models
{
    public sealed class CreateRoofRequest
    {
        [JsonProperty("levelId")]
        public string? LevelId { get; set; }

        [JsonProperty("roofTypeId")]
        public string? RoofTypeId { get; set; }

        [JsonProperty("points")]
        public List<BoundaryPointRequest> Points { get; set; } = new();

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public sealed class CreateOpeningRequest
    {
        [JsonProperty("hostId")]
        public string? HostId { get; set; }

        [JsonProperty("minX")]
        public double MinX { get; set; }

        [JsonProperty("minY")]
        public double MinY { get; set; }

        [JsonProperty("minZ")]
        public double MinZ { get; set; }

        [JsonProperty("maxX")]
        public double MaxX { get; set; }

        [JsonProperty("maxY")]
        public double MaxY { get; set; }

        [JsonProperty("maxZ")]
        public double MaxZ { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public sealed class CreateAreaRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("number")]
        public string? Number { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public sealed class CreateSpaceRequest
    {
        [JsonProperty("levelId")]
        public string? LevelId { get; set; }

        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("number")]
        public string? Number { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public sealed class UpdateSpatialElementRequest
    {
        [JsonProperty("elementId")]
        public string? ElementId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("number")]
        public string? Number { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public sealed class CreateTagRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("elementId")]
        public string? ElementId { get; set; }

        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("z")]
        public double Z { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";

        [JsonProperty("tagTypeId")]
        public string? TagTypeId { get; set; }

        [JsonProperty("addLeader")]
        public bool AddLeader { get; set; }
    }

    public sealed class CreateDimensionRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("elementIds")]
        public List<string> ElementIds { get; set; } = new();

        [JsonProperty("startX")]
        public double StartX { get; set; }

        [JsonProperty("startY")]
        public double StartY { get; set; }

        [JsonProperty("startZ")]
        public double StartZ { get; set; }

        [JsonProperty("endX")]
        public double EndX { get; set; }

        [JsonProperty("endY")]
        public double EndY { get; set; }

        [JsonProperty("endZ")]
        public double EndZ { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";

        [JsonProperty("dimensionTypeId")]
        public string? DimensionTypeId { get; set; }
    }

    public sealed class CreateFilledRegionRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("filledRegionTypeId")]
        public string? FilledRegionTypeId { get; set; }

        [JsonProperty("points")]
        public List<BoundaryPointRequest> Points { get; set; } = new();

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";
    }
}
