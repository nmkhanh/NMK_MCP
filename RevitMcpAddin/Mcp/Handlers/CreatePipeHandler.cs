using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreatePipeHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreatePipeHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_pipe";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Creates a pipe between two points.", InputSchema = MepCurveSchema(systemType: true, update: false) };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.CreatePipeAsync(arguments?.ToObject<CreateMepCurveRequest>() ?? new CreateMepCurveRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("CreatePipeHandler error", ex); return ToolHandlerResult.FromError($"Failed to create pipe: {ex.Message}"); }
        }
        internal static object MepCurveSchema(bool systemType, bool update) => new
        {
            type = "object",
            properties = new
            {
                elementId = new { type = "string", description = "Existing MEPCurve ElementId for update." },
                typeId = new { type = "string", description = "Optional curve type id. Uses first available type when omitted for create." },
                systemTypeId = new { type = "string", description = systemType ? "Optional system type id. Uses first available system type when omitted." : "Ignored." },
                levelId = new { type = "string", description = "Level ElementId for create." },
                startX = new { type = "number", description = update ? "Optional start X." : "Start X." },
                startY = new { type = "number", description = update ? "Optional start Y." : "Start Y." },
                startZ = new { type = "number", description = update ? "Optional start Z." : "Start Z." },
                endX = new { type = "number", description = update ? "Optional end X." : "End X." },
                endY = new { type = "number", description = update ? "Optional end Y." : "End Y." },
                endZ = new { type = "number", description = update ? "Optional end Z." : "End Z." },
                unit = new { type = "string", description = "Coordinate unit: feet, meters, or millimeters. Default: feet.", @default = "feet" },
                parameters = new { type = "object", description = "Optional instance parameters to set." }
            },
            required = update
                ? new[] { "elementId" }
                : new[] { "levelId", "startX", "startY", "startZ", "endX", "endY", "endZ" }
        };
    }
}
