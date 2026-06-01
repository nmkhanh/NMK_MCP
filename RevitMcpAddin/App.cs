using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit.Async;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Mcp.Handlers;
using RevitMcpAddin.Ribbon;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin
{
    // ═══════════════════════════════════════════════════════════════════════
    //  App — Revit IExternalApplication entry point
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Add-in entry point.  Bootstraps all services and starts the embedded
    /// MCP HTTP server when Revit launches.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class App : IExternalApplication
    {
        #region Singleton access (used by ViewModel / Command)

        public static App?             Instance    { get; private set; }
        public static McpServer?       McpServer   { get; private set; }
        public static McpRouter?       McpRouter   { get; private set; }
        public static RevitService?    RevitSvc    { get; private set; }
        public static AsyncQueueService? QueueSvc  { get; private set; }

        #endregion

        #region IExternalApplication

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                Instance = this;

                // ── 1. Initialise Revit.Async (MUST be first) ────────────
                //       This patches Revit's idle loop to drain our task queue.
                RevitTask.Initialize(application);
                Logger.Info("RevitTask initialised.");

                // Enable file logging → %LocalAppData%\RevitMCP\revitmcp.log
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RevitMCP", "revitmcp.log");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                Logger.EnableFileLogging(logPath);

                // ── 2. Build service graph ───────────────────────────────
                QueueSvc  = new AsyncQueueService();
                RevitSvc  = new RevitService(QueueSvc);

                // ── 3. Wire up MCP router with all tool handlers ─────────
                McpRouter = new McpRouter()
                    .Register(new GetDocumentHandler(RevitSvc))
                    .Register(new GetElementsHandler(RevitSvc))
                    .Register(new CreateWallHandler(RevitSvc))
                    .Register(new SelectElementsHandler(RevitSvc))
                    .Register(new GetProjectInfoHandler(RevitSvc))
                    .Register(new ListCategoriesHandler(RevitSvc))
                    .Register(new ListElementTypesHandler(RevitSvc))
                    .Register(new ListFamilyTypesHandler(RevitSvc))
                    .Register(new GetSelectedElementsHandler(RevitSvc))
                    .Register(new FindElementsByParameterHandler(RevitSvc))
                    .Register(new GetLevelsHandler(RevitSvc))
                    .Register(new CreateLevelHandler(RevitSvc))
                    .Register(new UpdateLevelHandler(RevitSvc))
                    .Register(new GetGridsHandler(RevitSvc))
                    .Register(new CreateGridHandler(RevitSvc))
                    .Register(new UpdateGridHandler(RevitSvc))
                    .Register(new GetElementParametersHandler(RevitSvc))
                    .Register(new SetElementParameterHandler(RevitSvc))
                    .Register(new SetElementParametersHandler(RevitSvc))
                    .Register(new BatchSetParametersHandler(RevitSvc))
                    .Register(new GetViewsHandler(RevitSvc))
                    .Register(new CreateViewHandler(RevitSvc))
                    .Register(new DuplicateViewHandler(RevitSvc))
                    .Register(new UpdateViewHandler(RevitSvc))
                    .Register(new GetSheetsHandler(RevitSvc))
                    .Register(new CreateSheetHandler(RevitSvc))
                    .Register(new UpdateSheetHandler(RevitSvc))
                    .Register(new PlaceViewOnSheetHandler(RevitSvc))
                    .Register(new PlaceTitleViewOnSheetHandler(RevitSvc))
                    .Register(new RemoveViewFromSheetHandler(RevitSvc))
                    .Register(new GetViewSheetSetsHandler(RevitSvc))
                    .Register(new CreateViewSheetSetHandler(RevitSvc))
                    .Register(new AddViewsToViewSheetSetHandler(RevitSvc))
                    .Register(new AddSheetsToViewSheetSetHandler(RevitSvc))
                    .Register(new RemoveFromViewSheetSetHandler(RevitSvc))
                    .Register(new DeleteViewSheetSetHandler(RevitSvc))
                    .Register(new GetElementHandler(RevitSvc))
                    .Register(new GetElementLocationHandler(RevitSvc))
                    .Register(new GetElementGeometrySummaryHandler(RevitSvc))
                    .Register(new UpdateElementHandler(RevitSvc))
                    .Register(new ChangeElementTypeHandler(RevitSvc))
                    .Register(new CreateFamilyInstanceHandler(RevitSvc))
                    .Register(new DeleteElementsHandler(RevitSvc))
                    .Register(new MoveElementsHandler(RevitSvc))
                    .Register(new CopyElementsHandler(RevitSvc))
                    .Register(new RotateElementsHandler(RevitSvc))
                    .Register(new MirrorElementsHandler(RevitSvc))
                    .Register(new PinElementsHandler(RevitSvc))
                    .Register(new UnpinElementsHandler(RevitSvc))
                    .Register(new HideElementsInViewHandler(RevitSvc))
                    .Register(new UnhideElementsInViewHandler(RevitSvc))
                    .Register(new ValidateBatchOperationsHandler(RevitSvc))
                    .Register(new ApplyBatchOperationsHandler(RevitSvc))
                    .Register(new CreateMaterialHandler(RevitSvc))
                    .Register(new UpdateMaterialHandler(RevitSvc))
                    .Register(new DuplicateElementTypeHandler(RevitSvc))
                    .Register(new RenameElementTypeHandler(RevitSvc))
                    .Register(new SetTypeParameterHandler(RevitSvc))
                    .Register(new SetTypeParametersHandler(RevitSvc))
                    .Register(new ApplyViewTemplateHandler(RevitSvc))
                    .Register(new CreateViewTemplateHandler(RevitSvc))
                    .Register(new CreateTextNoteHandler(RevitSvc))
                    .Register(new UpdateTextNoteHandler(RevitSvc))
                    .Register(new CreateDetailLineHandler(RevitSvc))
                    .Register(new CreateModelLineHandler(RevitSvc))
                    .Register(new PlaceDoorHandler(RevitSvc))
                    .Register(new PlaceWindowHandler(RevitSvc))
                    .Register(new PlaceColumnHandler(RevitSvc))
                    .Register(new PlaceStructuralFramingHandler(RevitSvc))
                    .Register(new PlaceFurnitureHandler(RevitSvc))
                    .Register(new PlaceEquipmentHandler(RevitSvc))
                    .Register(new UpdateWallHandler(RevitSvc))
                    .Register(new CreateFloorHandler(RevitSvc))
                    .Register(new UpdateFloorHandler(RevitSvc))
                    .Register(new CreateCeilingHandler(RevitSvc))
                    .Register(new UpdateCeilingHandler(RevitSvc))
                    .Register(new CreateRoomHandler(RevitSvc))
                    .Register(new UpdateRoomHandler(RevitSvc))
                    .Register(new CreateRoofHandler(RevitSvc))
                    .Register(new UpdateRoofHandler(RevitSvc))
                    .Register(new CreateOpeningHandler(RevitSvc))
                    .Register(new UpdateOpeningHandler(RevitSvc))
                    .Register(new CreateAreaHandler(RevitSvc))
                    .Register(new UpdateAreaHandler(RevitSvc))
                    .Register(new CreateSpaceHandler(RevitSvc))
                    .Register(new UpdateSpaceHandler(RevitSvc))
                    .Register(new CreateTagHandler(RevitSvc))
                    .Register(new PlaceRoomTagHandler(RevitSvc))
                    .Register(new PlaceAreaTagHandler(RevitSvc))
                    .Register(new PlaceSpaceTagHandler(RevitSvc))
                    .Register(new CreateDimensionHandler(RevitSvc))
                    .Register(new CreateFilledRegionHandler(RevitSvc))
                    .Register(new GetFamilySymbolsHandler(RevitSvc))
                    .Register(new ActivateFamilySymbolHandler(RevitSvc))
                    .Register(new GetRevitLinksHandler(RevitSvc))
                    .Register(new GetDesignOptionsHandler(RevitSvc))
                    .Register(new CreateScheduleHandler(RevitSvc))
                    .Register(new UpdateScheduleHandler(RevitSvc))
                    .Register(new GetScheduleDataHandler(RevitSvc))
                    .Register(new ExportScheduleHandler(RevitSvc))
                    .Register(new LoadFamilyHandler(RevitSvc))
                    .Register(new ReloadFamilyHandler(RevitSvc))
                    .Register(new CreateGroupHandler(RevitSvc))
                    .Register(new UpdateGroupHandler(RevitSvc))
                    .Register(new CreateAssemblyHandler(RevitSvc))
                    .Register(new UpdateAssemblyHandler(RevitSvc))
                    .Register(new ReloadRevitLinkHandler(RevitSvc))
                    .Register(new ManageWorksetsHandler(RevitSvc))
                    .Register(new SetElementDesignOptionHandler(RevitSvc))
                    .Register(new PlaceMepFixtureHandler(RevitSvc))
                    .Register(new PlaceMechanicalEquipmentHandler(RevitSvc))
                    .Register(new PlaceElectricalEquipmentHandler(RevitSvc))
                    .Register(new GetConnectorsHandler(RevitSvc))
                    .Register(new CreatePipeHandler(RevitSvc))
                    .Register(new UpdatePipeHandler(RevitSvc))
                    .Register(new CreateDuctHandler(RevitSvc))
                    .Register(new UpdateDuctHandler(RevitSvc))
                    .Register(new CreateConduitHandler(RevitSvc))
                    .Register(new UpdateConduitHandler(RevitSvc))
                    .Register(new CreateCableTrayHandler(RevitSvc))
                    .Register(new UpdateCableTrayHandler(RevitSvc))
                    .Register(new ConnectMepElementsHandler(RevitSvc))
                    .Register(new DisconnectMepElementsHandler(RevitSvc))
                    .RegisterRebarHandlers(RevitSvc)
                    .RegisterViewOverrideHandlers(RevitSvc)
                    .Register(new PrintSheetHandler(RevitSvc))
                    .Register(new ExportCadHandler(RevitSvc))
                    .Register(new ListCadExportTemplatesHandler(RevitSvc));

                // ── 4. Create and start the embedded HTTP/SSE server ─────
                //  DefaultPrefix  = "http://+:5000/"      → LAN + loopback (needs URL ACL)
                //  LocalhostPrefix= "http://localhost:5000/" → loopback only (no ACL needed)
                //
                //  McpServer auto-falls back to LocalhostPrefix if wildcard ACL is missing.
                //  To grant the ACL permanently (run once as admin):
                //    netsh http add urlacl url=http://+:5000/ user=Everyone
                McpServer = new McpServer(McpRouter, McpServer.DefaultPrefix);
                //  Fire-and-forget: StartAsync runs the accept loop
                _ = McpServer.StartAsync();

                // ── 5. Build the Revit ribbon ────────────────────────────
                RibbonCreator.Create(application);

                Logger.Info("RevitMCP add-in started → MCP server at " + McpServer.Prefix);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error("App.OnStartup fatal error", ex);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                McpServer?.Stop();
                QueueSvc?.Dispose();

                Logger.Info("RevitMCP add-in shut down.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error("App.OnShutdown error", ex);
                return Result.Failed;
            }
        }

        #endregion
    }
}
