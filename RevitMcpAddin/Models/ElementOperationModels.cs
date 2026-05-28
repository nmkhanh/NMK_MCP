using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMcpAddin.Models
{
    public sealed class BoundingBoxInfo
    {
        [JsonProperty("min")]
        public PointInfo? Min { get; set; }

        [JsonProperty("max")]
        public PointInfo? Max { get; set; }
    }

    public sealed class ElementDetailInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("uniqueId")]
        public string UniqueId { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("className")]
        public string ClassName { get; set; } = string.Empty;

        [JsonProperty("category")]
        public string Category { get; set; } = string.Empty;

        [JsonProperty("categoryId")]
        public string? CategoryId { get; set; }

        [JsonProperty("typeId")]
        public string? TypeId { get; set; }

        [JsonProperty("typeName")]
        public string? TypeName { get; set; }

        [JsonProperty("levelId")]
        public string? LevelId { get; set; }

        [JsonProperty("worksetId")]
        public string? WorksetId { get; set; }

        [JsonProperty("isPinned")]
        public bool IsPinned { get; set; }

        [JsonProperty("isElementType")]
        public bool IsElementType { get; set; }

        [JsonProperty("boundingBox")]
        public BoundingBoxInfo? BoundingBox { get; set; }

        [JsonProperty("parameters")]
        public object? Parameters { get; set; }
    }

    public sealed class GetElementRequest
    {
        [JsonProperty("elementId")]
        public string? ElementId { get; set; }

        [JsonProperty("includeParameters")]
        public bool IncludeParameters { get; set; }

        [JsonProperty("includeTypeParameters")]
        public bool IncludeTypeParameters { get; set; } = true;
    }

    public sealed class ElementLocationRequest
    {
        [JsonProperty("elementId")]
        public string? ElementId { get; set; }
    }

    public sealed class ElementGeometrySummaryRequest
    {
        [JsonProperty("elementId")]
        public string? ElementId { get; set; }

        [JsonProperty("detailLevel")]
        public string DetailLevel { get; set; } = "Medium";

        [JsonProperty("includeNonVisibleObjects")]
        public bool IncludeNonVisibleObjects { get; set; }
    }

    public sealed class UpdateElementRequest
    {
        [JsonProperty("elementId")]
        public string? ElementId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("typeId")]
        public string? TypeId { get; set; }

        [JsonProperty("pinned")]
        public bool? Pinned { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();

        [JsonProperty("parameterTarget")]
        public string ParameterTarget { get; set; } = "instance";
    }

    public sealed class ChangeElementTypeRequest
    {
        [JsonProperty("elementId")]
        public string? ElementId { get; set; }

        [JsonProperty("typeId")]
        public string? TypeId { get; set; }
    }

    public sealed class CreateFamilyInstanceRequest
    {
        [JsonProperty("symbolId")]
        public string? SymbolId { get; set; }

        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("z")]
        public double Z { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";

        [JsonProperty("levelId")]
        public string? LevelId { get; set; }

        [JsonProperty("hostId")]
        public string? HostId { get; set; }

        [JsonProperty("structuralType")]
        public string StructuralType { get; set; } = "NonStructural";

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public class ElementIdsRequest
    {
        [JsonProperty("elementIds")]
        public List<string> ElementIds { get; set; } = new();

        [JsonProperty("dryRun")]
        public bool DryRun { get; set; }

        [JsonProperty("maxItems")]
        public int MaxItems { get; set; } = 100;
    }

    public sealed class MoveElementsRequest : ElementIdsRequest
    {
        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("z")]
        public double Z { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";
    }

    public sealed class RotateElementsRequest : ElementIdsRequest
    {
        [JsonProperty("originX")]
        public double OriginX { get; set; }

        [JsonProperty("originY")]
        public double OriginY { get; set; }

        [JsonProperty("originZ")]
        public double OriginZ { get; set; }

        [JsonProperty("axisX")]
        public double AxisX { get; set; }

        [JsonProperty("axisY")]
        public double AxisY { get; set; }

        [JsonProperty("axisZ")]
        public double AxisZ { get; set; } = 1;

        [JsonProperty("angleDegrees")]
        public double AngleDegrees { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";
    }

    public sealed class MirrorElementsRequest : ElementIdsRequest
    {
        [JsonProperty("originX")]
        public double OriginX { get; set; }

        [JsonProperty("originY")]
        public double OriginY { get; set; }

        [JsonProperty("originZ")]
        public double OriginZ { get; set; }

        [JsonProperty("normalX")]
        public double NormalX { get; set; } = 1;

        [JsonProperty("normalY")]
        public double NormalY { get; set; }

        [JsonProperty("normalZ")]
        public double NormalZ { get; set; }

        [JsonProperty("copy")]
        public bool Copy { get; set; } = true;

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";
    }

    public sealed class ViewElementIdsRequest : ElementIdsRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }
    }

    public sealed class BatchOperationItem
    {
        [JsonProperty("operation")]
        public string? Operation { get; set; }

        [JsonProperty("arguments")]
        public JObject? Arguments { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken>? ExtraArguments { get; set; }
    }

    public sealed class BatchOperationsRequest
    {
        [JsonProperty("operations")]
        public List<BatchOperationItem> Operations { get; set; } = new();

        [JsonProperty("dryRun")]
        public bool DryRun { get; set; }

        [JsonProperty("continueOnError")]
        public bool ContinueOnError { get; set; }

        [JsonProperty("maxOperations")]
        public int MaxOperations { get; set; } = 25;
    }
}
