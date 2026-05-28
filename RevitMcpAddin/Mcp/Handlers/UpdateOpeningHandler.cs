using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateOpeningHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public UpdateOpeningHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "update_opening";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Updates an opening type and/or instance parameters.", InputSchema = CreateOpeningHandler.OpeningSchema(create: false) };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.UpdateOpeningAsync(arguments?.ToObject<UpdateBuildingElementRequest>() ?? new UpdateBuildingElementRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("UpdateOpeningHandler error", ex); return ToolHandlerResult.FromError($"Failed to update opening: {ex.Message}"); }
        }
    }
}
