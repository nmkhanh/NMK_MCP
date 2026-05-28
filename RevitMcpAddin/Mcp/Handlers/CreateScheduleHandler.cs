using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateScheduleHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateScheduleHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_schedule";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Creates a Revit schedule for a category and optional fields.", InputSchema = ScheduleSchema(create: true) };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.CreateScheduleAsync(arguments?.ToObject<CreateScheduleRequest>() ?? new CreateScheduleRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("CreateScheduleHandler error", ex); return ToolHandlerResult.FromError($"Failed to create schedule: {ex.Message}"); }
        }
        internal static object ScheduleSchema(bool create) => new
        {
            type = "object",
            properties = new
            {
                scheduleId = new { type = "string", description = "Schedule ElementId for update." },
                category = new { type = "string", description = "BuiltInCategory suffix, e.g. Walls, Doors, Rooms." },
                categoryId = new { type = "string", description = "Optional category ElementId alternative." },
                name = new { type = "string", description = "Optional schedule name." },
                fieldNames = new { type = "array", description = "Schedulable field names to add.", items = new { type = "string" } }
            },
            required = create ? Array.Empty<string>() : new[] { "scheduleId" }
        };
    }
}
