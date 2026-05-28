using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class ReloadFamilyHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public ReloadFamilyHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "reload_family";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Reloads a Revit family file and overwrites existing definitions.", InputSchema = LoadFamilyHandler.FamilyLoadSchema() };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.ReloadFamilyAsync(arguments?.ToObject<LoadFamilyRequest>() ?? new LoadFamilyRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("ReloadFamilyHandler error", ex); return ToolHandlerResult.FromError($"Failed to reload family: {ex.Message}"); }
        }
    }
}
