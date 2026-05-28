using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateSheetHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateSheetHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_sheet";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Creates a Revit sheet using a title block type id/name or the first available title block.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    sheetNumber = new { type = "string", description = "Optional sheet number." },
                    sheetName = new { type = "string", description = "Optional sheet name." },
                    titleBlockTypeId = new { type = "string", description = "Optional title block FamilySymbol ElementId." },
                    titleBlockTypeName = new { type = "string", description = "Optional title block type name or 'Family: Type'." }
                },
                required = new string[] { }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<CreateSheetRequest>() ?? new CreateSheetRequest();
                return ToolHandlerResult.FromJson(await _revitService.CreateSheetAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("CreateSheetHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to create sheet: {ex.Message}");
            }
        }
    }
}
