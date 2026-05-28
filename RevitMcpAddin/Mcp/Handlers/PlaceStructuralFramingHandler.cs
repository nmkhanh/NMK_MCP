using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class PlaceStructuralFramingHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public PlaceStructuralFramingHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "place_structural_framing";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Places a structural framing FamilySymbol. Defaults structuralType to Beam.",
            InputSchema = CreateFamilyInstanceHandler.FamilyInstanceSchema()
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<CreateFamilyInstanceRequest>() ?? new CreateFamilyInstanceRequest();
                var result = await _revitService.PlaceCategoryInstanceAsync(
                    request, "Place Structural Framing", BuiltInCategory.OST_StructuralFraming, "Beam", cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("PlaceStructuralFramingHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to place structural framing: {ex.Message}");
            }
        }
    }
}
