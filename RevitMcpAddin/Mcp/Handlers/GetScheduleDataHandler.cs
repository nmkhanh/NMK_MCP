using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class GetScheduleDataHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public GetScheduleDataHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "get_schedule_data";
        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Reads visible body cells from a Revit schedule.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    scheduleId = new { type = "string", description = "Schedule ElementId." },
                    maxRows = new { type = "integer", description = "Safety cap. Default: 500, hard max: 5000.", @default = 500 },
                    maxColumns = new { type = "integer", description = "Safety cap. Default: 100, hard max: 500.", @default = 100 }
                },
                required = new[] { "scheduleId" }
            }
        };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.GetScheduleDataAsync(arguments?.ToObject<GetScheduleDataRequest>() ?? new GetScheduleDataRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("GetScheduleDataHandler error", ex); return ToolHandlerResult.FromError($"Failed to read schedule data: {ex.Message}"); }
        }
    }
}
