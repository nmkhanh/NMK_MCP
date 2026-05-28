using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateCeilingHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public UpdateCeilingHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "update_ceiling";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Updates a ceiling type and/or instance parameters.", InputSchema = UpdateWallHandler.UpdateBuildingElementSchema("Ceiling ElementId.") };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.UpdateCeilingAsync(arguments?.ToObject<UpdateBuildingElementRequest>() ?? new UpdateBuildingElementRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("UpdateCeilingHandler error", ex); return ToolHandlerResult.FromError($"Failed to update ceiling: {ex.Message}"); }
        }
    }
}
