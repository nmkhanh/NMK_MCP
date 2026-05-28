using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateElementHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public UpdateElementHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "update_element";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Updates general element fields: name, typeId, pinned state, and instance/type parameters.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    elementId = new { type = "string", description = "Revit ElementId to update." },
                    name = new { type = "string", description = "Optional new element name where writable." },
                    typeId = new { type = "string", description = "Optional target ElementType id." },
                    pinned = new { type = "boolean", description = "Optional pinned state." },
                    parameters = new { type = "object", description = "Optional object of parameter display names to new values." },
                    parameterTarget = new { type = "string", description = "Parameter target: instance or type. Default: instance.", @default = "instance" }
                },
                required = new[] { "elementId" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<UpdateElementRequest>() ?? new UpdateElementRequest();
                var result = await _revitService.UpdateElementAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("UpdateElementHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to update element: {ex.Message}");
            }
        }
    }
}
