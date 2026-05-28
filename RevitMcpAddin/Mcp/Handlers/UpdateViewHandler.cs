using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateViewHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public UpdateViewHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "update_view";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Updates a Revit view name, scale, and/or view template.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    viewId = new { type = "string", description = "View ElementId." },
                    name = new { type = "string", description = "New view name." },
                    scale = new { type = "integer", description = "New view scale." },
                    viewTemplateId = new { type = "string", description = "View template ElementId, 'none', or -1." }
                },
                required = new[] { "viewId" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<UpdateViewRequest>() ?? new UpdateViewRequest();
                if (string.IsNullOrWhiteSpace(request.ViewId)) return ToolHandlerResult.FromError("'viewId' is required.");
                return ToolHandlerResult.FromJson(await _revitService.UpdateViewAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("UpdateViewHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to update view: {ex.Message}");
            }
        }
    }
}
