using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class LoadFamilyHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public LoadFamilyHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "load_family";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Loads a Revit family file into the active document.", InputSchema = FamilyLoadSchema() };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.LoadFamilyAsync(arguments?.ToObject<LoadFamilyRequest>() ?? new LoadFamilyRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("LoadFamilyHandler error", ex); return ToolHandlerResult.FromError($"Failed to load family: {ex.Message}"); }
        }
        internal static object FamilyLoadSchema() => new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "Absolute .rfa file path." },
                overwriteExisting = new { type = "boolean", description = "Overwrite existing family when found. Default: true.", @default = true },
                overwriteParameterValues = new { type = "boolean", description = "Overwrite parameter values when reloading. Default: true.", @default = true }
            },
            required = new[] { "path" }
        };
    }
}
