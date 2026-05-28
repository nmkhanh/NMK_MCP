using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateTextNoteHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateTextNoteHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_text_note";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Creates a Revit TextNote in a view.",
            InputSchema = TextNoteSchema(create: true)
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<CreateTextNoteRequest>() ?? new CreateTextNoteRequest();
                return ToolHandlerResult.FromJson(await _revitService.CreateTextNoteAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.Error("CreateTextNoteHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to create text note: {ex.Message}");
            }
        }

        internal static object TextNoteSchema(bool create) => new
        {
            type = "object",
            properties = new
            {
                textNoteId = new { type = "string", description = "TextNote id for update." },
                viewId = new { type = "string", description = "Target view id for create." },
                text = new { type = "string", description = "Text content." },
                x = new { type = "number", description = "X coordinate." },
                y = new { type = "number", description = "Y coordinate." },
                z = new { type = "number", description = "Z coordinate." },
                unit = new { type = "string", description = "Coordinate unit: feet, meters, or millimeters. Default: feet.", @default = "feet" },
                textNoteTypeId = new { type = "string", description = "Optional TextNoteType id." }
            },
            required = create ? new[] { "viewId", "text", "x", "y", "z" } : new[] { "textNoteId" }
        };
    }
}
