using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateLevelHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public CreateLevelHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "create_level";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Creates a Revit level at the requested elevation. Unit defaults to feet.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    name = new
                    {
                        type = "string",
                        description = "Optional level name. If omitted, Revit assigns a default name."
                    },
                    elevation = new
                    {
                        type = "number",
                        description = "Level elevation. Interpreted using the unit field."
                    },
                    unit = new
                    {
                        type = "string",
                        description = "Elevation unit: feet, meters, or millimeters. Default: feet.",
                        @default = "feet"
                    }
                },
                required = new[] { "elevation" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                if (arguments?["elevation"] == null)
                    return ToolHandlerResult.FromError("'elevation' is required.");

                var request = arguments.ToObject<CreateLevelRequest>() ?? new CreateLevelRequest();
                var result = await _revitService.CreateLevelAsync(request, cancellationToken);
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
                Logger.Error("CreateLevelHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to create level: {ex.Message}");
            }
        }
    }
}
