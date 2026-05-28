using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateConduitHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateConduitHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_conduit";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Creates a conduit between two points.", InputSchema = CreatePipeHandler.MepCurveSchema(systemType: false, update: false) };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.CreateConduitAsync(arguments?.ToObject<CreateMepCurveRequest>() ?? new CreateMepCurveRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("CreateConduitHandler error", ex); return ToolHandlerResult.FromError($"Failed to create conduit: {ex.Message}"); }
        }
    }
}
