using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class ListElementTypesHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public ListElementTypesHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "list_element_types";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Lists Revit ElementType records, optionally filtered by category and family name.",
            InputSchema = ElementTypeSchema()
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<ListElementTypesRequest>() ?? new ListElementTypesRequest();
                var result = await _revitService.ListElementTypesAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("ListElementTypesHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to list element types: {ex.Message}");
            }
        }

        internal static object ElementTypeSchema() => new
        {
            type = "object",
            properties = new
            {
                category = new { type = "string", description = "Optional BuiltInCategory suffix, e.g. Walls, Doors, Windows." },
                familyName = new { type = "string", description = "Optional family name exact match." },
                maxItems = new { type = "integer", description = "Safety cap. Default: 500, hard max: 1000.", @default = 500 }
            },
            required = Array.Empty<string>()
        };
    }
}
