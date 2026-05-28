using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class GetElementParametersHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public GetElementParametersHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "get_element_parameters";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Reads instance and optional type parameters for a Revit element.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    elementId = new
                    {
                        type = "string",
                        description = "Revit ElementId of the element to inspect."
                    },
                    includeTypeParameters = new
                    {
                        type = "boolean",
                        description = "Include parameters from the element type. Default: true.",
                        @default = true
                    }
                },
                required = new[] { "elementId" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<GetElementParametersRequest>() ?? new GetElementParametersRequest();
                if (string.IsNullOrWhiteSpace(request.ElementId))
                    return ToolHandlerResult.FromError("'elementId' is required.");

                var result = await _revitService.GetElementParametersAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("GetElementParametersHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to get element parameters: {ex.Message}");
            }
        }
    }
}
