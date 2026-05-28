using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class GetSheetsHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public GetSheetsHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "get_sheets";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Returns Revit sheets with id, sheet number, name, and placed views.",
            InputSchema = new { type = "object", properties = new { }, required = new string[] { } }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.GetSheetsAsync(cancellationToken)); }
            catch (Exception ex)
            {
                Logger.Error("GetSheetsHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to get sheets: {ex.Message}");
            }
        }
    }
}
