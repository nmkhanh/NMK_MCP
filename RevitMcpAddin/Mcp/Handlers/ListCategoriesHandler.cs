using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class ListCategoriesHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public ListCategoriesHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "list_categories";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Lists Revit categories, optionally filtered by categoryType.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    categoryType = new { type = "string", description = "Optional: model, annotation, analytical, or internal." }
                },
                required = Array.Empty<string>()
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<ListCategoriesRequest>() ?? new ListCategoriesRequest();
                var result = await _revitService.ListCategoriesAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("ListCategoriesHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to list categories: {ex.Message}");
            }
        }
    }
}
