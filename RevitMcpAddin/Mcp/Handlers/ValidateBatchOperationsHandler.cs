using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class ValidateBatchOperationsHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public ValidateBatchOperationsHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "validate_batch_operations";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Validates a list of supported element operations without modifying the Revit model.",
            InputSchema = ApplyBatchOperationsHandler.BatchOperationsSchema()
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<BatchOperationsRequest>() ?? new BatchOperationsRequest();
                var result = await _revitService.ValidateBatchOperationsAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("ValidateBatchOperationsHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to validate batch operations: {ex.Message}");
            }
        }
    }
}
