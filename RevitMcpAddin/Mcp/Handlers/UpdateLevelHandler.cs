using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateLevelHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public UpdateLevelHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "update_level";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Updates a Revit level name and/or elevation.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    levelId = new
                    {
                        type = "string",
                        description = "Revit ElementId of the level to update."
                    },
                    name = new
                    {
                        type = "string",
                        description = "New level name."
                    },
                    elevation = new
                    {
                        type = "number",
                        description = "New level elevation. Interpreted using the unit field."
                    },
                    unit = new
                    {
                        type = "string",
                        description = "Elevation unit: feet, meters, or millimeters. Default: feet.",
                        @default = "feet"
                    }
                },
                required = new[] { "levelId" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<UpdateLevelRequest>() ?? new UpdateLevelRequest();
                if (string.IsNullOrWhiteSpace(request.LevelId))
                    return ToolHandlerResult.FromError("'levelId' is required.");
                if (string.IsNullOrWhiteSpace(request.Name) && !request.Elevation.HasValue)
                    return ToolHandlerResult.FromError("Provide at least one field to update: 'name' or 'elevation'.");

                var result = await _revitService.UpdateLevelAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (ArgumentException ex)
            {
                return ToolHandlerResult.FromError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ToolHandlerResult.FromError(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Error("UpdateLevelHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to update level: {ex.Message}");
            }
        }
    }
}
