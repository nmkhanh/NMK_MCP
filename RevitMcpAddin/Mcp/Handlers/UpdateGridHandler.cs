using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateGridHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public UpdateGridHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "update_grid";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Updates a Revit grid. Currently supports renaming the grid.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    gridId = new
                    {
                        type = "string",
                        description = "Revit ElementId of the grid to update."
                    },
                    name = new
                    {
                        type = "string",
                        description = "New grid name."
                    }
                },
                required = new[] { "gridId" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<UpdateGridRequest>() ?? new UpdateGridRequest();
                if (string.IsNullOrWhiteSpace(request.GridId))
                    return ToolHandlerResult.FromError("'gridId' is required.");
                if (string.IsNullOrWhiteSpace(request.Name))
                    return ToolHandlerResult.FromError("Provide at least one field to update: 'name'.");

                var result = await _revitService.UpdateGridAsync(request, cancellationToken);
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
                Logger.Error("UpdateGridHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to update grid: {ex.Message}");
            }
        }
    }
}
