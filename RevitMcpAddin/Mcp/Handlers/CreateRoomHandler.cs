using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateRoomHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateRoomHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_room";
        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Creates a room at a level and XY point.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    levelId = new { type = "string", description = "Target Level ElementId." },
                    x = new { type = "number", description = "Room placement X." },
                    y = new { type = "number", description = "Room placement Y." },
                    unit = new { type = "string", description = "Coordinate unit: feet, meters, or millimeters. Default: feet.", @default = "feet" },
                    name = new { type = "string", description = "Optional room name." },
                    number = new { type = "string", description = "Optional room number." },
                    parameters = new { type = "object", description = "Optional instance parameters to set." }
                },
                required = new[] { "levelId", "x", "y" }
            }
        };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.CreateRoomAsync(arguments?.ToObject<CreateRoomRequest>() ?? new CreateRoomRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("CreateRoomHandler error", ex); return ToolHandlerResult.FromError($"Failed to create room: {ex.Message}"); }
        }
    }
}
