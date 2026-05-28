using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class DuplicateElementTypeHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public DuplicateElementTypeHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "duplicate_element_type";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Duplicates an ElementType and returns the new type id.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    typeId = new { type = "string", description = "Source ElementType id." },
                    name = new { type = "string", description = "New type name." }
                },
                required = new[] { "typeId", "name" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<DuplicateElementTypeRequest>() ?? new DuplicateElementTypeRequest();
                return ToolHandlerResult.FromJson(await _revitService.DuplicateElementTypeAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("DuplicateElementTypeHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to duplicate element type: {ex.Message}");
            }
        }
    }
}
