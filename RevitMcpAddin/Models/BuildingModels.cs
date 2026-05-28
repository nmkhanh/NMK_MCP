using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMcpAddin.Models
{
    public sealed class BoundaryPointRequest
    {
        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("z")]
        public double Z { get; set; }
    }

    public sealed class CreateFloorRequest
    {
        [JsonProperty("levelId")]
        public string? LevelId { get; set; }

        [JsonProperty("typeId")]
        public string? TypeId { get; set; }

        [JsonProperty("points")]
        public List<BoundaryPointRequest> Points { get; set; } = new();

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public sealed class CreateCeilingRequest
    {
        [JsonProperty("levelId")]
        public string? LevelId { get; set; }

        [JsonProperty("typeId")]
        public string? TypeId { get; set; }

        [JsonProperty("points")]
        public List<BoundaryPointRequest> Points { get; set; } = new();

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public sealed class UpdateBuildingElementRequest
    {
        [JsonProperty("elementId")]
        public string? ElementId { get; set; }

        [JsonProperty("typeId")]
        public string? TypeId { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public sealed class CreateRoomRequest
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

    public sealed class UpdateRoomRequest
    {
        [JsonProperty("roomId")]
        public string? RoomId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("number")]
        public string? Number { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }
}
