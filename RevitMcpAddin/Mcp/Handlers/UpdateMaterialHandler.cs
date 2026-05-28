using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateMaterialHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public UpdateMaterialHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "update_material";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Updates a Revit material by materialId or name.",
            InputSchema = CreateMaterialHandler.MaterialSchema(requireName: false)
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<UpdateMaterialRequest>() ?? new UpdateMaterialRequest();
                return ToolHandlerResult.FromJson(await _revitService.UpdateMaterialAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("UpdateMaterialHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to update material: {ex.Message}");
            }
        }
    }
}
