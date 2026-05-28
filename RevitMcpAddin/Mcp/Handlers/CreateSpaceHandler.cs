using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateSpaceHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateSpaceHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_space";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Creates an MEP space at a level and XY point.", InputSchema = CreateAreaHandler.SpatialCreateSchema(useView: false) };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.CreateSpaceAsync(arguments?.ToObject<CreateSpaceRequest>() ?? new CreateSpaceRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("CreateSpaceHandler error", ex); return ToolHandlerResult.FromError($"Failed to create space: {ex.Message}"); }
        }
    }
}
