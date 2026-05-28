using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class PlaceElectricalEquipmentHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public PlaceElectricalEquipmentHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "place_electrical_equipment";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Places an electrical equipment FamilySymbol.", InputSchema = CreateFamilyInstanceHandler.FamilyInstanceSchema() };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.PlaceCategoryInstanceAsync(arguments?.ToObject<CreateFamilyInstanceRequest>() ?? new CreateFamilyInstanceRequest(), "Place Electrical Equipment", BuiltInCategory.OST_ElectricalEquipment, "NonStructural", cancellationToken)); }
            catch (Exception ex) { Logger.Error("PlaceElectricalEquipmentHandler error", ex); return ToolHandlerResult.FromError($"Failed to place electrical equipment: {ex.Message}"); }
        }
    }
}
