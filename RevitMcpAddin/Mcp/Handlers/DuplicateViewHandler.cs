using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class DuplicateViewHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public DuplicateViewHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "duplicate_view";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Duplicates a Revit view using Duplicate, WithDetailing, or AsDependent.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    viewId = new { type = "string", description = "Source view ElementId." },
                    name = new { type = "string", description = "Optional new view name." },
                    duplicateOption = new { type = "string", description = "Duplicate, WithDetailing, or AsDependent.", @default = "Duplicate" }
                },
                required = new[] { "viewId" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<DuplicateViewRequest>() ?? new DuplicateViewRequest();
                if (string.IsNullOrWhiteSpace(request.ViewId)) return ToolHandlerResult.FromError("'viewId' is required.");
                return ToolHandlerResult.FromJson(await _revitService.DuplicateViewAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("DuplicateViewHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to duplicate view: {ex.Message}");
            }
        }
    }
}
