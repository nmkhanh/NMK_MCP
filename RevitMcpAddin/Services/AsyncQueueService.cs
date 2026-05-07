using System.Threading.Channels;
using Autodesk.Revit.UI;
using RevitMcpAddin.Utils;
using Revit.Async;

namespace RevitMcpAddin.Services
{
    // ═══════════════════════════════════════════════════════════════════════
    //  AsyncQueueService
    //  Queues Revit API work items through a bounded Channel to:
    //    1. Prevent flooding the Revit main thread
    //    2. Apply per-request timeouts
    //    3. Support cancellation
    //    4. Serialise access (single processor)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Thread-safe dispatcher that serialises all Revit API calls onto the
    /// Revit main thread using <c>RevitTask.RunAsync</c>.
    ///
    /// Usage:
    /// <code>
    /// var result = await _queue.EnqueueAsync&lt;MyDto&gt;(async uiApp =>
    /// {
    ///     var doc = uiApp.ActiveUIDocument.Document;
    ///     return await Task.FromResult(BuildDto(doc));
    /// }, cancellationToken);
    /// </code>
    /// </summary>
    public sealed class AsyncQueueService : IDisposable
    {
        #region Constants

        /// <summary>Maximum number of waiting requests before backpressure kicks in.</summary>
        private const int MaxQueueDepth = 100;

        /// <summary>Default request timeout in milliseconds.</summary>
        public const int DefaultTimeoutMs = 30_000;

        #endregion

        #region Fields

        private readonly Channel<QueuedWork> _channel;
        private readonly CancellationTokenSource _serviceCts = new();
        private readonly Task _processorTask;

        #endregion

        #region Constructor

        public AsyncQueueService()
        {
            _channel = Channel.CreateBounded<QueuedWork>(new BoundedChannelOptions(MaxQueueDepth)
            {
                FullMode    = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });

            // Start the single background processor
            _processorTask = Task.Run(() => ProcessQueueAsync(_serviceCts.Token));
            Logger.Info("AsyncQueueService started (queue capacity=" + MaxQueueDepth + ")");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Enqueue a Revit API work item and await its result.
        /// Throws <see cref="TimeoutException"/> if not completed within <paramref name="timeoutMs"/>.
        /// Throws <see cref="OperationCanceledException"/> if <paramref name="ct"/> is cancelled.
        /// </summary>
        /// <typeparam name="T">Return type of the work item.</typeparam>
        /// <param name="work">Delegate executed on the Revit main thread.</param>
        /// <param name="ct">Caller cancellation token.</param>
        /// <param name="timeoutMs">Per-item timeout in milliseconds.</param>
        public async Task<T> EnqueueAsync<T>(
            Func<UIApplication, Task<T>> work,
            CancellationToken ct = default,
            int timeoutMs = DefaultTimeoutMs)
        {
            // Fail fast: if the processor has already died, there is no point waiting.
            if (_processorTask.IsCompleted)
                throw new InvalidOperationException(
                    "Revit API queue processor has stopped unexpectedly. " +
                    "Please restart the add-in (toggle the MCP server button in the ribbon).");

            // Combine caller ct + service ct + per-item timeout
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(
                ct, _serviceCts.Token, timeoutCts.Token);

            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Box the typed delegate to object? to keep the channel homogeneous
            async Task<object?> BoxedWork(UIApplication app) => await work(app);

            var item = new QueuedWork(BoxedWork, tcs);

            try
            {
                await _channel.Writer.WriteAsync(item, linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                throw new TimeoutException($"Request timed out waiting in queue after {timeoutMs} ms.");
            }

            // Now wait for the processor to complete the work
            try
            {
                var rawResult = await tcs.Task.WaitAsync(linkedCts.Token);
                return (T)rawResult!;
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                tcs.TrySetCanceled();
                throw new TimeoutException($"Revit API call timed out after {timeoutMs} ms.");
            }
        }

        #endregion

        #region Queue Processor (single consumer)

        private async Task ProcessQueueAsync(CancellationToken ct)
        {
            Logger.Info("AsyncQueueService processor started.");

            // Restart loop — if an unexpected exception escapes ExecuteWorkItemAsync,
            // the processor restarts instead of dying silently (which would block all
            // subsequent requests forever).
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await foreach (var item in _channel.Reader.ReadAllAsync(ct))
                    {
                        await ExecuteWorkItemAsync(item, ct);
                    }
                    break; // channel completed normally (service disposed)
                }
                catch (OperationCanceledException)
                {
                    break; // service is shutting down
                }
                catch (Exception ex)
                {
                    Logger.Error("AsyncQueueService processor error — restarting in 500 ms", ex);
                    try { await Task.Delay(500, ct); } catch { break; }
                }
            }

            Logger.Info("AsyncQueueService processor stopped.");
        }

