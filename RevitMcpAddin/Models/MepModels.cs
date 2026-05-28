using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMcpAddin.Models
{
    public sealed class CreateMepCurveRequest
    {
        [JsonProperty("typeId")]
        public string? TypeId { get; set; }

        [JsonProperty("systemTypeId")]
        public string? SystemTypeId { get; set; }

        [JsonProperty("levelId")]
        public string? LevelId { get; set; }

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

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public sealed class UpdateMepCurveRequest
    {
        [JsonProperty("elementId")]
        public string? ElementId { get; set; }

        [JsonProperty("typeId")]
        public string? TypeId { get; set; }

        [JsonProperty("startX")]
        public double? StartX { get; set; }

        [JsonProperty("startY")]
        public double? StartY { get; set; }

        [JsonProperty("startZ")]
        public double? StartZ { get; set; }

        [JsonProperty("endX")]
        public double? EndX { get; set; }

        [JsonProperty("endY")]
        public double? EndY { get; set; }

        [JsonProperty("endZ")]
        public double? EndZ { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public sealed class GetConnectorsRequest
    {
        [JsonProperty("elementId")]
        public string? ElementId { get; set; }
    }

    public sealed class ConnectorRefRequest
    {
        [JsonProperty("elementId")]
        public string? ElementId { get; set; }

        [JsonProperty("connectorIndex")]
        public int? ConnectorIndex { get; set; }

        [JsonProperty("x")]
        public double? X { get; set; }

        [JsonProperty("y")]
        public double? Y { get; set; }

        [JsonProperty("z")]
        public double? Z { get; set; }
    }

    public sealed class ConnectMepElementsRequest
    {
        [JsonProperty("first")]
        public ConnectorRefRequest First { get; set; } = new();

        [JsonProperty("second")]
        public ConnectorRefRequest Second { get; set; } = new();

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";
    }
}
