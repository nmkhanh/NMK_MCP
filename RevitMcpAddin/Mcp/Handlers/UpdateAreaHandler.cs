using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateAreaHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public UpdateAreaHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "update_area";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Updates area name, number, and/or parameters.", InputSchema = CreateAreaHandler.SpatialUpdateSchema("Area") };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.UpdateAreaAsync(arguments?.ToObject<UpdateSpatialElementRequest>() ?? new UpdateSpatialElementRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("UpdateAreaHandler error", ex); return ToolHandlerResult.FromError($"Failed to update area: {ex.Message}"); }
        }
    }
}
