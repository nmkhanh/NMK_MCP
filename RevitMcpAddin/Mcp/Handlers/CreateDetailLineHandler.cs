using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateDetailLineHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateDetailLineHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_detail_line";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Creates a detail line in a Revit view.",
            InputSchema = LineSchema(requireView: true)
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<CreateLineRequest>() ?? new CreateLineRequest();
                return ToolHandlerResult.FromJson(await _revitService.CreateDetailLineAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("CreateDetailLineHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to create detail line: {ex.Message}");
            }
        }

        internal static object LineSchema(bool requireView) => new
        {
            type = "object",
            properties = new
            {
                viewId = new { type = "string", description = "Target view id. Required for detail line." },
                startX = new { type = "number", description = "Start X." },
                startY = new { type = "number", description = "Start Y." },
                startZ = new { type = "number", description = "Start Z." },
                endX = new { type = "number", description = "End X." },
                endY = new { type = "number", description = "End Y." },
                endZ = new { type = "number", description = "End Z." },
                unit = new { type = "string", description = "Coordinate unit: feet, meters, or millimeters. Default: feet.", @default = "feet" }
            },
            required = requireView
                ? new[] { "viewId", "startX", "startY", "startZ", "endX", "endY", "endZ" }
                : new[] { "startX", "startY", "startZ", "endX", "endY", "endZ" }
        };
    }
}
