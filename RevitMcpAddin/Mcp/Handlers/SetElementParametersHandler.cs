using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class SetElementParametersHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public SetElementParametersHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "set_element_parameters";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Sets multiple writable instance or type parameters on one Revit element.",
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
                    parameters = new
                    {
                        type = "object",
                        description = "Object whose keys are parameter display names and values are new parameter values."
                    },
                    target = new
                    {
                        type = "string",
                        description = "Parameter target: instance or type. Default: instance.",
                        @default = "instance"
                    }
                },
                required = new[] { "elementId", "parameters" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<SetElementParametersRequest>() ?? new SetElementParametersRequest();
                if (string.IsNullOrWhiteSpace(request.ElementId))
                    return ToolHandlerResult.FromError("'elementId' is required.");
                if (request.Parameters.Count == 0)
                    return ToolHandlerResult.FromError("'parameters' must contain at least one parameter.");

                var result = await _revitService.SetElementParametersAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("SetElementParametersHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to set element parameters: {ex.Message}");
            }
        }
    }
}
