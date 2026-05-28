using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateSpaceHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public UpdateSpaceHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "update_space";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Updates space name, number, and/or parameters.", InputSchema = CreateAreaHandler.SpatialUpdateSchema("Space") };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.UpdateSpaceAsync(arguments?.ToObject<UpdateSpatialElementRequest>() ?? new UpdateSpatialElementRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("UpdateSpaceHandler error", ex); return ToolHandlerResult.FromError($"Failed to update space: {ex.Message}"); }
        }
    }
}
