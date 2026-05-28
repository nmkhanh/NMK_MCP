using Newtonsoft.Json;

namespace RevitMcpAddin.Models
{
    public sealed class ViewSheetSetInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("viewCount")]
        public int ViewCount { get; set; }

        [JsonProperty("views")]
        public List<ViewInfo> Views { get; set; } = new();
    }

    public sealed class CreateViewSheetSetRequest
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("viewIds")]
        public List<string> ViewIds { get; set; } = new();

        [JsonProperty("sheetIds")]
        public List<string> SheetIds { get; set; } = new();

        [JsonProperty("replaceExisting")]
        public bool ReplaceExisting { get; set; }
    }

    public sealed class ModifyViewSheetSetRequest
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("viewIds")]
        public List<string> ViewIds { get; set; } = new();

        [JsonProperty("sheetIds")]
        public List<string> SheetIds { get; set; } = new();
    }

    public sealed class DeleteViewSheetSetRequest
    {
        [JsonProperty("name")]
        public string? Name { get; set; }
    }
}
