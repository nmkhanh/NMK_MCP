using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class SetElementDesignOptionHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public SetElementDesignOptionHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "set_element_design_option";
        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Assigns an element to a design option when the parameter is writable.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    elementId = new { type = "string", description = "Target ElementId." },
                    designOptionId = new { type = "string", description = "Target DesignOption ElementId." }
                },
                required = new[] { "elementId", "designOptionId" }
            }
        };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.SetElementDesignOptionAsync(arguments?.ToObject<SetElementDesignOptionRequest>() ?? new SetElementDesignOptionRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("SetElementDesignOptionHandler error", ex); return ToolHandlerResult.FromError($"Failed to set element design option: {ex.Message}"); }
        }
    }
}
