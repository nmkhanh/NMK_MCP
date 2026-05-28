using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateScheduleHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public UpdateScheduleHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "update_schedule";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Updates a schedule name and/or adds fields.", InputSchema = CreateScheduleHandler.ScheduleSchema(create: false) };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.UpdateScheduleAsync(arguments?.ToObject<UpdateScheduleRequest>() ?? new UpdateScheduleRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("UpdateScheduleHandler error", ex); return ToolHandlerResult.FromError($"Failed to update schedule: {ex.Message}"); }
        }
    }
}
