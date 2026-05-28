using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class PlaceRoomTagHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public PlaceRoomTagHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "place_room_tag";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Places a room tag in a view.", InputSchema = CreateTagHandler.TagSchema() };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.PlaceRoomTagAsync(arguments?.ToObject<CreateTagRequest>() ?? new CreateTagRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("PlaceRoomTagHandler error", ex); return ToolHandlerResult.FromError($"Failed to place room tag: {ex.Message}"); }
        }
    }
}
