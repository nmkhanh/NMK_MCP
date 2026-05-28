using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateAreaHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateAreaHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_area";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Creates an area in an area plan view at an XY point.", InputSchema = SpatialCreateSchema(useView: true) };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.CreateAreaAsync(arguments?.ToObject<CreateAreaRequest>() ?? new CreateAreaRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("CreateAreaHandler error", ex); return ToolHandlerResult.FromError($"Failed to create area: {ex.Message}"); }
        }
        internal static object SpatialCreateSchema(bool useView) => new
        {
            type = "object",
            properties = new
            {
                viewId = new { type = "string", description = "Area plan View ElementId." },
                levelId = new { type = "string", description = "Level ElementId." },
                x = new { type = "number", description = "Placement X coordinate." },
                y = new { type = "number", description = "Placement Y coordinate." },
                unit = new { type = "string", description = "Coordinate unit: feet, meters, or millimeters. Default: feet.", @default = "feet" },
                name = new { type = "string", description = "Optional name." },
                number = new { type = "string", description = "Optional number." },
                parameters = new { type = "object", description = "Optional instance parameters to set." }
            },
            required = useView ? new[] { "viewId", "x", "y" } : new[] { "levelId", "x", "y" }
        };
        internal static object SpatialUpdateSchema(string label) => new
        {
            type = "object",
            properties = new
            {
                elementId = new { type = "string", description = $"{label} ElementId." },
                name = new { type = "string", description = "Optional name." },
                number = new { type = "string", description = "Optional number." },
                parameters = new { type = "object", description = "Optional instance parameters to set." }
            },
            required = new[] { "elementId" }
        };
    }
}
