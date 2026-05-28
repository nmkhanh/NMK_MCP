using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateGroupHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public UpdateGroupHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "update_group";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Updates a Revit group name and/or parameters.", InputSchema = CreateGroupHandler.GroupSchema(create: false) };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.UpdateGroupAsync(arguments?.ToObject<UpdateGroupRequest>() ?? new UpdateGroupRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("UpdateGroupHandler error", ex); return ToolHandlerResult.FromError($"Failed to update group: {ex.Message}"); }
        }
    }
}
