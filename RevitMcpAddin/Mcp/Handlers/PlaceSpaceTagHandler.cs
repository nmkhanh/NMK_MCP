using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class PlaceSpaceTagHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public PlaceSpaceTagHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "place_space_tag";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Places a space tag in a view.", InputSchema = CreateTagHandler.TagSchema() };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.PlaceSpaceTagAsync(arguments?.ToObject<CreateTagRequest>() ?? new CreateTagRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("PlaceSpaceTagHandler error", ex); return ToolHandlerResult.FromError($"Failed to place space tag: {ex.Message}"); }
        }
    }
}
