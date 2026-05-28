using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class GetElementHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public GetElementHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "get_element";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Returns detailed metadata for one Revit element by ElementId.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    elementId = new { type = "string", description = "Revit ElementId to inspect." },
                    includeParameters = new { type = "boolean", description = "Include instance parameters. Default: false.", @default = false },
                    includeTypeParameters = new { type = "boolean", description = "Include type parameters when includeParameters is true. Default: true.", @default = true }
                },
                required = new[] { "elementId" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<GetElementRequest>() ?? new GetElementRequest();
                var result = await _revitService.GetElementAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("GetElementHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to get element: {ex.Message}");
            }
        }
    }
}
