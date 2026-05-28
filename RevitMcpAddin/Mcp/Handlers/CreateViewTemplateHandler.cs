using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateViewTemplateHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateViewTemplateHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_view_template";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Creates a view template from an existing Revit view.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    sourceViewId = new { type = "string", description = "Source view id." },
                    name = new { type = "string", description = "Optional template name." }
                },
                required = new[] { "sourceViewId" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<CreateViewTemplateRequest>() ?? new CreateViewTemplateRequest();
                return ToolHandlerResult.FromJson(await _revitService.CreateViewTemplateAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("CreateViewTemplateHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to create view template: {ex.Message}");
            }
        }
    }
}
