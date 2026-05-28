using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateGroupHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateGroupHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_group";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Creates a Revit model group from element ids.", InputSchema = GroupSchema(create: true) };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.CreateGroupAsync(arguments?.ToObject<CreateGroupRequest>() ?? new CreateGroupRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("CreateGroupHandler error", ex); return ToolHandlerResult.FromError($"Failed to create group: {ex.Message}"); }
        }
        internal static object GroupSchema(bool create) => new
        {
            type = "object",
            properties = new
            {
                groupId = new { type = "string", description = "Group ElementId for update." },
                elementIds = new { type = "array", description = "ElementIds to group.", items = new { type = "string" } },
                name = new { type = "string", description = "Optional group type name." },
                parameters = new { type = "object", description = "Optional instance parameters to set on update." }
            },
            required = create ? new[] { "elementIds" } : new[] { "groupId" }
        };
    }
}
