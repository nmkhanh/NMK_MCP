using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class BatchSetParametersHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public BatchSetParametersHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "batch_set_parameters";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Sets the same parameter values on multiple Revit elements, with optional dry run.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    elementIds = new
                    {
                        type = "array",
                        description = "Revit ElementIds to update.",
                        items = new { type = "string" }
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
                    },
                    dryRun = new
                    {
                        type = "boolean",
                        description = "Validate parameter availability without writing changes. Default: false.",
                        @default = false
                    },
                    maxItems = new
                    {
                        type = "integer",
                        description = "Safety cap for element count. Default: 100, hard max: 500.",
                        @default = 100
                    }
                },
                required = new[] { "elementIds", "parameters" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<BatchSetParametersRequest>() ?? new BatchSetParametersRequest();
                if (request.ElementIds.Count == 0)
                    return ToolHandlerResult.FromError("'elementIds' must contain at least one id.");
                if (request.Parameters.Count == 0)
                    return ToolHandlerResult.FromError("'parameters' must contain at least one parameter.");

                var result = await _revitService.BatchSetParametersAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("BatchSetParametersHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to batch set parameters: {ex.Message}");
            }
        }
    }
}
