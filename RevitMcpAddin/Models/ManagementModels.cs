using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMcpAddin.Models
{
    public sealed class CreateScheduleRequest
    {
        [JsonProperty("category")]
        public string? Category { get; set; }

        [JsonProperty("categoryId")]
        public string? CategoryId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("fieldNames")]
        public List<string> FieldNames { get; set; } = new();
    }

    public sealed class UpdateScheduleRequest
    {
        [JsonProperty("scheduleId")]
        public string? ScheduleId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("fieldNames")]
        public List<string> FieldNames { get; set; } = new();
    }

    public sealed class GetScheduleDataRequest
    {
        [JsonProperty("scheduleId")]
        public string? ScheduleId { get; set; }

        [JsonProperty("maxRows")]
        public int MaxRows { get; set; } = 500;

        [JsonProperty("maxColumns")]
        public int MaxColumns { get; set; } = 100;
    }

    public sealed class ExportScheduleRequest
    {
        [JsonProperty("scheduleId")]
        public string? ScheduleId { get; set; }

        [JsonProperty("outputFolder")]
        public string? OutputFolder { get; set; }

        [JsonProperty("fileName")]
        public string? FileName { get; set; }
    }

    public sealed class LoadFamilyRequest
    {
        [JsonProperty("path")]
        public string? Path { get; set; }

        [JsonProperty("overwriteExisting")]
        public bool OverwriteExisting { get; set; } = true;

        [JsonProperty("overwriteParameterValues")]
        public bool OverwriteParameterValues { get; set; } = true;
    }

    public sealed class CreateGroupRequest
    {
        [JsonProperty("elementIds")]
        public List<string> ElementIds { get; set; } = new();

        [JsonProperty("name")]
        public string? Name { get; set; }
    }

    public sealed class UpdateGroupRequest
    {
        [JsonProperty("groupId")]
        public string? GroupId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public sealed class CreateAssemblyRequest
    {
        [JsonProperty("elementIds")]
        public List<string> ElementIds { get; set; } = new();

        [JsonProperty("namingCategoryId")]
        public string? NamingCategoryId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }
    }

    public sealed class UpdateAssemblyRequest
    {
        [JsonProperty("assemblyId")]
        public string? AssemblyId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("addElementIds")]
        public List<string> AddElementIds { get; set; } = new();

        [JsonProperty("removeElementIds")]
        public List<string> RemoveElementIds { get; set; } = new();

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public sealed class ReloadRevitLinkRequest
    {
        [JsonProperty("linkTypeId")]
        public string? LinkTypeId { get; set; }

        [JsonProperty("linkInstanceId")]
        public string? LinkInstanceId { get; set; }
    }

    public sealed class ManageWorksetsRequest
    {
        [JsonProperty("action")]
        public string Action { get; set; } = "list";

        [JsonProperty("worksetId")]
        public string? WorksetId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }
    }

    public sealed class SetElementDesignOptionRequest
    {
        [JsonProperty("elementId")]
        public string? ElementId { get; set; }

        [JsonProperty("designOptionId")]
        public string? DesignOptionId { get; set; }
    }
}
