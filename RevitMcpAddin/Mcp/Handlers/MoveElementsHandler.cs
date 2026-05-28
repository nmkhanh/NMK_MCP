using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class MoveElementsHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public MoveElementsHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "move_elements";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Moves Revit elements by a translation vector.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    elementIds = new { type = "array", description = "Revit ElementIds to move.", items = new { type = "string" } },
                    x = new { type = "number", description = "Translation X." },
                    y = new { type = "number", description = "Translation Y." },
                    z = new { type = "number", description = "Translation Z." },
                    unit = new { type = "string", description = "Coordinate unit: feet, meters, or millimeters. Default: feet.", @default = "feet" },
                    dryRun = new { type = "boolean", description = "Validate inputs without modifying the model. Default: false.", @default = false },
                    maxItems = new { type = "integer", description = "Safety cap. Default: 100, hard max: 500.", @default = 100 }
                },
                required = new[] { "elementIds", "x", "y", "z" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<MoveElementsRequest>() ?? new MoveElementsRequest();
                var result = await _revitService.MoveElementsAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("MoveElementsHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to move elements: {ex.Message}");
            }
        }
    }
}
