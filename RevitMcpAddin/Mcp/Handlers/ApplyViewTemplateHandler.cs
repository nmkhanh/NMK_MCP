using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class ApplyViewTemplateHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public ApplyViewTemplateHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "apply_view_template";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Applies a view template to a Revit view.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    viewId = new { type = "string", description = "Target view id." },
                    viewTemplateId = new { type = "string", description = "View template id." }
                },
                required = new[] { "viewId", "viewTemplateId" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<ApplyViewTemplateRequest>() ?? new ApplyViewTemplateRequest();
                return ToolHandlerResult.FromJson(await _revitService.ApplyViewTemplateAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("ApplyViewTemplateHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to apply view template: {ex.Message}");
            }
        }
    }
}
