using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class PlaceMepFixtureHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public PlaceMepFixtureHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "place_mep_fixture";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Places an MEP fixture FamilySymbol.", InputSchema = CreateFamilyInstanceHandler.FamilyInstanceSchema() };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.PlaceCategoryInstanceAsync(arguments?.ToObject<CreateFamilyInstanceRequest>() ?? new CreateFamilyInstanceRequest(), "Place MEP Fixture", null, "NonStructural", cancellationToken)); }
            catch (Exception ex) { Logger.Error("PlaceMepFixtureHandler error", ex); return ToolHandlerResult.FromError($"Failed to place MEP fixture: {ex.Message}"); }
        }
    }
}
