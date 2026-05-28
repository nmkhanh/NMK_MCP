using Newtonsoft.Json.Linq;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class GetProjectInfoHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public GetProjectInfoHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "get_project_info";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Returns Revit project information, document metadata, and active view summary.",
            InputSchema = new { type = "object", properties = new { }, required = Array.Empty<string>() }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _revitService.GetProjectInfoAsync(cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("GetProjectInfoHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to get project info: {ex.Message}");
            }
        }
    }
}
