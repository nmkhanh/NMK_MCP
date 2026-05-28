using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class RotateElementsHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public RotateElementsHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "rotate_elements";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Rotates Revit elements around an axis defined by origin and vector.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    elementIds = new { type = "array", description = "Revit ElementIds to rotate.", items = new { type = "string" } },
                    originX = new { type = "number", description = "Axis origin X." },
                    originY = new { type = "number", description = "Axis origin Y." },
                    originZ = new { type = "number", description = "Axis origin Z." },
                    axisX = new { type = "number", description = "Axis vector X." },
                    axisY = new { type = "number", description = "Axis vector Y." },
                    axisZ = new { type = "number", description = "Axis vector Z.", @default = 1 },
                    angleDegrees = new { type = "number", description = "Rotation angle in degrees." },
                    unit = new { type = "string", description = "Coordinate unit: feet, meters, or millimeters. Default: feet.", @default = "feet" },
                    dryRun = new { type = "boolean", description = "Validate inputs without modifying the model. Default: false.", @default = false },
                    maxItems = new { type = "integer", description = "Safety cap. Default: 100, hard max: 500.", @default = 100 }
                },
                required = new[] { "elementIds", "originX", "originY", "originZ", "axisX", "axisY", "axisZ", "angleDegrees" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<RotateElementsRequest>() ?? new RotateElementsRequest();
                var result = await _revitService.RotateElementsAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("RotateElementsHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to rotate elements: {ex.Message}");
            }
        }
    }
}
