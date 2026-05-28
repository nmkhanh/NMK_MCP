using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class ExportScheduleHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public ExportScheduleHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "export_schedule";
        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Exports a Revit schedule to a text file.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    scheduleId = new { type = "string", description = "Schedule ElementId." },
                    outputFolder = new { type = "string", description = "Absolute output folder. Defaults to document folder or Desktop." },
                    fileName = new { type = "string", description = "Optional output file name. Defaults to schedule name plus .txt." }
                },
                required = new[] { "scheduleId" }
            }
        };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.ExportScheduleAsync(arguments?.ToObject<ExportScheduleRequest>() ?? new ExportScheduleRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("ExportScheduleHandler error", ex); return ToolHandlerResult.FromError($"Failed to export schedule: {ex.Message}"); }
        }
    }
}
