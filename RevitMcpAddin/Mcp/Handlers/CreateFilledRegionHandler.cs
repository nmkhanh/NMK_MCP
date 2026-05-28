using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateFilledRegionHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateFilledRegionHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_filled_region";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Creates a filled region in a view from a closed polygon.", InputSchema = FilledRegionSchema() };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.CreateFilledRegionAsync(arguments?.ToObject<CreateFilledRegionRequest>() ?? new CreateFilledRegionRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("CreateFilledRegionHandler error", ex); return ToolHandlerResult.FromError($"Failed to create filled region: {ex.Message}"); }
        }
        private static object FilledRegionSchema() => new
        {
            type = "object",
            properties = new
            {
                viewId = new { type = "string", description = "Target view ElementId." },
                filledRegionTypeId = new { type = "string", description = "Optional FilledRegionType id." },
                points = CreateRoofHandler.BoundaryPointsSchema(),
                unit = new { type = "string", description = "Coordinate unit: feet, meters, or millimeters. Default: feet.", @default = "feet" }
            },
            required = new[] { "viewId", "points" }
        };
    }
}
