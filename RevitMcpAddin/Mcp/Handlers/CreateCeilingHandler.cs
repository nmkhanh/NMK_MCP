using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateCeilingHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateCeilingHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_ceiling";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Creates a ceiling from a closed polygon boundary.", InputSchema = CreateFloorHandler.CreateBoundaryElementSchema("Optional CeilingType id.") };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.CreateCeilingAsync(arguments?.ToObject<CreateCeilingRequest>() ?? new CreateCeilingRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("CreateCeilingHandler error", ex); return ToolHandlerResult.FromError($"Failed to create ceiling: {ex.Message}"); }
        }
    }
}
