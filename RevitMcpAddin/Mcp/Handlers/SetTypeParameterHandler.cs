using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class SetTypeParameterHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public SetTypeParameterHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "set_type_parameter";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Sets one writable parameter on an ElementType.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    typeId = new { type = "string", description = "ElementType id." },
                    parameterName = new { type = "string", description = "Parameter display name." },
                    value = new { description = "New parameter value." }
                },
                required = new[] { "typeId", "parameterName", "value" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<SetTypeParameterRequest>() ?? new SetTypeParameterRequest();
                return ToolHandlerResult.FromJson(await _revitService.SetTypeParameterAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("SetTypeParameterHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to set type parameter: {ex.Message}");
            }
        }
    }
}
