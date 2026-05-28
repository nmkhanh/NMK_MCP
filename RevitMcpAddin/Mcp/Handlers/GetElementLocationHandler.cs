using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class GetElementLocationHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public GetElementLocationHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "get_element_location";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Returns the LocationPoint or LocationCurve data for one Revit element.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    elementId = new { type = "string", description = "Revit ElementId to inspect." }
                },
                required = new[] { "elementId" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<ElementLocationRequest>() ?? new ElementLocationRequest();
                var result = await _revitService.GetElementLocationAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("GetElementLocationHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to get element location: {ex.Message}");
            }
        }
    }
}
