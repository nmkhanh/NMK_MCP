using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateRoomHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public UpdateRoomHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "update_room";
        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Updates room name, number, and/or parameters.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    roomId = new { type = "string", description = "Room ElementId." },
                    name = new { type = "string", description = "Optional room name." },
                    number = new { type = "string", description = "Optional room number." },
                    parameters = new { type = "object", description = "Optional instance parameters to set." }
                },
                required = new[] { "roomId" }
            }
        };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.UpdateRoomAsync(arguments?.ToObject<UpdateRoomRequest>() ?? new UpdateRoomRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("UpdateRoomHandler error", ex); return ToolHandlerResult.FromError($"Failed to update room: {ex.Message}"); }
        }
    }
}
