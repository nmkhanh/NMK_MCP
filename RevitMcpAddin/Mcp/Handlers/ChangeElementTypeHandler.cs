using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class ChangeElementTypeHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public ChangeElementTypeHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "change_element_type";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Changes one element to another compatible Revit ElementType.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    elementId = new { type = "string", description = "Revit ElementId to update." },
                    typeId = new { type = "string", description = "Target ElementType id." }
                },
                required = new[] { "elementId", "typeId" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<ChangeElementTypeRequest>() ?? new ChangeElementTypeRequest();
                var result = await _revitService.ChangeElementTypeAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("ChangeElementTypeHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to change element type: {ex.Message}");
            }
        }
    }
}
