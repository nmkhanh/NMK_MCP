using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcpAddin.Utils;
using RevitMcpAddin.ViewModels;
using RevitMcpAddin.Views;

namespace RevitMcpAddin
{
    // ═══════════════════════════════════════════════════════════════════════
    //  OpenMcpPanelCommand — IExternalCommand
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Opened by the ribbon button "Open MCP Panel".
    /// Shows the RevitMCP control window as a modeless dialog.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class OpenMcpPanelCommand : IExternalCommand
    {
        // Keep a single instance open (modeless)
        private static MainView? _panel;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                // Re-focus if already open
                if (_panel != null && _panel.IsLoaded)
                {
                    _panel.Activate();
                    return Result.Succeeded;
                }

                var viewModel = new MainViewModel();
                _panel        = new MainView(viewModel);

                // Modeless — use Show() not ShowDialog()
                _panel.Closed += (_, _) => _panel = null;
                _panel.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error("OpenMcpPanelCommand failed", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