        /// <summary>
        /// Executes a single queued item on the Revit main thread.
        /// <para>
        /// The TCS is completed INSIDE the <c>RevitTask.RunAsync</c> lambda so that
        /// exceptions from the work delegate are captured reliably — bypassing any
        /// Revit.Async internal exception-propagation quirks that could leave the
        /// returned Task permanently pending and cause a silent hang.
        /// </para>
        /// <para>
        /// A 60 s safety timeout guards against Revit being stuck in a modal dialog
        /// or an Idling-event blackout.  Stale items (caller already timed out) are
        /// skipped inside the lambda so Revit's internal dispatch queue drains fast.
        /// </para>
        /// </summary>
        private static async Task ExecuteWorkItemAsync(QueuedWork item, CancellationToken serviceCt)
        {
            // Fast path: caller already timed out before we even dequeued this item.
            if (item.Tcs.Task.IsCompleted)
            {
                Logger.Info("Skipping already-completed (timed-out) work item.");
                return;
            }

            Logger.Info("ExecuteWorkItemAsync: dispatching via RevitTask.RunAsync…");

            using var safetyTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using var safetyCts = CancellationTokenSource.CreateLinkedTokenSource(
                safetyTimeoutCts.Token, serviceCt);

            // Fire-and-forget: result/exception are set on the TCS directly inside
            // the lambda, so we don't depend on RevitTask.RunAsync propagating them.
            _ = RevitTask.RunAsync(async uiApp =>
            {
                // Guard: item may have timed out while waiting in Revit.Async's
                // internal dispatch queue.  Skip the work so the queue drains fast.
                if (item.Tcs.Task.IsCompleted)
                {
                    Logger.Info("Skipping stale work item inside Revit.Async queue.");
                    return;
                }
                try
                {
                    var result = await item.Work(uiApp);
                    Logger.Info("ExecuteWorkItemAsync: work completed successfully.");
                    item.Tcs.TrySetResult(result);
                }
                catch (OperationCanceledException ex)
                {
                    item.Tcs.TrySetCanceled(ex.CancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.Error("Revit work item threw an exception", ex);
                    item.Tcs.TrySetException(ex);
                }
            });

            // Wait for the TCS (populated by the lambda above).
            try
            {
                await item.Tcs.Task.WaitAsync(safetyCts.Token);
            }
            catch (OperationCanceledException) when (!serviceCt.IsCancellationRequested)
            {
                // 60 s safety timeout: Revit is blocked (modal dialog, long operation).
                Logger.Error(
                    "ExecuteWorkItemAsync: 60 s safety timeout. " +
                    "Revit may be in a modal state or the Idling event is suppressed.");
                item.Tcs.TrySetException(
                    new TimeoutException(
                        "Revit did not execute the queued work within 60 s. " +
                        "Ensure Revit is idle (no modal dialogs) and the add-in is running."));
            }
            catch (OperationCanceledException)
            {
                // Service is shutting down.
                item.Tcs.TrySetCanceled(serviceCt);
            }
            catch
            {
                // TCS was faulted by the lambda (work item exception).
                // Already captured on the TCS — swallow here to keep the processor alive.
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _channel.Writer.TryComplete();
            _serviceCts.Cancel();
            _serviceCts.Dispose();
            Logger.Info("AsyncQueueService disposed.");
        }

        #endregion
    }

    // ───────────────────────────────────────────────────────────────────────
    //  Internal queue item
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>A single queued unit of Revit API work.</summary>
    internal sealed class QueuedWork
    {
        /// <summary>The Revit API lambda to execute on the main thread.</summary>
        public Func<UIApplication, Task<object?>> Work { get; }

        /// <summary>Completion source the caller awaits.</summary>
        public TaskCompletionSource<object?> Tcs { get; }

        public QueuedWork(Func<UIApplication, Task<object?>> work, TaskCompletionSource<object?> tcs)
        {
            Work = work;
            Tcs  = tcs;
        }
    }
}
