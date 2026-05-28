using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateViewHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateViewHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_view";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Creates a Revit view. Supports floorPlan, ceilingPlan, structuralPlan, threeD, and drafting.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    viewType = new { type = "string", description = "floorPlan, ceilingPlan, structuralPlan, threeD, or drafting.", @default = "floorPlan" },
                    name = new { type = "string", description = "Optional view name." },
                    levelId = new { type = "string", description = "Required for plan views." },
                    viewFamilyTypeId = new { type = "string", description = "Optional ViewFamilyType id. Defaults to the first matching type." },
                    scale = new { type = "integer", description = "Optional view scale." }
                },
                required = new string[] { }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<CreateViewRequest>() ?? new CreateViewRequest();
                var result = await _revitService.CreateViewAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("CreateViewHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to create view: {ex.Message}");
            }
        }
    }
}
