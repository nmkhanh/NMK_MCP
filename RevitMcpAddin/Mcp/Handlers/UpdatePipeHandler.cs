using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdatePipeHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public UpdatePipeHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "update_pipe";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Updates a pipe type, endpoints, and/or parameters.", InputSchema = CreatePipeHandler.MepCurveSchema(systemType: true, update: true) };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.UpdatePipeAsync(arguments?.ToObject<UpdateMepCurveRequest>() ?? new UpdateMepCurveRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("UpdatePipeHandler error", ex); return ToolHandlerResult.FromError($"Failed to update pipe: {ex.Message}"); }
        }
    }
}
