using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateFloorHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateFloorHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_floor";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Creates a floor from a closed polygon boundary.", InputSchema = CreateBoundaryElementSchema("Optional FloorType id.") };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.CreateFloorAsync(arguments?.ToObject<CreateFloorRequest>() ?? new CreateFloorRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("CreateFloorHandler error", ex); return ToolHandlerResult.FromError($"Failed to create floor: {ex.Message}"); }
        }
        internal static object CreateBoundaryElementSchema(string typeDescription) => new
        {
            type = "object",
            properties = new
            {
                levelId = new { type = "string", description = "Target Level ElementId." },
                typeId = new { type = "string", description = typeDescription },
                points = new
                {
                    type = "array",
                    description = "Closed boundary points; do not repeat the first point at the end.",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            x = new { type = "number" },
                            y = new { type = "number" },
                            z = new { type = "number" }
                        },
                        required = new[] { "x", "y", "z" }
                    }
                },
                unit = new { type = "string", description = "Coordinate unit: feet, meters, or millimeters. Default: feet.", @default = "feet" },
                parameters = new { type = "object", description = "Optional instance parameters to set after creation." }
            },
            required = new[] { "levelId", "points" }
        };
    }
}
