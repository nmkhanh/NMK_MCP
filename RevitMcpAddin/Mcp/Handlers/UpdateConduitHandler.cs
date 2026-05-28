using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateConduitHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public UpdateConduitHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "update_conduit";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Updates a conduit type, endpoints, and/or parameters.", InputSchema = CreatePipeHandler.MepCurveSchema(systemType: false, update: true) };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.UpdateConduitAsync(arguments?.ToObject<UpdateMepCurveRequest>() ?? new UpdateMepCurveRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("UpdateConduitHandler error", ex); return ToolHandlerResult.FromError($"Failed to update conduit: {ex.Message}"); }
        }
    }
}
