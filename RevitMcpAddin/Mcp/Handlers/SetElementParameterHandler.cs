using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class SetElementParameterHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public SetElementParameterHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "set_element_parameter";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Sets one writable instance or type parameter on a Revit element.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    elementId = new
                    {
                        type = "string",
                        description = "Revit ElementId of the element to update."
                    },
                    parameterName = new
                    {
                        type = "string",
                        description = "Parameter display name."
                    },
                    value = new
                    {
                        description = "New parameter value. JSON type is converted based on the Revit storage type."
                    },
                    target = new
                    {
                        type = "string",
                        description = "Parameter target: instance or type. Default: instance.",
                        @default = "instance"
                    }
                },
                required = new[] { "elementId", "parameterName", "value" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<SetElementParameterRequest>() ?? new SetElementParameterRequest();
                if (string.IsNullOrWhiteSpace(request.ElementId))
                    return ToolHandlerResult.FromError("'elementId' is required.");
                if (string.IsNullOrWhiteSpace(request.ParameterName))
                    return ToolHandlerResult.FromError("'parameterName' is required.");
                if (request.Value == null)
                    return ToolHandlerResult.FromError("'value' is required.");

                var result = await _revitService.SetElementParameterAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("SetElementParameterHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to set element parameter: {ex.Message}");
            }
        }
    }
}
