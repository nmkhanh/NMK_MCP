using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class PlaceMechanicalEquipmentHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public PlaceMechanicalEquipmentHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "place_mechanical_equipment";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Places a mechanical equipment FamilySymbol.", InputSchema = CreateFamilyInstanceHandler.FamilyInstanceSchema() };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.PlaceCategoryInstanceAsync(arguments?.ToObject<CreateFamilyInstanceRequest>() ?? new CreateFamilyInstanceRequest(), "Place Mechanical Equipment", BuiltInCategory.OST_MechanicalEquipment, "NonStructural", cancellationToken)); }
            catch (Exception ex) { Logger.Error("PlaceMechanicalEquipmentHandler error", ex); return ToolHandlerResult.FromError($"Failed to place mechanical equipment: {ex.Message}"); }
        }
    }
}
