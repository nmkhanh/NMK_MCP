using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateModelLineHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateModelLineHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_model_line";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Creates a model line on a horizontal sketch plane.",
            InputSchema = CreateDetailLineHandler.LineSchema(requireView: false)
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<CreateLineRequest>() ?? new CreateLineRequest();
                return ToolHandlerResult.FromJson(await _revitService.CreateModelLineAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("CreateModelLineHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to create model line: {ex.Message}");
            }
        }
    }
}
