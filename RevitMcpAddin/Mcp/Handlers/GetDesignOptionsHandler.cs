using Newtonsoft.Json.Linq;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class GetDesignOptionsHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public GetDesignOptionsHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "get_design_options";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Lists design options in the active Revit document.", InputSchema = new { type = "object", properties = new { }, required = Array.Empty<string>() } };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.GetDesignOptionsAsync(cancellationToken)); }
            catch (Exception ex) { Logger.Error("GetDesignOptionsHandler error", ex); return ToolHandlerResult.FromError($"Failed to get design options: {ex.Message}"); }
        }
    }
}
