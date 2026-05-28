using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateRoofHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public UpdateRoofHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "update_roof";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Updates a roof type and/or instance parameters.", InputSchema = UpdateWallHandler.UpdateBuildingElementSchema("Roof ElementId.") };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.UpdateRoofAsync(arguments?.ToObject<UpdateBuildingElementRequest>() ?? new UpdateBuildingElementRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("UpdateRoofHandler error", ex); return ToolHandlerResult.FromError($"Failed to update roof: {ex.Message}"); }
        }
    }
}
