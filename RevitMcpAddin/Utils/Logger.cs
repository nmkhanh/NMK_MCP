using System.Diagnostics;

namespace RevitMcpAddin.Utils
{
    /// <summary>
    /// Centralised, thread-safe logging utility.
    /// All output goes to <see cref="Trace"/> so it appears in:
    ///   – Visual Studio Output window (when debugAttached)
    ///   – Any TraceListener added to Trace.Listeners (e.g. file listener)
    /// </summary>
    public static class Logger
    {
        #region Fields

        private const string Prefix = "[RevitMCP]";

        // Optional file listener – call Logger.EnableFileLogging(path) to activate
        private static TextWriterTraceListener? _fileListener;
        private static readonly object _lock = new();

        #endregion

        #region Public API

        public static void Info(string message) =>
            Write("INFO ", message, null);

        public static void Warning(string message) =>
            Write("WARN ", message, null);

        public static void Error(string message, Exception? ex = null) =>
            Write("ERROR", message, ex);

        public static void Debug(string message)
        {
#if DEBUG
            Write("DEBUG", message, null);
#endif
        }

        /// <summary>
        /// Attach a rolling file trace listener.
        /// Call once from <c>App.OnStartup</c> if file logging is desired.
        /// </summary>
        public static void EnableFileLogging(string filePath)
        {
            lock (_lock)
            {
                _fileListener?.Dispose();
                _fileListener = new TextWriterTraceListener(filePath, "RevitMcpFileLogger");
                Trace.Listeners.Add(_fileListener);
                Trace.AutoFlush = true;
                Info($"File logging enabled → {filePath}");
            }
        }

        #endregion

        #region Helpers

        private static void Write(string level, string message, Exception? ex)
        {
            var line = $"{Prefix}[{level}] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {message}";
            Trace.WriteLine(line);

            if (ex != null)
                Trace.WriteLine($"{Prefix}[EXCEP] {ex}");
        }

        #endregion
    }
}
