using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateAssemblyHandler : IToolHandler
    {
        private readonly RevitService _revitService;
        public CreateAssemblyHandler(RevitService revitService) => _revitService = revitService;
        public string ToolName => "create_assembly";
        public McpToolDefinition GetDefinition() => new() { Name = ToolName, Description = "Creates a Revit assembly from element ids.", InputSchema = AssemblySchema(create: true) };
        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try { return ToolHandlerResult.FromJson(await _revitService.CreateAssemblyAsync(arguments?.ToObject<CreateAssemblyRequest>() ?? new CreateAssemblyRequest(), cancellationToken)); }
            catch (Exception ex) { Logger.Error("CreateAssemblyHandler error", ex); return ToolHandlerResult.FromError($"Failed to create assembly: {ex.Message}"); }
        }
        internal static object AssemblySchema(bool create) => new
        {
            type = "object",
            properties = new
            {
                assemblyId = new { type = "string", description = "Assembly ElementId for update." },
                elementIds = new { type = "array", description = "ElementIds to assemble.", items = new { type = "string" } },
                namingCategoryId = new { type = "string", description = "Optional naming category id. Defaults to first member category." },
                name = new { type = "string", description = "Optional assembly type name." },
                addElementIds = new { type = "array", description = "ElementIds to add on update.", items = new { type = "string" } },
                removeElementIds = new { type = "array", description = "ElementIds to remove on update.", items = new { type = "string" } },
                parameters = new { type = "object", description = "Optional instance parameters to set on update." }
            },
            required = create ? new[] { "elementIds" } : new[] { "assemblyId" }
        };
    }
}
