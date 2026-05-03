using System.Windows;
using RevitMcpAddin.ViewModels;

namespace RevitMcpAddin.Views
{
    /// <summary>
    /// Code-behind for the MCP Control Panel window.
    /// All business logic lives in <see cref="MainViewModel"/>.
    /// </summary>
    public partial class MainView : Window
    {
        private readonly MainViewModel _viewModel;

        public MainView(MainViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.Dispose();
            base.OnClosed(e);
        }
    }
}
