using Newtonsoft.Json;

namespace RevitMcpAddin.Models
{
    public sealed class ViewInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("viewType")]
        public string ViewType { get; set; } = string.Empty;

        [JsonProperty("isTemplate")]
        public bool IsTemplate { get; set; }

        [JsonProperty("scale")]
        public int Scale { get; set; }

        [JsonProperty("levelId")]
        public string? LevelId { get; set; }

        [JsonProperty("viewTemplateId")]
        public string? ViewTemplateId { get; set; }

        [JsonProperty("canBePrinted")]
        public bool CanBePrinted { get; set; }
    }

    public sealed class CreateViewRequest
    {
        [JsonProperty("viewType")]
        public string ViewType { get; set; } = "floorPlan";

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("levelId")]
        public string? LevelId { get; set; }

        [JsonProperty("viewFamilyTypeId")]
        public string? ViewFamilyTypeId { get; set; }

        [JsonProperty("scale")]
        public int? Scale { get; set; }
    }

    public sealed class DuplicateViewRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("duplicateOption")]
        public string DuplicateOption { get; set; } = "Duplicate";
    }

    public sealed class UpdateViewRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("scale")]
        public int? Scale { get; set; }

        [JsonProperty("viewTemplateId")]
        public string? ViewTemplateId { get; set; }
    }

    public sealed class SheetInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("sheetNumber")]
        public string SheetNumber { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("placedViews")]
        public List<PlacedViewInfo> PlacedViews { get; set; } = new();
    }

    public sealed class PlacedViewInfo
    {
        [JsonProperty("viewportId")]
        public string ViewportId { get; set; } = string.Empty;

        [JsonProperty("viewId")]
        public string ViewId { get; set; } = string.Empty;

        [JsonProperty("viewName")]
        public string ViewName { get; set; } = string.Empty;

        [JsonProperty("viewType")]
        public string ViewType { get; set; } = string.Empty;
    }

    public sealed class CreateSheetRequest
    {
        [JsonProperty("sheetNumber")]
        public string? SheetNumber { get; set; }

        [JsonProperty("sheetName")]
        public string? SheetName { get; set; }

        [JsonProperty("titleBlockTypeId")]
        public string? TitleBlockTypeId { get; set; }

        [JsonProperty("titleBlockTypeName")]
        public string? TitleBlockTypeName { get; set; }
    }

    public sealed class UpdateSheetRequest
    {
        [JsonProperty("sheetId")]
        public string? SheetId { get; set; }

        [JsonProperty("sheetNumber")]
        public string? SheetNumber { get; set; }

        [JsonProperty("sheetName")]
        public string? SheetName { get; set; }
    }

    public sealed class PlaceViewOnSheetRequest
    {
        [JsonProperty("sheetId")]
        public string? SheetId { get; set; }

        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";

        [JsonProperty("viewportTypeId")]
        public string? ViewportTypeId { get; set; }
    }

    public sealed class RemoveViewFromSheetRequest
    {
        [JsonProperty("viewportId")]
        public string? ViewportId { get; set; }

        [JsonProperty("sheetId")]
        public string? SheetId { get; set; }

        [JsonProperty("viewId")]
        public string? ViewId { get; set; }
    }
}
