using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class FindElementsByParameterHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public FindElementsByParameterHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "find_elements_by_parameter";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Finds elements by an instance or type parameter value, optionally filtered by category.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    parameterName = new { type = "string", description = "Parameter display name." },
                    value = new { type = "string", description = "Optional value to match. Omit to find elements that have the parameter." },
                    category = new { type = "string", description = "Optional BuiltInCategory suffix, e.g. Walls, Doors, Rooms." },
                    comparison = new { type = "string", description = "equals, contains, startsWith, endsWith, or notEquals. Default: equals.", @default = "equals" },
                    includeParameters = new { type = "boolean", description = "Include a small parameter sample in results. Default: false.", @default = false },
                    maxItems = new { type = "integer", description = "Safety cap. Default: 200, hard max: 2000.", @default = 200 }
                },
                required = new[] { "parameterName" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<FindElementsByParameterRequest>() ?? new FindElementsByParameterRequest();
                var result = await _revitService.FindElementsByParameterAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("FindElementsByParameterHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to find elements by parameter: {ex.Message}");
            }
        }
    }
}
