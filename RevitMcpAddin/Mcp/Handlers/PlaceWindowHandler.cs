using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class PlaceWindowHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public PlaceWindowHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "place_window";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Places a window FamilySymbol. Provide a hostId for hosted window families.",
            InputSchema = CreateFamilyInstanceHandler.FamilyInstanceSchema()
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<CreateFamilyInstanceRequest>() ?? new CreateFamilyInstanceRequest();
                var result = await _revitService.PlaceCategoryInstanceAsync(
                    request, "Place Window", BuiltInCategory.OST_Windows, "NonStructural", cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("PlaceWindowHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to place window: {ex.Message}");
            }
        }
    }
}
