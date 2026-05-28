using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class PlaceColumnHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public PlaceColumnHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "place_column";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Places a column FamilySymbol. Defaults structuralType to Column.",
            InputSchema = CreateFamilyInstanceHandler.FamilyInstanceSchema()
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<CreateFamilyInstanceRequest>() ?? new CreateFamilyInstanceRequest();
                var result = await _revitService.PlaceCategoryInstanceAsync(
                    request, "Place Column", null, "Column", cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("PlaceColumnHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to place column: {ex.Message}");
            }
        }
    }
}
