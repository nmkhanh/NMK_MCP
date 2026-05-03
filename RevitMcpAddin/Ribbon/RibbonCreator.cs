using Autodesk.Revit.UI;
using RevitMcpAddin.Utils;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace RevitMcpAddin.Ribbon
{
    /// <summary>
    /// Builds the RevitMCP ribbon tab/panel/button in the Revit UI.
    /// Called once from <see cref="App.OnStartup"/>.
    /// </summary>
    public static class RibbonCreator
    {
        #region Constants

        private const string TabName   = "MCP";
        private const string PanelName = "Claude Tools";

        #endregion

        #region Create

        /// <summary>
        /// Create (or reuse) the MCP ribbon tab and populate it with buttons.
        /// Idempotent — safe to call multiple times.
        /// </summary>
        public static void Create(UIControlledApplication app)
        {
            try
            {
                // ── Tab ──────────────────────────────────────────────────
                try
                {
                    app.CreateRibbonTab(TabName);
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException)
                {
                    // Tab already exists (e.g. add-in reloaded) — harmless
                }

                // ── Panel ────────────────────────────────────────────────
                var panel = app.CreateRibbonPanel(TabName, PanelName);

                // ── Open MCP Panel button ───────────────────────────────
                var openBtn = new PushButtonData(
                    name:  "OpenMcpPanel",
                    text:          "Open MCP\nPanel",
                    assemblyName:  Assembly.GetExecutingAssembly().Location,
                    className:     "RevitMcpAddin.OpenMcpPanelCommand")
                {
                    ToolTip            = "Open the RevitMCP control panel to manage the embedded MCP server.",
                    LongDescription    = "Launches the RevitMCP panel showing server status, " +
                                        "available tools, and the request log.",
                    Image              = LoadBitmap("RevitMcpAddin.Resources.icon_16.png"),
                    LargeImage         = LoadBitmap("RevitMcpAddin.Resources.icon_32.png")
                };

                panel.AddItem(openBtn);

                Logger.Info($"Ribbon tab '{TabName}' / panel '{PanelName}' created.");
            }
            catch (Exception ex)
            {
                Logger.Error("RibbonCreator.Create failed", ex);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Attempt to load an embedded PNG bitmap. Returns null if not found
        /// (button still works, just without icon).
        /// </summary>
        private static BitmapImage? LoadBitmap(string resourcePath)
        {
            try
            {
                var asm    = Assembly.GetExecutingAssembly();
                var stream = asm.GetManifestResourceStream(resourcePath);
                if (stream == null) return null;

                var img = new BitmapImage();
                img.BeginInit();
                img.StreamSource   = stream;
                img.CacheOption    = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch
            {
                return null; // Missing icon is non-fatal
            }
        }

        #endregion
    }
}
