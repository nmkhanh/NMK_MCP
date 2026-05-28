using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class RenameElementTypeHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public RenameElementTypeHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "rename_element_type";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Renames an ElementType.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    typeId = new { type = "string", description = "ElementType id to rename." },
                    name = new { type = "string", description = "New type name." }
                },
                required = new[] { "typeId", "name" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<RenameElementTypeRequest>() ?? new RenameElementTypeRequest();
                return ToolHandlerResult.FromJson(await _revitService.RenameElementTypeAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("RenameElementTypeHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to rename element type: {ex.Message}");
            }
        }
    }
}
