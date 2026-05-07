namespace RevitMcpAddin.Services
{
    // -----------------------------------------------------------------------
    //  RevitService � Hub / entry point
    //
    //  This file is intentionally thin.  All domain functions are implemented
    //  as partial-class extensions in the Services/Functions/ subfolder:
    //
    //    F_Document.cs   � GetDocumentInfoAsync
    //    F_Elements.cs   � GetElementsAsync
    //    F_Walls.cs      � CreateWallAsync
    //    F_Selection.cs  � SelectElementsAsync
    //    F_Print.cs      � PrintSheetToPdfAsync
    //
    //  All public methods enqueue work via AsyncQueueService ? RevitTask,
    //  making them safe to call from any thread.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Provides domain-level Revit operations consumed by MCP tool handlers.
    /// Never call Revit API directly from handlers; use this service instead.
    /// </summary>
    public sealed partial class RevitService
    {
        #region Fields

        private readonly AsyncQueueService _queue;

        #endregion

        #region Constructor

        public RevitService(AsyncQueueService queue)
        {
            _queue = queue;
        }

        #endregion
    }
}
