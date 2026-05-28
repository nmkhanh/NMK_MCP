using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public sealed class ApplyBatchOperationsHandler : IToolHandler
    {
        private readonly RevitService _revitService;

        public ApplyBatchOperationsHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        public string ToolName => "apply_batch_operations";

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = "Applies a list of supported element operations. Use dryRun=true to validate without changing the model.",
            InputSchema = BatchOperationsSchema()
        };

        public async Task<ToolHandlerResult> HandleAsync(JObject? arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<BatchOperationsRequest>() ?? new BatchOperationsRequest();
                var result = await _revitService.ApplyBatchOperationsAsync(request, cancellationToken);
                return ToolHandlerResult.FromJson(result);
            }
            catch (Exception ex)
            {
                Logger.Error("ApplyBatchOperationsHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to apply batch operations: {ex.Message}");
            }
        }

        internal static object BatchOperationsSchema() => new
        {
            type = "object",
            properties = new
            {
                operations = new
                {
                    type = "array",
                    description = "Batch items. Each item accepts operation plus either an arguments object or flattened arguments.",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            operation = new
                            {
                                type = "string",
                                description = "Supported: create_family_instance, update_element, change_element_type, delete_elements, move_elements, copy_elements, rotate_elements, mirror_elements, pin_elements, unpin_elements, hide_elements_in_view, unhide_elements_in_view."
                            },
                            arguments = new
                            {
                                type = "object",
                                description = "Arguments matching the selected operation. Flattened arguments are also accepted."
                            }
                        },
                        required = new[] { "operation" }
                    }
                },
                dryRun = new { type = "boolean", description = "Validate without modifying the model. Default: false for apply_batch_operations.", @default = false },
                continueOnError = new { type = "boolean", description = "Continue after a failed operation. Default: false.", @default = false },
                maxOperations = new { type = "integer", description = "Safety cap. Default: 25, hard max: 100.", @default = 25 }
            },
            required = new[] { "operations" }
        };
    }
}
