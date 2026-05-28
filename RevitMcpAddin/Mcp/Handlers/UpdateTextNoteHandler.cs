using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateTextNoteHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public UpdateTextNoteHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "update_text_note";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Updates text, position, and/or type of a Revit TextNote.",
            InputSchema = CreateTextNoteHandler.TextNoteSchema(create: false)
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<UpdateTextNoteRequest>() ?? new UpdateTextNoteRequest();
                return ToolHandlerResult.FromJson(await _revitService.UpdateTextNoteAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("UpdateTextNoteHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to update text note: {ex.Message}");
            }
        }
    }
}
