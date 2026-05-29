using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Utils;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using Color = System.Windows.Media.Color;

namespace RevitMcpAddin.ViewModels
{
    /// <summary>
    /// ViewModel for the MCP Control Panel window.
    /// Binds to <see cref="Views.MainView"/> and subscribes to server events.
    /// Uses CommunityToolkit.Mvvm source generators for boilerplate reduction.
    /// </summary>
    public sealed partial class MainViewModel : ObservableObject, IDisposable
    {
        #region Observable Properties (source-generated)

        [ObservableProperty]
        private string _serverStatus = "Stopped";

        [ObservableProperty]
        private SolidColorBrush _serverStatusColor = new(Color.FromRgb(0xEF, 0x44, 0x44)); // red

        [ObservableProperty]
        private string _serverUrl = McpServer.DefaultPrefix;

        [ObservableProperty]
        private bool _isServerRunning;

        [ObservableProperty]
        private string _connectedClients = "0";

        /// <summary>Bound to the Request Log list.</summary>
        public ObservableCollection<LogEntry> RequestLog { get; } = new();

        /// <summary>Bound to the Tools list.</summary>
        public ObservableCollection<string> AvailableTools { get; } = new();

        #endregion

        #region Fields

        private readonly McpServer? _server;

        #endregion

        #region Constructor

        public MainViewModel()
        {
            // Access the running server from the static App instance
            _server = App.McpServer;

            if (_server != null)
            {
                _server.ServerStatusChanged += OnServerStatusChanged;
                _server.RequestLogged       += OnRequestLogged;
                RefreshServerState();
            }

            // Populate known tools from the router
            if (App.McpRouter != null)
            {
                foreach (var name in App.McpRouter.ToolNames)
                    AvailableTools.Add(name);
            }
        }

        #endregion

        #region Commands

        [RelayCommand(CanExecute = nameof(CanStartServer))]
        private async Task StartServerAsync()
        {
            try
            {
                if (_server == null) return;
                _ = Task.Run(() => _server.StartAsync());
                await Task.Delay(300); // Brief delay to let the server initialise
                RefreshServerState();
            }
            catch (Exception ex)
            {
                Logger.Error("StartServer command error", ex);
                SetStatus(false);
            }
        }

        private bool CanStartServer() => !IsServerRunning;

        [RelayCommand(CanExecute = nameof(CanStopServer))]
        private void StopServer()
        {
            try
            {
                _server?.Stop();
                SetStatus(false);
            }
            catch (Exception ex)
            {
                Logger.Error("StopServer command error", ex);
            }
        }

        private bool CanStopServer() => IsServerRunning;

        [RelayCommand]
        private void ClearLog()
        {
            RequestLog.Clear();
        }

        [RelayCommand]
        private void CopyUrl()
        {
            try
            {
                Clipboard.SetText(ServerUrl);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Copy URL failed: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        private void OnServerStatusChanged(bool isRunning)
        {
            Application.Current?.Dispatcher.InvokeAsync(() => SetStatus(isRunning));
        }

        private void OnRequestLogged(string message)
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                RequestLog.Insert(0, new LogEntry(DateTime.Now, message));

                // Keep log bounded to 200 entries
                while (RequestLog.Count > 200)
                    RequestLog.RemoveAt(RequestLog.Count - 1);
            });
        }

        #endregion

        #region Helpers

        private void RefreshServerState()
        {
            SetStatus(_server?.IsRunning ?? false);
        }

        private void SetStatus(bool running)
        {
            IsServerRunning   = running;
            ServerStatus      = running ? "Running" : "Stopped";
            ServerStatusColor = running
                ? new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)) // green
                : new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)); // red

            StartServerCommand.NotifyCanExecuteChanged();
            StopServerCommand.NotifyCanExecuteChanged();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_server != null)
            {
                _server.ServerStatusChanged -= OnServerStatusChanged;
                _server.RequestLogged       -= OnRequestLogged;
            }
        }

        #endregion
    }

    /// <summary>A single entry in the request log.</summary>
    public sealed class LogEntry
    {
        public string Timestamp { get; }
        public string Message   { get; }

        public LogEntry(DateTime ts, string message)
        {
            Timestamp = ts.ToString("HH:mm:ss.fff");
            Message   = message;
        }

        public override string ToString() => $"[{Timestamp}] {Message}";
    }
}
