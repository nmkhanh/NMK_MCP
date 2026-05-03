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

                // Optional: enable file logging
                // var logPath = Path.Combine(
                //     Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                //     "RevitMCP", "revitmcp.log");
                // Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                // Logger.EnableFileLogging(logPath);

                // ── 2. Build service graph ───────────────────────────────
                QueueSvc  = new AsyncQueueService();
                RevitSvc  = new RevitService(QueueSvc);

                // ── 3. Wire up MCP router with all tool handlers ─────────
                McpRouter = new McpRouter()
                    .Register(new GetDocumentHandler(RevitSvc))
                    .Register(new GetElementsHandler(RevitSvc))
                    .Register(new CreateWallHandler(RevitSvc))
                    .Register(new SelectElementsHandler(RevitSvc));

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
