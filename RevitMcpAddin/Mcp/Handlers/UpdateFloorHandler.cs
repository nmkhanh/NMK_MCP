using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateFloorHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public UpdateFloorHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "update_floor";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Updates a floor type and/or instance parameters.", InputSchema = UpdateWallHandler.UpdateBuildingElementSchema("Floor ElementId.") };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.UpdateFloorAsync(arguments?.ToObject<UpdateBuildingElementRequest>() ?? new UpdateBuildingElementRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("UpdateFloorHandler error", ex); return ToolHandlerResult.FromError($"Failed to update floor: {ex.Message}"); }
        }
    }
}
