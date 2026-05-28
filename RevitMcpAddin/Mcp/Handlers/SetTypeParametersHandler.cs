using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class SetTypeParametersHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public SetTypeParametersHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "set_type_parameters";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Sets multiple writable parameters on an ElementType.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    typeId = new { type = "string", description = "ElementType id." },
                    parameters = new { type = "object", description = "Object whose keys are parameter names and values are new values." }
                },
                required = new[] { "typeId", "parameters" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<SetTypeParametersRequest>() ?? new SetTypeParametersRequest();
                return ToolHandlerResult.FromJson(await _revitService.SetTypeParametersAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("SetTypeParametersHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to set type parameters: {ex.Message}");
            }
        }
    }
}
