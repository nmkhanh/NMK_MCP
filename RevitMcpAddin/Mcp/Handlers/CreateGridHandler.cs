using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateGridHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public CreateGridHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "create_grid";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Creates a straight Revit grid line. Coordinates default to feet.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    name = new
                    {
                        type = "string",
                        description = "Optional grid name. If omitted, Revit assigns a default name."
                    },
                    startX = new { type = "number", description = "Grid start X coordinate." },
                    startY = new { type = "number", description = "Grid start Y coordinate." },
                    endX = new { type = "number", description = "Grid end X coordinate." },
                    endY = new { type = "number", description = "Grid end Y coordinate." },
                    z = new
                    {
                        type = "number",
                        description = "Optional Z coordinate for both endpoints. Default: 0.",
                        @default = 0
                    },
                    unit = new
                    {
                        type = "string",
                        description = "Coordinate unit: feet, meters, or millimeters. Default: feet.",
                        @default = "feet"
                    }
                },
                required = new[] { "startX", "startY", "endX", "endY" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                foreach (var key in new[] { "startX", "startY", "endX", "endY" })
                {
                    if (arguments?[key] == null)
                        return ToolHandlerResult.FromError($"'{key}' is required.");
                }

                var request = arguments!.ToObject<CreateGridRequest>() ?? new CreateGridRequest();
                var result = await _revitService.CreateGridAsync(request, cancellationToken);
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
                Logger.Error("CreateGridHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to create grid: {ex.Message}");
            }
        }
    }
}
