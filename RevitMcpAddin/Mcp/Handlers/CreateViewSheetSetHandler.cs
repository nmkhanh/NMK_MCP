using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateViewSheetSetHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateViewSheetSetHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_view_sheet_set";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Creates a named ViewSheetSet from viewIds and/or sheetIds.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string", description = "ViewSheetSet name." },
                    viewIds = new { type = "array", items = new { type = "string" }, description = "View ids to include." },
                    sheetIds = new { type = "array", items = new { type = "string" }, description = "Sheet ids to include." },
                    replaceExisting = new { type = "boolean", description = "Replace an existing set with the same name.", @default = false }
                },
                required = new[] { "name" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<CreateViewSheetSetRequest>() ?? new CreateViewSheetSetRequest();
                if (string.IsNullOrWhiteSpace(request.Name)) return ToolHandlerResult.FromError("'name' is required.");
                return ToolHandlerResult.FromJson(await _revitService.CreateViewSheetSetAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("CreateViewSheetSetHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to create ViewSheetSet: {ex.Message}");
            }
        }
    }
}
