using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateDuctHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateDuctHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_duct";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Creates a duct between two points.", InputSchema = CreatePipeHandler.MepCurveSchema(systemType: true, update: false) };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.CreateDuctAsync(arguments?.ToObject<CreateMepCurveRequest>() ?? new CreateMepCurveRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("CreateDuctHandler error", ex); return ToolHandlerResult.FromError($"Failed to create duct: {ex.Message}"); }
        }
    }
}
