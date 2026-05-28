using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateTagHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateTagHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_tag";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Creates an IndependentTag for an element in a view.", InputSchema = TagSchema() };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.CreateTagAsync(arguments?.ToObject<CreateTagRequest>() ?? new CreateTagRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("CreateTagHandler error", ex); return ToolHandlerResult.FromError($"Failed to create tag: {ex.Message}"); }
        }
        internal static object TagSchema() => new
        {
            type = "object",
            properties = new
            {
                viewId = new { type = "string", description = "Target view ElementId." },
                elementId = new { type = "string", description = "ElementId to tag." },
                x = new { type = "number", description = "Tag head X coordinate." },
                y = new { type = "number", description = "Tag head Y coordinate." },
                z = new { type = "number", description = "Tag head Z coordinate." },
                unit = new { type = "string", description = "Coordinate unit: feet, meters, or millimeters. Default: feet.", @default = "feet" },
                tagTypeId = new { type = "string", description = "Optional tag type id." },
                addLeader = new { type = "boolean", description = "Add a leader. Default: false.", @default = false }
            },
            required = new[] { "viewId", "elementId", "x", "y", "z" }
        };
    }
}
