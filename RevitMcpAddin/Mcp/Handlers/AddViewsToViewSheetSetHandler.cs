using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class AddViewsToViewSheetSetHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public AddViewsToViewSheetSetHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "add_views_to_view_sheet_set";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Adds viewIds to an existing ViewSheetSet.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string", description = "Existing ViewSheetSet name." },
                    viewIds = new { type = "array", items = new { type = "string" }, description = "View ids to add." }
                },
                required = new[] { "name", "viewIds" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<ModifyViewSheetSetRequest>() ?? new ModifyViewSheetSetRequest();
                if (string.IsNullOrWhiteSpace(request.Name)) return ToolHandlerResult.FromError("'name' is required.");
                if (request.ViewIds.Count == 0) return ToolHandlerResult.FromError("'viewIds' must contain at least one id.");
                return ToolHandlerResult.FromJson(await _revitService.AddViewsToViewSheetSetAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("AddViewsToViewSheetSetHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to add views to ViewSheetSet: {ex.Message}");
            }
        }
    }
}
