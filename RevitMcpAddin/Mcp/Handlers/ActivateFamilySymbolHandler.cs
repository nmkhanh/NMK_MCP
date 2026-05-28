using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class ActivateFamilySymbolHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public ActivateFamilySymbolHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "activate_family_symbol";
        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Activates a Revit FamilySymbol so it can be placed.",
            InputSchema = new
            {
                type = "object",
                properties = new { symbolId = new { type = "string", description = "FamilySymbol ElementId." } },
                required = new[] { "symbolId" }
            }
        };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.ActivateFamilySymbolAsync(arguments?.ToObject<ActivateFamilySymbolRequest>() ?? new ActivateFamilySymbolRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("ActivateFamilySymbolHandler error", ex); return ToolHandlerResult.FromError($"Failed to activate family symbol: {ex.Message}"); }
        }
    }
}
