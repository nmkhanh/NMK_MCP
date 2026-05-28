using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateWallHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public UpdateWallHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "update_wall";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Updates a wall type and/or instance parameters.", InputSchema = UpdateBuildingElementSchema("Wall ElementId.") };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.UpdateWallAsync(arguments?.ToObject<UpdateBuildingElementRequest>() ?? new UpdateBuildingElementRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("UpdateWallHandler error", ex); return ToolHandlerResult.FromError($"Failed to update wall: {ex.Message}"); }
        }
        internal static object UpdateBuildingElementSchema(string idDescription) => new
        {
            type = "object",
            properties = new
            {
                elementId = new { type = "string", description = idDescription },
                typeId = new { type = "string", description = "Optional target type id." },
                parameters = new { type = "object", description = "Optional instance parameters to set." }
            },
            required = new[] { "elementId" }
        };
    }
}
