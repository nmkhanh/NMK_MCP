using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp
{
    // ═══════════════════════════════════════════════════════════════════════
    //  MCP HTTP Server — SSE transport (spec 2024-11-05)
    //  Compatible with: Claude Code · Claude.ai web · ngrok tunnel
    // ═══════════════════════════════════════════════════════════════════════
    //
    //  Transport flow:
    //    1. Client  →  GET /sse                         (persistent SSE stream)
    //    2. Server  →  event: endpoint                  (POST URL for this session)
    //    3. Client  →  POST /messages?sessionId=<id>    (JSON-RPC request)
    //    4. Server  →  HTTP 202 Accepted                (immediate ack)
    //    5. Server  →  event: message  data: {json}     (JSON-RPC response over SSE)
    //
    //  ngrok usage:
    //    ngrok http 5000
    //    Add https://xxxx.ngrok-free.app/sse to Claude → Settings → Integrations
    //
    //  Wildcard prefix  http://+:5000/  requires a URL ACL reservation:
    //    netsh http add urlacl url=http://+:5000/ user=Everyone
    //  If you cannot run that command use the localhost-only prefix instead:
    //    http://localhost:5000/
    //  ngrok tunnels TO localhost, so both prefixes work for ngrok.
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Embedded MCP HTTP server running inside the Revit process.
    /// Implements MCP SSE transport (2024-11-05) with full ngrok / Claude-web
    /// compatibility.
    /// </summary>
    public sealed class McpServer : IDisposable
    {
        #region Constants

        /// <summary>
        /// Wildcard binding — accepts traffic from ngrok, LAN, loopback.
        /// Requires:  netsh http add urlacl url=http://+:5000/ user=Everyone
        /// Fall back to <see cref="LocalhostPrefix"/> if that command cannot be run.
        /// </summary>
        public const string DefaultPrefix   = "http://+:5000/";

        /// <summary>Localhost-only binding. Sufficient for ngrok; does not need ACL reservation.</summary>
        public const string LocalhostPrefix = "http://localhost:5000/";

        /// <summary>Keep-alive comment sent every 5 s to prevent proxy / ngrok timeout.</summary>
        private const int HeartbeatIntervalMs = 5_000;

        #endregion

        #region Fields

        private readonly string _prefix;
        private readonly McpRouter _router;

        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _acceptTask;

        /// <summary>Active SSE sessions keyed by sessionId.</summary>
        private readonly ConcurrentDictionary<string, SseSession> _sessions = new();

        #endregion

        #region Events (for UI binding)

        /// <summary>Raised on the thread pool whenever server status changes.</summary>
        public event Action<bool>? ServerStatusChanged;

        /// <summary>Raised whenever a request+response cycle completes.</summary>
        public event Action<string>? RequestLogged;

        #endregion

        #region Properties

        public bool IsRunning { get; private set; }
        public string Prefix => _prefix;

        #endregion

        #region Constructor

        /// <param name="router">Pre-configured tool router.</param>
        /// <param name="prefix">
        /// HTTP prefix. Default is <see cref="DefaultPrefix"/> (wildcard).
        /// Use <see cref="LocalhostPrefix"/> if you cannot reserve the URL ACL.
        /// </param>
        public McpServer(McpRouter router, string prefix = LocalhostPrefix)
        {
            _router = router;
            _prefix = prefix;
        }

        #endregion

        #region Start / Stop

        /// <summary>
        /// Start listening.  Fire-and-forget safe — call from <c>App.OnStartup</c>
        /// without awaiting.
        /// </summary>
        public async Task StartAsync(CancellationToken externalCt = default)
        {
            if (IsRunning) return;

            try
            {
                _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);

                _listener = new HttpListener();
                _listener.Prefixes.Add(_prefix);
                _listener.Start();

                IsRunning = true;
                ServerStatusChanged?.Invoke(true);
                Logger.Info($"McpServer started → {_prefix}  (ngrok: ngrok http 5000)");

                _acceptTask = AcceptLoopAsync(_cts.Token);
                await _acceptTask; // suspends until stopped
            }
            catch (HttpListenerException) when (_cts?.Token.IsCancellationRequested == true)
            {
                // Normal shutdown path — swallow
            }
            catch (HttpListenerException hlex) when (hlex.ErrorCode == 5 /* ACCESS_DENIED */)
            {
                // Wildcard binding denied — retry with localhost
                Logger.Warning("Wildcard binding denied (need URL ACL). Retrying on localhost:5000.");
                await StartWithFallbackAsync(externalCt);
            }
            catch (Exception ex)
            {
                Logger.Error("McpServer failed to start", ex);
                IsRunning = false;
                ServerStatusChanged?.Invoke(false);
                throw;
            }
        }

        /// <summary>Fallback: if wildcard ACL not set, bind to localhost only.</summary>
        private async Task StartWithFallbackAsync(CancellationToken ct)
        {
            _listener?.Close();
            _listener = new HttpListener();
            _listener.Prefixes.Add(LocalhostPrefix);

            try
            {
                _listener.Start();
                IsRunning = true;
                ServerStatusChanged?.Invoke(true);
                Logger.Info($"McpServer started (fallback) → {LocalhostPrefix}");
                await AcceptLoopAsync(_cts!.Token);
            }
            catch (Exception ex)
            {
                Logger.Error("McpServer fallback start failed", ex);
                IsRunning = false;
                ServerStatusChanged?.Invoke(false);
                throw;
            }
        }

        /// <summary>Gracefully stop the server and close all SSE sessions.</summary>
        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener?.Close();

                foreach (var session in _sessions.Values)
                    session.Dispose();
                _sessions.Clear();

                IsRunning = false;
                ServerStatusChanged?.Invoke(false);
                Logger.Info("McpServer stopped.");
            }
            catch (Exception ex)
            {
                Logger.Error("McpServer stop error", ex);
            }
        }

        #endregion

        #region Accept Loop

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener!.GetContextAsync().WaitAsync(ct);
                    // Handle each connection on its own task
                    _ = Task.Run(() => HandleContextAsync(context, ct), ct);
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException)    { break; }
                catch (HttpListenerException)  when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    Logger.Error("AcceptLoop error", ex);
                }
            }
        }

        #endregion

        #region Request Dispatch

        private async Task HandleContextAsync(HttpListenerContext ctx, CancellationToken ct)
        {
            var req  = ctx.Request;
            var resp = ctx.Response;

            // ── Universal CORS + ngrok headers ──────────────────────────
            // "ngrok-skip-browser-warning" lets programmatic clients bypass
            // the ngrok free-tier browser-interstitial page.
            resp.Headers.Set("Access-Control-Allow-Origin",
                req.Headers["Origin"] ?? "*");
            resp.Headers.Set("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            resp.Headers.Set("Access-Control-Allow-Headers",
                "Content-Type, Accept, Authorization, " +
                "ngrok-skip-browser-warning, mcp-session-id");
            resp.Headers.Set("Access-Control-Expose-Headers", "mcp-session-id");
            resp.Headers.Set("Access-Control-Max-Age", "86400");

            if (req.HttpMethod == "OPTIONS")
            {
                resp.StatusCode = 204;
                resp.Close();
                return;
            }

            // Strip trailing slash for comparison
            var path = req.Url?.AbsolutePath?.TrimEnd('/') ?? string.Empty;

            Trace.WriteLine($"[RevitMCP][REQ ] {req.HttpMethod} {path}  from {req.RemoteEndPoint}");

            try
            {
                switch ((req.HttpMethod, path))
                {
                    case ("GET",  "/sse"):
                        await HandleSseEndpointAsync(ctx, ct);
                        break;

                    case ("POST", "/messages"):
                        await HandleMessagesEndpointAsync(ctx, ct);
                        break;

                    case ("GET", "/health") or ("GET", ""):
                        // Simple health / discovery endpoint
                        await WriteJsonResponseAsync(resp, 200, new
                        {
                            status     = "ok",
                            server     = "RevitMCP",
                            version    = "1.0.0",
                            transport  = "sse",
                            sseUrl     = "/sse",
                            messagesUrl= "/messages"
                        }, ct);
                        break;

                    default:
                        resp.StatusCode = 404;
                        resp.Close();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"HandleContext path={path}", ex);
                try { resp.StatusCode = 500; resp.Close(); } catch { }
            }
        }

        /// <summary>Write a JSON object as an HTTP response and close it.</summary>
        private static async Task WriteJsonResponseAsync(
            HttpListenerResponse resp, int status, object body, CancellationToken ct)
        {
            var json  = JsonConvert.SerializeObject(body);
            var bytes = Encoding.UTF8.GetBytes(json);
            resp.StatusCode    = status;
            resp.ContentType   = "application/json; charset=utf-8";
            resp.ContentLength64 = bytes.Length;
            await resp.OutputStream.WriteAsync(bytes, ct);
            resp.Close();
        }

        #endregion

        #region SSE endpoint  GET /sse

        private async Task HandleSseEndpointAsync(HttpListenerContext ctx, CancellationToken ct)
        {
            var req       = ctx.Request;
            var resp      = ctx.Response;
            var sessionId = Guid.NewGuid().ToString("N");

            // ── SSE response headers ──────────────────────────────────────
            // These are mandatory for SSE to work correctly through proxies,
            // ngrok, and Claude web.
            resp.ContentType = "text/event-stream; charset=utf-8";
            resp.Headers.Set("Cache-Control",     "no-cache, no-store, must-revalidate");
            resp.Headers.Set("Pragma",             "no-cache");
            resp.Headers.Set("Connection",         "keep-alive");
            // Disable buffering in nginx/Caddy/ngrok reverse proxies
            resp.Headers.Set("X-Accel-Buffering", "no");
            // Expose the session id so clients can correlate
            resp.Headers.Set("mcp-session-id",    sessionId);
            resp.StatusCode = 200;
            // Do NOT set ContentLength — the stream is unbounded

            // StreamWriter wrapping the raw output stream
            var writer = new StreamWriter(resp.OutputStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 1, leaveOpen: true)
            {
                AutoFlush = false,
                NewLine   = "\n"   // SSE lines must end with \n
            };

            var session = new SseSession(sessionId, writer);
            _sessions[sessionId] = session;

            Trace.WriteLine($"[RevitMCP][SSE ] session opened: {sessionId}  client={req.RemoteEndPoint}");
            Logger.Info($"SSE session opened: {sessionId}  client={req.RemoteEndPoint}");

            try
            {
                // ── Step 1: advertise the messages endpoint ───────────────
                // Build the POST URL that the client should use.
                // When behind ngrok the X-Forwarded-* headers carry the public URL.
                var messagesUrl = BuildMessagesUrl(req, sessionId);
                Trace.WriteLine($"[RevitMCP][SSE ] sending endpoint: {messagesUrl}");
                await session.WriteRawAsync($"event: endpoint\ndata: {messagesUrl}\n\n", ct);

                // ── Step 2: heartbeat loop ────────────────────────────────
                // SSE comment lines (": ...") are ignored by clients but keep
                // the TCP connection alive through proxies with aggressive timeouts.
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, session.SessionCt);
                while (!linked.Token.IsCancellationRequested)
                {
                    await Task.Delay(HeartbeatIntervalMs, linked.Token);
                    await session.WriteRawAsync(": keep-alive\n\n", linked.Token);
                    Trace.WriteLine($"[RevitMCP][SSE ] ♥ keep-alive → {sessionId.Substring(0, 8)}…");
                }
            }
            catch (OperationCanceledException) { /* normal close */ }
            catch (Exception ex)
            {
                Logger.Error($"SSE session {sessionId} error", ex);
            }
            finally
            {
                _sessions.TryRemove(sessionId, out _);
                session.Dispose();
                Trace.WriteLine($"[RevitMCP][SSE ] session closed: {sessionId}");
                Logger.Info($"SSE session closed: {sessionId}");
            }
        }

        /// <summary>
        /// Build the absolute URL that the client should POST messages to.
        /// Respects X-Forwarded-Proto / X-Forwarded-Host set by ngrok/proxies
        /// so the returned URL is valid from the client's perspective.
        /// </summary>
        private static string BuildMessagesUrl(HttpListenerRequest req, string sessionId)
        {
            // ngrok (and other reverse-proxies) set these standard headers
            var proto = req.Headers["X-Forwarded-Proto"]
                     ?? (req.IsSecureConnection ? "https" : "http");
            var host  = req.Headers["X-Forwarded-Host"]
                     ?? req.Headers["Host"]
                     ?? req.UserHostName;

            // Prefer an absolute URL so the client never has to guess
            return $"{proto}://{host}/messages?sessionId={sessionId}";
        }

        #endregion

        #region Messages endpoint  POST /messages

        private async Task HandleMessagesEndpointAsync(HttpListenerContext ctx, CancellationToken ct)
        {
            var req  = ctx.Request;
            var resp = ctx.Response;

            var sessionId = req.QueryString["sessionId"] ?? string.Empty;

            // ── Read body ────────────────────────────────────────────────
            string body;
            using (var sr = new StreamReader(req.InputStream,
                       req.ContentEncoding ?? Encoding.UTF8,
                       detectEncodingFromByteOrderMarks: true,
                       bufferSize: 4096,
                       leaveOpen: true))
                body = await sr.ReadToEndAsync(ct);

            var sidShort = sessionId.Length >= 8 ? sessionId.Substring(0, 8) : sessionId;
            Trace.WriteLine($"[RevitMCP][RECV] [{sidShort}…] {body}");
            Logger.Info($"← [{sidShort}…] {body}");

            // ── MCP spec: acknowledge immediately with 202 ───────────────
            // The actual response travels back over the SSE stream, not here.
            resp.StatusCode = 202;
            resp.ContentLength64 = 0;
            resp.Close();

            // ── Parse JSON-RPC ───────────────────────────────────────────
            McpRequest? mcpRequest;
            try
            {
                mcpRequest = JsonConvert.DeserializeObject<McpRequest>(body);
            }
            catch (JsonException ex)
            {
                Logger.Error("JSON parse error", ex);
                await TrySendSseAsync(sessionId, McpResponse.ParseError(), ct);
                return;
            }

            if (mcpRequest == null)
            {
                Logger.Warning("Received null/empty JSON-RPC request.");
                return;
            }

            // ── Route ────────────────────────────────────────────────────
            McpResponse? mcpResponse;
            try
            {
                mcpResponse = await _router.RouteAsync(mcpRequest, ct);
            }
            catch (Exception ex)
            {
                Logger.Error($"Router error for method '{mcpRequest.Method}'", ex);
                mcpResponse = McpResponse.InternalError(mcpRequest.Id, ex.Message);
            }

            // Notifications have no response
            if (mcpResponse == null) return;

            var responseJson = JsonConvert.SerializeObject(mcpResponse, Formatting.None);
            Trace.WriteLine($"[RevitMCP][SEND] [{sidShort}…] {responseJson}");
            Logger.Info($"→ [{sidShort}…] {responseJson}");
            RequestLogged?.Invoke($"[{mcpRequest.Method}] {responseJson}");

            await TrySendSseAsync(sessionId, mcpResponse, ct);
        }

        private async Task TrySendSseAsync(string sessionId, McpResponse response, CancellationToken ct)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                Logger.Warning($"No active SSE session for id='{sessionId}'. " +
                               "Did the client close the stream before the response arrived?");
                return;
            }

            var json = JsonConvert.SerializeObject(response, Formatting.None);
            await session.SendMessageAsync(json, ct);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
            _listener?.Close();
        }

        #endregion
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  SSE Session — one per connected MCP client
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Holds the SSE write stream for one connected MCP client.
    /// Thread-safe write lock ensures no interleaved SSE frames.
    /// </summary>
    internal sealed class SseSession : IDisposable
    {
        #region Fields

        public string SessionId { get; }

        private readonly StreamWriter _writer;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly CancellationTokenSource _cts = new();

        public CancellationToken SessionCt => _cts.Token;

        #endregion

        public SseSession(string sessionId, StreamWriter writer)
        {
            SessionId = sessionId;
            _writer   = writer;
        }

        /// <summary>
        /// Send a JSON-RPC response as an SSE <c>message</c> event.
        ///
        /// Wire format (MCP SSE spec 2024-11-05):
        /// <code>
        /// event: message\n
        /// data: {json}\n
        /// \n
        /// </code>
        /// The double newline terminates the SSE frame.
        /// </summary>
        public async Task SendMessageAsync(string json, CancellationToken ct)
        {
            // Named "message" event as required by the MCP SSE spec
            await WriteRawAsync($"event: message\ndata: {json}\n\n", ct);
        }

        /// <summary>
        /// Write a raw SSE frame (heartbeat comment, endpoint event, etc.).
        /// Acquires the write lock so concurrent senders never interleave bytes.
        /// </summary>
        public async Task WriteRawAsync(string raw, CancellationToken ct)
        {
            await _writeLock.WaitAsync(ct);
            try
            {
#if NET48
                ct.ThrowIfCancellationRequested();
                await _writer.WriteAsync(raw);
#else
                await _writer.WriteAsync(raw.AsMemory(), ct);
#endif
                // Flush to the underlying network buffer immediately.
                // Without this the bytes sit in the StreamWriter buffer and
                // the client never receives them (proxy-buffering problem).
                await _writer.FlushAsync();
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _writeLock.Dispose();
            try { _writer.Dispose(); } catch { }
        }
    }
}
