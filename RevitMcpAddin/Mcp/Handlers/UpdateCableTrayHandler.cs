using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateCableTrayHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public UpdateCableTrayHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "update_cable_tray";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Updates a cable tray type, endpoints, and/or parameters.", InputSchema = CreatePipeHandler.MepCurveSchema(systemType: false, update: true) };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.UpdateCableTrayAsync(arguments?.ToObject<UpdateMepCurveRequest>() ?? new UpdateMepCurveRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("UpdateCableTrayHandler error", ex); return ToolHandlerResult.FromError($"Failed to update cable tray: {ex.Message}"); }
        }
    }
}
