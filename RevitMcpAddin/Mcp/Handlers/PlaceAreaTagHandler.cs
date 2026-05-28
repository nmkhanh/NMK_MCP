using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class PlaceAreaTagHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public PlaceAreaTagHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "place_area_tag";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Places an area tag in a view.", InputSchema = CreateTagHandler.TagSchema() };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.PlaceAreaTagAsync(arguments?.ToObject<CreateTagRequest>() ?? new CreateTagRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("PlaceAreaTagHandler error", ex); return ToolHandlerResult.FromError($"Failed to place area tag: {ex.Message}"); }
        }
    }
}
