using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class MirrorElementsHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public MirrorElementsHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "mirror_elements";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Mirrors Revit elements across a plane defined by origin and normal.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    elementIds = new { type = "array", description = "Revit ElementIds to mirror.", items = new { type = "string" } },
                    originX = new { type = "number", description = "Mirror plane origin X." },
                    originY = new { type = "number", description = "Mirror plane origin Y." },
                    originZ = new { type = "number", description = "Mirror plane origin Z." },
                    normalX = new { type = "number", description = "Mirror plane normal X.", @default = 1 },
                    normalY = new { type = "number", description = "Mirror plane normal Y." },
                    normalZ = new { type = "number", description = "Mirror plane normal Z." },
                    copy = new { type = "boolean", description = "Create mirrored copies instead of mirroring originals. Default: true.", @default = true },
                    unit = new { type = "string", description = "Coordinate unit: feet, meters, or millimeters. Default: feet.", @default = "feet" },
                    dryRun = new { type = "boolean", description = "Validate inputs without modifying the model. Default: false.", @default = false },
                    maxItems = new { type = "integer", description = "Safety cap. Default: 100, hard max: 500.", @default = 100 }
                },
                required = new[] { "elementIds", "originX", "originY", "originZ", "normalX", "normalY", "normalZ" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<MirrorElementsRequest>() ?? new MirrorElementsRequest();
                var result = await _revitService.MirrorElementsAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("MirrorElementsHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to mirror elements: {ex.Message}");
            }
        }
    }
}
