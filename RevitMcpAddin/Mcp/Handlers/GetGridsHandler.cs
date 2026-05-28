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
                properties = new { },
                required = new string[] { }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _revitService.GetGridsAsync(cancellationToken);
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
