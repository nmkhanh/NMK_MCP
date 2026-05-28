using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateDuctHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public UpdateDuctHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "update_duct";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Updates a duct type, endpoints, and/or parameters.", InputSchema = CreatePipeHandler.MepCurveSchema(systemType: true, update: true) };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.UpdateDuctAsync(arguments?.ToObject<UpdateMepCurveRequest>() ?? new UpdateMepCurveRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("UpdateDuctHandler error", ex); return ToolHandlerResult.FromError($"Failed to update duct: {ex.Message}"); }
        }
    }
}
