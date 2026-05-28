using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class UpdateAssemblyHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public UpdateAssemblyHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "update_assembly";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Updates a Revit assembly name, members, and/or parameters.", InputSchema = CreateAssemblyHandler.AssemblySchema(create: false) };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.UpdateAssemblyAsync(arguments?.ToObject<UpdateAssemblyRequest>() ?? new UpdateAssemblyRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("UpdateAssemblyHandler error", ex); return ToolHandlerResult.FromError($"Failed to update assembly: {ex.Message}"); }
        }
    }
}
