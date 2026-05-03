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
            Logger.Debug("AsyncQueueService processor started.");

            try
            {
                await foreach (var item in _channel.Reader.ReadAllAsync(ct))
                {
                    await ExecuteWorkItemAsync(item);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("AsyncQueueService processor stopped (cancellation).");
            }
            catch (Exception ex)
            {
                Logger.Error("AsyncQueueService processor fatal error", ex);
            }
        }

        private static async Task ExecuteWorkItemAsync(QueuedWork item)
        {
            try
            {
                // RevitTask.RunAsync dispatches to the Revit main thread
                var result = await RevitTask.RunAsync(async uiApp =>
                    await item.Work(uiApp));

                item.Tcs.TrySetResult(result);
            }
            catch (OperationCanceledException ex)
            {
                item.Tcs.TrySetCanceled(ex.CancellationToken);
            }
            catch (Exception ex)
            {
                Logger.Error("Queue item execution error", ex);
                item.Tcs.TrySetException(ex);
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
