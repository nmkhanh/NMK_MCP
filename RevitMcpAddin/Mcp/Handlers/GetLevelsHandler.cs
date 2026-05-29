using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class GetLevelsHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public GetLevelsHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "get_levels";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Returns all Revit levels in the active document with id, name, and elevation.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    useActiveView = new { type = "boolean", description = "When true, return only levels visible in the active view. Default: false.", @default = false },
                    viewId = new { type = "string", description = "Optional view ElementId to scope the query. Overrides useActiveView when supplied." }
                },
                required = new string[] { }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var useActiveView = arguments?["useActiveView"]?.Value<bool>() ?? false;
                var viewId = arguments?["viewId"]?.Value<string>();
                var result = await _revitService.GetLevelsAsync(useActiveView, viewId, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("GetLevelsHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to get levels: {ex.Message}");
            }
        }
    }
}
