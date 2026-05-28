using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateOpeningHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateOpeningHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_opening";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Creates an opening on a host element from a bounding box/profile.", InputSchema = OpeningSchema(create: true) };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.CreateOpeningAsync(arguments?.ToObject<CreateOpeningRequest>() ?? new CreateOpeningRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("CreateOpeningHandler error", ex); return ToolHandlerResult.FromError($"Failed to create opening: {ex.Message}"); }
        }
        internal static object OpeningSchema(bool create) => new
        {
            type = "object",
            properties = new
            {
                elementId = new { type = "string", description = "Opening ElementId for update." },
                hostId = new { type = "string", description = "Host ElementId for create." },
                minX = new { type = "number", description = "Minimum X coordinate." },
                minY = new { type = "number", description = "Minimum Y coordinate." },
                minZ = new { type = "number", description = "Minimum Z coordinate." },
                maxX = new { type = "number", description = "Maximum X coordinate." },
                maxY = new { type = "number", description = "Maximum Y coordinate." },
                maxZ = new { type = "number", description = "Maximum Z coordinate." },
                unit = new { type = "string", description = "Coordinate unit: feet, meters, or millimeters. Default: feet.", @default = "feet" },
                typeId = new { type = "string", description = "Optional target type id for update when supported." },
                parameters = new { type = "object", description = "Optional instance parameters to set." }
            },
            required = create
                ? new[] { "hostId", "minX", "minY", "minZ", "maxX", "maxY", "maxZ" }
                : new[] { "elementId" }
        };
    }
}
