using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class PlaceFurnitureHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public PlaceFurnitureHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "place_furniture";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Places a furniture FamilySymbol.",
            InputSchema = CreateFamilyInstanceHandler.FamilyInstanceSchema()
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<CreateFamilyInstanceRequest>() ?? new CreateFamilyInstanceRequest();
                var result = await _revitService.PlaceCategoryInstanceAsync(
                    request, "Place Furniture", BuiltInCategory.OST_Furniture, "NonStructural", cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("PlaceFurnitureHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to place furniture: {ex.Message}");
            }
        }
    }
}
