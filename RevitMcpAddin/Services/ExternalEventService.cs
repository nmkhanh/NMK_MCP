using Autodesk.Revit.UI;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Services
{
    // ═══════════════════════════════════════════════════════════════════════
    //  ExternalEventService
    //  Alternative dispatcher using Revit's native IExternalEventHandler.
    //  Useful for fire-and-forget scenarios or when Revit.Async is unavailable.
    //  For normal MCP operations, prefer AsyncQueueService (RevitTask.RunAsync).
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Provides an <c>IExternalEventHandler</c>-based dispatcher for Revit API calls.
    /// Supports a single queued action at a time. Register with Revit in
    /// <c>App.OnStartup</c> after <c>RevitTask.Initialize</c>.
    /// </summary>
    public sealed class ExternalEventService : IDisposable
    {
        #region Fields

        private readonly RevitActionHandler _handler;
        private readonly ExternalEvent _externalEvent;

        #endregion

        #region Constructor

        /// <summary>
        /// MUST be called from the Revit main thread (e.g. inside
        /// <c>IExternalApplication.OnStartup</c>).
        /// </summary>
        public ExternalEventService()
        {
            _handler       = new RevitActionHandler();
            _externalEvent = ExternalEvent.Create(_handler);
            Logger.Info("ExternalEventService initialised.");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Post a synchronous Revit API action and await its result.
        /// Note: For async work (e.g. loading families), use AsyncQueueService instead.
        /// </summary>
        /// <typeparam name="T">Return type.</typeparam>
        /// <param name="action">Synchronous lambda executed on the Revit main thread.</param>
        public Task<T> RunAsync<T>(Func<UIApplication, T> action)
        {
            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

            _handler.SetWork(uiApp =>
            {
                try
                {
                    var result = action(uiApp);
                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            var status = _externalEvent.Raise();

            if (status == ExternalEventRequest.Denied)
            {
                tcs.TrySetException(new InvalidOperationException(
                    "ExternalEvent.Raise() was denied. Revit may not be in an idle state."));
            }

            return tcs.Task.ContinueWith(t => (T)t.Result!);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _externalEvent.Dispose();
            Logger.Info("ExternalEventService disposed.");
        }

        #endregion
    }

    // ───────────────────────────────────────────────────────────────────────
    //  Internal event handler
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>Internal <see cref="IExternalEventHandler"/> used by <see cref="ExternalEventService"/>.</summary>
    internal sealed class RevitActionHandler : IExternalEventHandler
    {
        private Action<UIApplication>? _work;
        private readonly object _lock = new();

        public string GetName() => "RevitMCP_ExternalEventHandler";

        /// <summary>Set the action to run next. Thread-safe.</summary>
        public void SetWork(Action<UIApplication> work)
        {
            lock (_lock)
                _work = work;
        }

        /// <summary>Called by Revit on the main thread when the external event fires.</summary>
        public void Execute(UIApplication app)
        {
            Action<UIApplication>? work;
            lock (_lock)
            {
                work  = _work;
                _work = null;
            }

            try
            {
                work?.Invoke(app);
            }
            catch (Exception ex)
            {
                Logger.Error("ExternalEventHandler.Execute error", ex);
            }
        }
    }
}
