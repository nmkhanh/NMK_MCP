using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateMaterialHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateMaterialHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_material";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Creates a Revit material with optional RGB color and transparency.",
            InputSchema = MaterialSchema(requireName: true)
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<CreateMaterialRequest>() ?? new CreateMaterialRequest();
                return ToolHandlerResult.FromJson(await _revitService.CreateMaterialAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("CreateMaterialHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to create material: {ex.Message}");
            }
        }

        internal static object ColorSchema() => new
        {
            type = "object",
            properties = new
            {
                r = new { type = "integer", description = "Red 0-255." },
                g = new { type = "integer", description = "Green 0-255." },
                b = new { type = "integer", description = "Blue 0-255." }
            },
            required = new[] { "r", "g", "b" }
        };

        internal static object MaterialSchema(bool requireName) => new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string", description = requireName ? "New material name." : "Existing material name if materialId is omitted." },
                materialId = new { type = "string", description = "Existing Material ElementId for update." },
                newName = new { type = "string", description = "Optional new material name for update." },
                color = ColorSchema(),
                transparency = new { type = "integer", description = "Transparency 0-100." }
            },
            required = requireName ? new[] { "name" } : Array.Empty<string>()
        };
    }
}
