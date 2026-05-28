using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class GetFamilySymbolsHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public GetFamilySymbolsHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "get_family_symbols";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Lists Revit FamilySymbol ids, optionally filtered by category and family name.", InputSchema = ListElementTypesHandler.ElementTypeSchema() };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.ListFamilyTypesAsync(arguments?.ToObject<ListElementTypesRequest>() ?? new ListElementTypesRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("GetFamilySymbolsHandler error", ex); return ToolHandlerResult.FromError($"Failed to get family symbols: {ex.Message}"); }
        }
    }
}
