using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateRoofHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateRoofHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_roof";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Creates a footprint roof from a closed polygon boundary.", InputSchema = CreateRoofSchema() };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.CreateRoofAsync(arguments?.ToObject<CreateRoofRequest>() ?? new CreateRoofRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("CreateRoofHandler error", ex); return ToolHandlerResult.FromError($"Failed to create roof: {ex.Message}"); }
        }
        internal static object CreateRoofSchema() => new
        {
            type = "object",
            properties = new
            {
                levelId = new { type = "string", description = "Target Level ElementId." },
                roofTypeId = new { type = "string", description = "Optional RoofType id. Uses the first available roof type when omitted." },
                points = BoundaryPointsSchema(),
                unit = new { type = "string", description = "Coordinate unit: feet, meters, or millimeters. Default: feet.", @default = "feet" },
                parameters = new { type = "object", description = "Optional instance parameters to set after creation." }
            },
            required = new[] { "levelId", "points" }
        };
        internal static object BoundaryPointsSchema() => new
        {
            type = "array",
            description = "Closed boundary points; do not repeat the first point at the end.",
            items = new
            {
                type = "object",
                properties = new { x = new { type = "number" }, y = new { type = "number" }, z = new { type = "number" } },
                required = new[] { "x", "y", "z" }
            }
        };
    }
}
