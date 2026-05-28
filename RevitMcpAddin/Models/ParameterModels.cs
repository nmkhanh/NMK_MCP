using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMcpAddin.Models
{
    public sealed class RevitParameterInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("storageType")]
        public string StorageType { get; set; } = string.Empty;

        [JsonProperty("isReadOnly")]
        public bool IsReadOnly { get; set; }

        [JsonProperty("isShared")]
        public bool IsShared { get; set; }

        [JsonProperty("hasValue")]
        public bool HasValue { get; set; }

        [JsonProperty("value")]
        public object? Value { get; set; }

        [JsonProperty("displayValue")]
        public string DisplayValue { get; set; } = string.Empty;

        [JsonProperty("source")]
        public string Source { get; set; } = "instance";
    }

    public sealed class GetElementParametersRequest
    {
        [JsonProperty("elementId")]
        public string? ElementId { get; set; }

        [JsonProperty("includeTypeParameters")]
        public bool IncludeTypeParameters { get; set; } = true;
    }

    public sealed class SetElementParameterRequest
    {
        [JsonProperty("elementId")]
        public string? ElementId { get; set; }

        [JsonProperty("parameterName")]
        public string? ParameterName { get; set; }

        [JsonProperty("value")]
        public JToken? Value { get; set; }

        [JsonProperty("target")]
        public string Target { get; set; } = "instance";
    }

    public sealed class SetElementParametersRequest
    {
        [JsonProperty("elementId")]
        public string? ElementId { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();

        [JsonProperty("target")]
        public string Target { get; set; } = "instance";
    }

    public sealed class BatchSetParametersRequest
    {
        [JsonProperty("elementIds")]
        public List<string> ElementIds { get; set; } = new();

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();

        [JsonProperty("target")]
        public string Target { get; set; } = "instance";

        [JsonProperty("dryRun")]
        public bool DryRun { get; set; }

        [JsonProperty("maxItems")]
        public int MaxItems { get; set; } = 100;
    }
}
