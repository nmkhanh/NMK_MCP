using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateCableTrayHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateCableTrayHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_cable_tray";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Creates a cable tray between two points.", InputSchema = CreatePipeHandler.MepCurveSchema(systemType: false, update: false) };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.CreateCableTrayAsync(arguments?.ToObject<CreateMepCurveRequest>() ?? new CreateMepCurveRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("CreateCableTrayHandler error", ex); return ToolHandlerResult.FromError($"Failed to create cable tray: {ex.Message}"); }
        }
    }
}
