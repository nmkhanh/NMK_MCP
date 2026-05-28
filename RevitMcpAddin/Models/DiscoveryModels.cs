using Newtonsoft.Json;

namespace RevitMcpAddin.Models
{
    public sealed class CategoryInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("categoryType")]
        public string CategoryType { get; set; } = string.Empty;

        [JsonProperty("allowsBoundParameters")]
        public bool AllowsBoundParameters { get; set; }
    }

    public sealed class ElementTypeSummary
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("familyName")]
        public string FamilyName { get; set; } = string.Empty;

        [JsonProperty("category")]
        public string Category { get; set; } = string.Empty;

        [JsonProperty("className")]
        public string ClassName { get; set; } = string.Empty;

        [JsonProperty("isActive")]
        public bool? IsActive { get; set; }
    }

    public sealed class ListCategoriesRequest
    {
        [JsonProperty("categoryType")]
        public string? CategoryType { get; set; }
    }

    public sealed class ListElementTypesRequest
    {
        [JsonProperty("category")]
        public string? Category { get; set; }

        [JsonProperty("familyName")]
        public string? FamilyName { get; set; }

        [JsonProperty("maxItems")]
        public int MaxItems { get; set; } = 500;
    }

    public sealed class FindElementsByParameterRequest
    {
        [JsonProperty("parameterName")]
        public string? ParameterName { get; set; }

        [JsonProperty("value")]
        public string? Value { get; set; }

        [JsonProperty("category")]
        public string? Category { get; set; }

        [JsonProperty("comparison")]
        public string Comparison { get; set; } = "equals";

        [JsonProperty("includeParameters")]
        public bool IncludeParameters { get; set; }

        [JsonProperty("maxItems")]
        public int MaxItems { get; set; } = 200;
    }
}
