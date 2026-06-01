using Newtonsoft.Json;

namespace RevitMcpAddin.Models
{
    public sealed class GraphicOverrideRequest
    {
        [JsonProperty("projectionLineColor")]
        public string? ProjectionLineColor { get; set; }

        [JsonProperty("projectionLinePatternId")]
        public string? ProjectionLinePatternId { get; set; }

        [JsonProperty("projectionLineWeight")]
        public int? ProjectionLineWeight { get; set; }

        [JsonProperty("cutLineColor")]
        public string? CutLineColor { get; set; }

        [JsonProperty("cutLinePatternId")]
        public string? CutLinePatternId { get; set; }

        [JsonProperty("cutLineWeight")]
        public int? CutLineWeight { get; set; }

        [JsonProperty("surfaceForegroundPatternId")]
        public string? SurfaceForegroundPatternId { get; set; }

        [JsonProperty("surfaceForegroundPatternColor")]
        public string? SurfaceForegroundPatternColor { get; set; }

        [JsonProperty("surfaceForegroundPatternVisible")]
        public bool? SurfaceForegroundPatternVisible { get; set; }

        [JsonProperty("surfaceBackgroundPatternId")]
        public string? SurfaceBackgroundPatternId { get; set; }

        [JsonProperty("surfaceBackgroundPatternColor")]
        public string? SurfaceBackgroundPatternColor { get; set; }

        [JsonProperty("surfaceBackgroundPatternVisible")]
        public bool? SurfaceBackgroundPatternVisible { get; set; }

        [JsonProperty("cutForegroundPatternId")]
        public string? CutForegroundPatternId { get; set; }

        [JsonProperty("cutForegroundPatternColor")]
        public string? CutForegroundPatternColor { get; set; }

        [JsonProperty("cutForegroundPatternVisible")]
        public bool? CutForegroundPatternVisible { get; set; }

        [JsonProperty("cutBackgroundPatternId")]
        public string? CutBackgroundPatternId { get; set; }

        [JsonProperty("cutBackgroundPatternColor")]
        public string? CutBackgroundPatternColor { get; set; }

        [JsonProperty("cutBackgroundPatternVisible")]
        public bool? CutBackgroundPatternVisible { get; set; }

        [JsonProperty("transparency")]
        public int? Transparency { get; set; }

        [JsonProperty("halftone")]
        public bool? Halftone { get; set; }

        [JsonProperty("detailLevel")]
        public string? DetailLevel { get; set; }
    }

    public sealed class GetViewOverridesRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("elementIds")]
        public List<string> ElementIds { get; set; } = new();

        [JsonProperty("categoryIds")]
        public List<string> CategoryIds { get; set; } = new();

        [JsonProperty("categories")]
        public List<string> Categories { get; set; } = new();

        [JsonProperty("filterIds")]
        public List<string> FilterIds { get; set; } = new();

        [JsonProperty("includeDefaults")]
        public bool IncludeDefaults { get; set; }
    }

    public sealed class ElementOverridesInViewRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("elementIds")]
        public List<string> ElementIds { get; set; } = new();

        [JsonProperty("overrides")]
        public GraphicOverrideRequest Overrides { get; set; } = new();

        [JsonProperty("dryRun")]
        public bool DryRun { get; set; }

        [JsonProperty("maxItems")]
        public int MaxItems { get; set; } = 100;
    }

    public sealed class CategoryOverrideInViewRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("categoryId")]
        public string? CategoryId { get; set; }

        [JsonProperty("category")]
        public string? Category { get; set; }

        [JsonProperty("overrides")]
        public GraphicOverrideRequest Overrides { get; set; } = new();
    }

    public sealed class CategoryVisibilityInViewRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("categoryId")]
        public string? CategoryId { get; set; }

        [JsonProperty("category")]
        public string? Category { get; set; }

        [JsonProperty("visible")]
        public bool Visible { get; set; } = true;
    }

    public sealed class FilterOverrideInViewRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("filterId")]
        public string? FilterId { get; set; }

        [JsonProperty("overrides")]
        public GraphicOverrideRequest Overrides { get; set; } = new();

        [JsonProperty("visible")]
        public bool? Visible { get; set; }

        [JsonProperty("enabled")]
        public bool? Enabled { get; set; }
    }

    public sealed class FilterInViewRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("filterId")]
        public string? FilterId { get; set; }

        [JsonProperty("overrides")]
        public GraphicOverrideRequest? Overrides { get; set; }

        [JsonProperty("visible")]
        public bool? Visible { get; set; }

        [JsonProperty("enabled")]
        public bool? Enabled { get; set; }
    }

    public sealed class ViewDetailGraphicsRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("detailLevel")]
        public string? DetailLevel { get; set; }

        [JsonProperty("displayStyle")]
        public string? DisplayStyle { get; set; }

        [JsonProperty("partsVisibility")]
        public string? PartsVisibility { get; set; }

        [JsonProperty("discipline")]
        public string? Discipline { get; set; }
    }

    public sealed class ViewOverridePresetRequest
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("overrides")]
        public GraphicOverrideRequest Overrides { get; set; } = new();
    }

    public sealed class ApplyViewOverridePresetRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("targetType")]
        public string? TargetType { get; set; }

        [JsonProperty("targetIds")]
        public List<string> TargetIds { get; set; } = new();

        [JsonProperty("presetName")]
        public string? PresetName { get; set; }

        [JsonProperty("visible")]
        public bool? Visible { get; set; }

        [JsonProperty("enabled")]
        public bool? Enabled { get; set; }

        [JsonProperty("maxItems")]
        public int MaxItems { get; set; } = 100;
    }
}
