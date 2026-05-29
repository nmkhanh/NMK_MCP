using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class GetGridsHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public GetGridsHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "get_grids";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Returns all Revit grids in the active document with id, name, and curve data.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    useActiveView = new { type = "boolean", description = "When true, return only grids visible in the active view. Default: false.", @default = false },
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
                var result = await _revitService.GetGridsAsync(useActiveView, viewId, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("GetGridsHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to get grids: {ex.Message}");
            }
        }
    }
}
