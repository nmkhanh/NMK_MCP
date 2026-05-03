using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    /// <summary>
    /// MCP tool: <b>get_active_document</b>
    /// Returns metadata about the currently active Revit document.
    ///
    /// Input schema: {} (no arguments required)
    ///
    /// Output JSON:
    /// <code>
    /// {
    ///   "title":          "MyProject",
    ///   "filePath":       "C:\...\MyProject.rvt",
    ///   "isModified":     false,
    ///   "isWorkshared":   false,
    ///   "activeViewName": "Level 1",
    ///   "activeViewType": "FloorPlan",
    ///   "elementCount":   4231,
    ///   "revitVersion":   "Autodesk Revit 2026"
    /// }
    /// </code>
    /// </summary>
    public sealed class GetDocumentHandler : IToolHandler
    {
        #region Fields

        private readonly RevitService _revitService;

        #endregion

        #region Constructor

        public GetDocumentHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        #endregion

        #region IToolHandler

        public string ToolName => "get_active_document";

        public McpToolDefinition GetDefinition() => new()
        {
            Name        = ToolName,
            Description = "Returns metadata about the currently active Revit document: " +
                          "title, file path, view, element count, and Revit version.",
            InputSchema = new
            {
                type       = "object",
                properties = new { },
                required   = Array.Empty<string>()
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(
            JObject? arguments,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var docInfo = await _revitService.GetDocumentInfoAsync(cancellationToken);
                Logger.Info($"get_active_document: '{docInfo.Title}'");
                return ToolHandlerResult.FromJson(docInfo);
            }
            catch (Exception ex)
            {
                Logger.Error("GetDocumentHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to get document info: {ex.Message}");
            }
        }

        #endregion
    }
}
