using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class ListFamilyTypesHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public ListFamilyTypesHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "list_family_types";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Lists Revit FamilySymbol ids for placement tools, optionally filtered by category and family name.",
            InputSchema = ListElementTypesHandler.ElementTypeSchema()
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<ListElementTypesRequest>() ?? new ListElementTypesRequest();
                var result = await _revitService.ListFamilyTypesAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("ListFamilyTypesHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to list family types: {ex.Message}");
            }
        }
    }
}
