#if NET48
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RevitMcpAddin.Compat
{
    internal static class Net48Extensions
    {
        public static void Deconstruct<TKey, TValue>(
            this KeyValuePair<TKey, TValue> pair,
            out TKey key,
            out TValue value)
        {
            key = pair.Key;
            value = pair.Value;
        }

        public static bool Contains(this string source, string value, StringComparison comparisonType)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return source.IndexOf(value, comparisonType) >= 0;
        }

        public static Task WriteAsync(this Stream stream, byte[] buffer, CancellationToken ct)
        {
            return stream.WriteAsync(buffer, 0, buffer.Length, ct);
        }

        public static Task<string> ReadToEndAsync(this TextReader reader, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return reader.ReadToEndAsync();
        }

        public static async Task<T> WaitAsync<T>(this Task<T> task, CancellationToken ct)
        {
            if (task.IsCompleted)
                return await task.ConfigureAwait(false);

            var cancellation = new TaskCompletionSource<bool>();
            using (ct.Register(state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancellation))
            {
                if (task != await Task.WhenAny(task, cancellation.Task).ConfigureAwait(false))
                    throw new OperationCanceledException(ct);
            }

            return await task.ConfigureAwait(false);
        }
    }
}
#endif
