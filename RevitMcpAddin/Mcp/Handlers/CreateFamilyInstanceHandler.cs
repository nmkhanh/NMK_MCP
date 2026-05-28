using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class CreateFamilyInstanceHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public CreateFamilyInstanceHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "create_family_instance";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Creates a Revit FamilyInstance from a FamilySymbol at a point, with optional level, host, structural type, and parameters.",
            InputSchema = FamilyInstanceSchema()
        };

        internal static object FamilyInstanceSchema() => new
        {
            type = "object",
            properties = new
            {
                symbolId = new { type = "string", description = "FamilySymbol ElementId to place." },
                x = new { type = "number", description = "Location X." },
                y = new { type = "number", description = "Location Y." },
                z = new { type = "number", description = "Location Z." },
                unit = new { type = "string", description = "Coordinate unit: feet, meters, or millimeters. Default: feet.", @default = "feet" },
                levelId = new { type = "string", description = "Optional Level ElementId for level-based families." },
                hostId = new { type = "string", description = "Optional host ElementId for hosted families." },
                structuralType = new { type = "string", description = "NonStructural, Beam, Brace, Column, or Footing. Default: NonStructural.", @default = "NonStructural" },
                parameters = new { type = "object", description = "Optional instance parameters to set after creation." }
            },
            required = new[] { "symbolId", "x", "y", "z" }
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<CreateFamilyInstanceRequest>() ?? new CreateFamilyInstanceRequest();
                var result = await _revitService.CreateFamilyInstanceAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("CreateFamilyInstanceHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to create family instance: {ex.Message}");
            }
        }
    }
}
