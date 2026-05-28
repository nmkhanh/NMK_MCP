using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMcpAddin.Models
{
    public sealed class RgbColorRequest
    {
        [JsonProperty("r")]
        public int R { get; set; }

        [JsonProperty("g")]
        public int G { get; set; }

        [JsonProperty("b")]
        public int B { get; set; }
    }

    public sealed class CreateMaterialRequest
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("color")]
        public RgbColorRequest? Color { get; set; }

        [JsonProperty("transparency")]
        public int? Transparency { get; set; }
    }

    public sealed class UpdateMaterialRequest
    {
        [JsonProperty("materialId")]
        public string? MaterialId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("newName")]
        public string? NewName { get; set; }

        [JsonProperty("color")]
        public RgbColorRequest? Color { get; set; }

        [JsonProperty("transparency")]
        public int? Transparency { get; set; }
    }

    public sealed class DuplicateElementTypeRequest
    {
        [JsonProperty("typeId")]
        public string? TypeId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }
    }

    public sealed class RenameElementTypeRequest
    {
        [JsonProperty("typeId")]
        public string? TypeId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }
    }

    public sealed class SetTypeParameterRequest
    {
        [JsonProperty("typeId")]
        public string? TypeId { get; set; }

        [JsonProperty("parameterName")]
        public string? ParameterName { get; set; }

        [JsonProperty("value")]
        public JToken? Value { get; set; }
    }

    public sealed class SetTypeParametersRequest
    {
        [JsonProperty("typeId")]
        public string? TypeId { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public sealed class ApplyViewTemplateRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("viewTemplateId")]
        public string? ViewTemplateId { get; set; }
    }

    public sealed class CreateViewTemplateRequest
    {
        [JsonProperty("sourceViewId")]
        public string? SourceViewId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }
    }

    public sealed class CreateTextNoteRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("text")]
        public string? Text { get; set; }

        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("z")]
        public double Z { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";

        [JsonProperty("textNoteTypeId")]
        public string? TextNoteTypeId { get; set; }
    }

    public sealed class UpdateTextNoteRequest
    {
        [JsonProperty("textNoteId")]
        public string? TextNoteId { get; set; }

        [JsonProperty("text")]
        public string? Text { get; set; }

        [JsonProperty("x")]
        public double? X { get; set; }

        [JsonProperty("y")]
        public double? Y { get; set; }

        [JsonProperty("z")]
        public double? Z { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";

        [JsonProperty("textNoteTypeId")]
        public string? TextNoteTypeId { get; set; }
    }

    public sealed class CreateLineRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

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
    }
}
