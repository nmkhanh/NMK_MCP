using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class ReloadRevitLinkHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public ReloadRevitLinkHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "reload_revit_link";
        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Reloads a Revit link type by type id or link instance id.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    linkTypeId = new { type = "string", description = "RevitLinkType ElementId." },
                    linkInstanceId = new { type = "string", description = "RevitLinkInstance ElementId alternative." }
                },
                required = Array.Empty<string>()
            }
        };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.ReloadRevitLinkAsync(arguments?.ToObject<ReloadRevitLinkRequest>() ?? new ReloadRevitLinkRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("ReloadRevitLinkHandler error", ex); return ToolHandlerResult.FromError($"Failed to reload Revit link: {ex.Message}"); }
        }
    }
}
