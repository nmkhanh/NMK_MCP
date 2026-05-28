using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class ManageWorksetsHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public ManageWorksetsHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "manage_worksets";
        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Lists, creates, or renames user worksets.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    action = new { type = "string", description = "list, create, or rename. Default: list.", @default = "list" },
                    worksetId = new { type = "string", description = "Workset id for rename." },
                    name = new { type = "string", description = "Workset name for create/rename." }
                },
                required = Array.Empty<string>()
            }
        };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.ManageWorksetsAsync(arguments?.ToObject<ManageWorksetsRequest>() ?? new ManageWorksetsRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("ManageWorksetsHandler error", ex); return ToolHandlerResult.FromError($"Failed to manage worksets: {ex.Message}"); }
        }
    }
}
