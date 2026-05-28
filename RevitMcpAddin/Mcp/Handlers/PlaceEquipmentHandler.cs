using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class PlaceEquipmentHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public PlaceEquipmentHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "place_equipment";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Places an equipment FamilySymbol. Use list_family_types to locate the correct symbolId.",
            InputSchema = CreateFamilyInstanceHandler.FamilyInstanceSchema()
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<CreateFamilyInstanceRequest>() ?? new CreateFamilyInstanceRequest();
                var result = await _revitService.PlaceCategoryInstanceAsync(
                    request, "Place Equipment", null, "NonStructural", cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("PlaceEquipmentHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to place equipment: {ex.Message}");
            }
        }
    }
}
