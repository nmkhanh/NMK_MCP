using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateDimensionHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateDimensionHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_dimension";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Creates a dimension between element references in a view.", InputSchema = DimensionSchema() };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.CreateDimensionAsync(arguments?.ToObject<CreateDimensionRequest>() ?? new CreateDimensionRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("CreateDimensionHandler error", ex); return ToolHandlerResult.FromError($"Failed to create dimension: {ex.Message}"); }
        }
        private static object DimensionSchema() => new
        {
            type = "object",
            properties = new
            {
                viewId = new { type = "string", description = "Target view ElementId." },
                elementIds = new { type = "array", description = "ElementIds to dimension.", items = new { type = "string" } },
                startX = new { type = "number", description = "Dimension line start X." },
                startY = new { type = "number", description = "Dimension line start Y." },
                startZ = new { type = "number", description = "Dimension line start Z." },
                endX = new { type = "number", description = "Dimension line end X." },
                endY = new { type = "number", description = "Dimension line end Y." },
                endZ = new { type = "number", description = "Dimension line end Z." },
                unit = new { type = "string", description = "Coordinate unit: feet, meters, or millimeters. Default: feet.", @default = "feet" },
                dimensionTypeId = new { type = "string", description = "Optional DimensionType id." }
            },
            required = new[] { "viewId", "elementIds", "startX", "startY", "startZ", "endX", "endY", "endZ" }
        };
    }
}
