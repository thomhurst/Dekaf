#if NETSTANDARD2_0
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace System
{
    internal struct HashCode
    {
        private int _hash;

        public static int Combine<T1, T2>(T1 value1, T2 value2)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + EqualityComparer<T1>.Default.GetHashCode(value1!);
                hash = (hash * 31) + EqualityComparer<T2>.Default.GetHashCode(value2!);
                return hash;
            }
        }

        public void Add<T>(T value)
        {
            unchecked
            {
                _hash = ((_hash == 0 ? 17 : _hash) * 31) + EqualityComparer<T>.Default.GetHashCode(value!);
            }
        }

        public readonly int ToHashCode() => _hash;
    }

    internal static class StringCompatibilityExtensions
    {
        public static bool Contains(this string value, string comparison, StringComparison comparisonType)
            => value.IndexOf(comparison, comparisonType) >= 0;

        public static bool Contains(this string value, char comparison, StringComparison comparisonType)
            => value.IndexOf(comparison.ToString(), comparisonType) >= 0;
    }
}

namespace System.Numerics
{
    internal static class BitOperations
    {
        public static int Log2(uint value)
        {
            var result = 0;
            while ((value >>= 1) != 0)
            {
                result++;
            }

            return result;
        }

        public static int Log2(ulong value)
        {
            var result = 0;
            while ((value >>= 1) != 0)
            {
                result++;
            }

            return result;
        }

        public static uint RoundUpToPowerOf2(uint value)
        {
            if (value <= 1)
                return 1;

            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }
    }
}

namespace System.Buffers
{
    internal ref struct SequenceReader<T>
    {
        private readonly ReadOnlyMemory<T> _memory;
        private int _position;

        public SequenceReader(ReadOnlySequence<T> sequence)
        {
            _memory = sequence.IsSingleSegment ? sequence.First : sequence.ToArray();
            _position = 0;
        }

        public long Consumed => _position;

        public long Remaining => _memory.Length - _position;

        public bool End => _position >= _memory.Length;

        public ReadOnlySpan<T> UnreadSpan => _memory.Span.Slice(_position);

        public ReadOnlySequence<T> UnreadSequence => new(_memory.Slice(_position));

        public bool TryRead(out T value)
        {
            if (_position >= _memory.Length)
            {
                value = default!;
                return false;
            }

            value = _memory.Span[_position++];
            return true;
        }

        public void Advance(long count)
        {
            if (count < 0 || count > Remaining)
                throw new ArgumentOutOfRangeException(nameof(count));

            _position += checked((int)count);
        }

        public bool TryCopyTo(scoped Span<T> destination)
        {
            if (destination.Length > Remaining)
                return false;

            UnreadSpan.Slice(0, destination.Length).CopyTo(destination);
            return true;
        }
    }

    internal sealed class ArrayBufferWriter<T> : IBufferWriter<T>
    {
        private const int DefaultInitialCapacity = 256;
        private T[] _buffer;

        public ArrayBufferWriter()
            : this(DefaultInitialCapacity)
        {
        }

        public ArrayBufferWriter(int initialCapacity)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            _buffer = initialCapacity == 0 ? Array.Empty<T>() : new T[initialCapacity];
        }

        public int WrittenCount { get; private set; }

        public ReadOnlyMemory<T> WrittenMemory => _buffer.AsMemory(0, WrittenCount);

        public ReadOnlySpan<T> WrittenSpan => _buffer.AsSpan(0, WrittenCount);

        public void Advance(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (WrittenCount > _buffer.Length - count)
                throw new InvalidOperationException("Cannot advance past the end of the buffer.");

            WrittenCount += count;
        }

        public Memory<T> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsMemory(WrittenCount);
        }

        public Span<T> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsSpan(WrittenCount);
        }

        private void EnsureCapacity(int sizeHint)
        {
            if (sizeHint < 0)
                throw new ArgumentOutOfRangeException(nameof(sizeHint));

            if (sizeHint == 0)
                sizeHint = 1;

            if (sizeHint <= _buffer.Length - WrittenCount)
                return;

            var growBy = Math.Max(sizeHint, _buffer.Length);
            var newSize = checked(_buffer.Length + growBy);
            if (newSize == 0)
                newSize = DefaultInitialCapacity;

            Array.Resize(ref _buffer, newSize);
        }
    }
}

namespace System.Collections.Generic
{
    public interface IReadOnlySet<T> : IReadOnlyCollection<T>
    {
        bool Contains(T item);
        bool IsProperSubsetOf(IEnumerable<T> other);
        bool IsProperSupersetOf(IEnumerable<T> other);
        bool IsSubsetOf(IEnumerable<T> other);
        bool IsSupersetOf(IEnumerable<T> other);
        bool Overlaps(IEnumerable<T> other);
        bool SetEquals(IEnumerable<T> other);
    }

    internal static class CollectionCompatibilityExtensions
    {
        public static void Deconstruct<TKey, TValue>(
            this KeyValuePair<TKey, TValue> pair,
            out TKey key,
            out TValue value)
        {
            key = pair.Key;
            value = pair.Value;
        }

        public static bool TryDequeue<T>(this Queue<T> queue, out T value)
        {
            if (queue.Count == 0)
            {
                value = default!;
                return false;
            }

            value = queue.Dequeue();
            return true;
        }

        public static int EnsureCapacity<T>(this List<T> list, int capacity)
        {
            if (list.Capacity < capacity)
                list.Capacity = capacity;

            return list.Capacity;
        }

        public static TValue? GetValueOrDefault<TKey, TValue>(
            this IReadOnlyDictionary<TKey, TValue> dictionary,
            TKey key)
            => dictionary.TryGetValue(key, out var value) ? value : default;

        public static TValue GetValueOrDefault<TKey, TValue>(
            this IReadOnlyDictionary<TKey, TValue> dictionary,
            TKey key,
            TValue defaultValue)
            => dictionary.TryGetValue(key, out var value) ? value : defaultValue;

        public static bool TryAdd<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary,
            TKey key,
            TValue value)
        {
            if (dictionary.ContainsKey(key))
                return false;

            dictionary.Add(key, value);
            return true;
        }

        public static bool Remove<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary,
            TKey key,
            out TValue value)
        {
            if (!dictionary.TryGetValue(key, out value!))
                return false;

            return dictionary.Remove(key);
        }
    }
}

namespace System.Collections.Concurrent
{
    internal static class ConcurrentCollectionCompatibilityExtensions
    {
        public static TValue? GetValueOrDefault<TKey, TValue>(
            this ConcurrentDictionary<TKey, TValue> dictionary,
            TKey key)
            where TKey : notnull
            => dictionary.TryGetValue(key, out var value) ? value : default;

        public static TValue GetValueOrDefault<TKey, TValue>(
            this ConcurrentDictionary<TKey, TValue> dictionary,
            TKey key,
            TValue defaultValue)
            where TKey : notnull
            => dictionary.TryGetValue(key, out var value) ? value : defaultValue;

        public static TValue GetOrAdd<TKey, TValue, TArg>(
            this ConcurrentDictionary<TKey, TValue> dictionary,
            TKey key,
            Func<TKey, TArg, TValue> valueFactory,
            TArg factoryArgument)
            where TKey : notnull
            => dictionary.GetOrAdd(key, item => valueFactory(item, factoryArgument));

        public static TValue AddOrUpdate<TKey, TValue, TArg>(
            this ConcurrentDictionary<TKey, TValue> dictionary,
            TKey key,
            Func<TKey, TArg, TValue> addValueFactory,
            Func<TKey, TValue, TArg, TValue> updateValueFactory,
            TArg factoryArgument)
            where TKey : notnull
        {
            return dictionary.AddOrUpdate(
                key,
                item => addValueFactory(item, factoryArgument),
                (item, existing) => updateValueFactory(item, existing, factoryArgument));
        }

        public static void Clear<T>(this ConcurrentQueue<T> queue)
        {
            while (queue.TryDequeue(out _))
            {
            }
        }

        public static bool TryRemove<TKey, TValue>(
            this ConcurrentDictionary<TKey, TValue> dictionary,
            KeyValuePair<TKey, TValue> item)
            where TKey : notnull
        {
            if (!dictionary.TryGetValue(item.Key, out var value) ||
                !EqualityComparer<TValue>.Default.Equals(value, item.Value))
            {
                return false;
            }

            return dictionary.TryRemove(item.Key, out _);
        }
    }
}

namespace System.Linq
{
    internal static class EnumerableCompatibilityExtensions
    {
        public static bool TryGetNonEnumeratedCount<TSource>(
            this IEnumerable<TSource> source,
            out int count)
        {
            if (source is ICollection<TSource> collection)
            {
                count = collection.Count;
                return true;
            }

            if (source is IReadOnlyCollection<TSource> readOnlyCollection)
            {
                count = readOnlyCollection.Count;
                return true;
            }

            count = 0;
            return false;
        }

        public static IOrderedEnumerable<TSource> Order<TSource>(this IEnumerable<TSource> source)
            => source.OrderBy(static item => item);

        public static HashSet<TSource> ToHashSet<TSource>(this IEnumerable<TSource> source)
            => new(source);
    }
}

namespace System.Threading
{
    internal sealed class Lock
    {
    }

    internal sealed class PeriodicTimer : IDisposable
    {
        private readonly TimeSpan _period;
        private volatile bool _disposed;

        public PeriodicTimer(TimeSpan period)
        {
            if (period <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(period));

            _period = period;
        }

        public async ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return false;

            try
            {
                await Task.Delay(_period, cancellationToken).ConfigureAwait(false);
                return !_disposed;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }

    internal static class ThreadingCompatibilityExtensions
    {
        public static Task CancelAsync(this CancellationTokenSource cancellationTokenSource)
        {
            cancellationTokenSource.Cancel();
            return Task.CompletedTask;
        }

        public static CancellationTokenRegistration UnsafeRegister(
            this CancellationToken cancellationToken,
            Action<object?> callback,
            object? state)
            => cancellationToken.Register(callback, state);

        public static CancellationTokenRegistration UnsafeRegister(
            this CancellationToken cancellationToken,
            Action<object?, CancellationToken> callback,
            object? state)
            => cancellationToken.Register(static captured =>
            {
                var (innerCallback, innerState, innerToken) =
                    ((Action<object?, CancellationToken>, object?, CancellationToken))captured!;
                innerCallback(innerState, innerToken);
            }, (callback, state, cancellationToken));

        public static bool TryReset(this CancellationTokenSource cancellationTokenSource)
            => false;
    }
}

namespace System.Threading.Tasks
{
    internal sealed class TaskCompletionSource
    {
        private readonly TaskCompletionSource<object?> _inner;

        public TaskCompletionSource()
            : this(TaskCreationOptions.None)
        {
        }

        public TaskCompletionSource(TaskCreationOptions creationOptions)
        {
            _inner = new TaskCompletionSource<object?>(creationOptions);
        }

        public Task Task => _inner.Task;

        public bool TrySetResult() => _inner.TrySetResult(null);

        public bool TrySetException(Exception exception) => _inner.TrySetException(exception);
    }

    internal static class TaskWaitAsyncCompatibilityExtensions
    {
        public static Task WaitAsync(this Task task, CancellationToken cancellationToken)
            => WaitAsync(task, Timeout.InfiniteTimeSpan, cancellationToken);

        public static Task WaitAsync(this Task task, TimeSpan timeout)
            => WaitAsync(task, timeout, CancellationToken.None);

        public static async Task WaitAsync(this Task task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (task.IsCompleted)
            {
                await task.ConfigureAwait(false);
                return;
            }

            using var cts = CreateDelayTokenSource(timeout, cancellationToken);
            var delayTask = Task.Delay(timeout, cts.Token);
            var completed = await Task.WhenAny(task, delayTask).ConfigureAwait(false);
            if (completed == task)
            {
                cts.Cancel();
                await task.ConfigureAwait(false);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            throw new TimeoutException();
        }

        public static Task<T> WaitAsync<T>(this Task<T> task, CancellationToken cancellationToken)
            => WaitAsync(task, Timeout.InfiniteTimeSpan, cancellationToken);

        public static Task<T> WaitAsync<T>(this Task<T> task, TimeSpan timeout)
            => WaitAsync(task, timeout, CancellationToken.None);

        public static async Task<T> WaitAsync<T>(this Task<T> task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (task.IsCompleted)
                return await task.ConfigureAwait(false);

            using var cts = CreateDelayTokenSource(timeout, cancellationToken);
            var delayTask = Task.Delay(timeout, cts.Token);
            var completed = await Task.WhenAny(task, delayTask).ConfigureAwait(false);
            if (completed == task)
            {
                cts.Cancel();
                return await task.ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            throw new TimeoutException();
        }

        private static CancellationTokenSource CreateDelayTokenSource(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (timeout == Timeout.InfiniteTimeSpan)
                return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (timeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }
    }
}

namespace System.IO
{
    internal static class StreamCompatibilityExtensions
    {
        public static ValueTask DisposeAsync(this Stream stream)
        {
            stream.Dispose();
            return default;
        }

        public static Task WriteAsync(
            this Stream stream,
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
                return stream.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken);

            return WriteAsyncSlow(stream, buffer, cancellationToken);
        }

        public static Task<int> ReadAsync(
            this Stream stream,
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
                return stream.ReadAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken);

            return ReadAsyncSlow(stream, buffer, cancellationToken);
        }

        private static async Task WriteAsyncSlow(
            Stream stream,
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken)
        {
            var rented = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                buffer.CopyTo(rented);
                await stream.WriteAsync(rented, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        private static async Task<int> ReadAsyncSlow(
            Stream stream,
            Memory<byte> buffer,
            CancellationToken cancellationToken)
        {
            var rented = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                var read = await stream.ReadAsync(rented, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                rented.AsMemory(0, read).CopyTo(buffer);
                return read;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}

namespace System.Net.Sockets
{
    internal static class SocketCompatibilityExtensions
    {
        public static Task<int> ReceiveAsync(
            this Socket socket,
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
            => socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);

        public static Task<int> ReceiveAsync(
            this Socket socket,
            Memory<byte> buffer,
            SocketFlags socketFlags,
            CancellationToken cancellationToken = default)
        {
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
                return socket.ReceiveAsync(segment, socketFlags).WaitAsync(cancellationToken);

            return ReceiveAsyncSlow(socket, buffer, socketFlags, cancellationToken);
        }

        public static Task<int> ReceiveAsync(
            this Socket socket,
            Memory<byte> buffer,
            SocketFlags socketFlags)
            => ReceiveAsync(socket, buffer, socketFlags, CancellationToken.None);

        public static Task ConnectAsync(
            this Socket socket,
            EndPoint remoteEndPoint,
            CancellationToken cancellationToken = default)
            => socket.ConnectAsync(remoteEndPoint).WaitAsync(cancellationToken);

        private static async Task<int> ReceiveAsyncSlow(
            Socket socket,
            Memory<byte> buffer,
            SocketFlags socketFlags,
            CancellationToken cancellationToken)
        {
            var rented = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                var read = await socket.ReceiveAsync(new ArraySegment<byte>(rented, 0, buffer.Length), socketFlags)
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                rented.AsMemory(0, read).CopyTo(buffer);
                return read;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}

namespace System.Net.Http
{
    internal static class HttpContentCompatibilityExtensions
    {
        public static Task<string> ReadAsStringAsync(
            this HttpContent content,
            CancellationToken cancellationToken)
            => content.ReadAsStringAsync().WaitAsync(cancellationToken);
    }
}

namespace System.Text
{
    internal static class EncodingCompatibilityExtensions
    {
        public static int GetByteCount(this Encoding encoding, ReadOnlySpan<char> value)
            => encoding.GetByteCount(value.ToArray());

        public static int GetBytes(this Encoding encoding, ReadOnlySpan<char> value, Span<byte> destination)
        {
            var bytes = encoding.GetBytes(value.ToArray());
            bytes.CopyTo(destination);
            return bytes.Length;
        }

        public static int GetBytes(this Encoding encoding, string value, Span<byte> destination)
        {
            var bytes = encoding.GetBytes(value);
            bytes.CopyTo(destination);
            return bytes.Length;
        }

        public static string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes)
            => encoding.GetString(bytes.ToArray());
    }
}

namespace System.Net.Security
{
    internal sealed class SslClientAuthenticationOptions
    {
        public string? TargetHost { get; set; }

        public X509CertificateCollection? ClientCertificates { get; set; }

        public RemoteCertificateValidationCallback? RemoteCertificateValidationCallback { get; set; }

        public SslProtocols EnabledSslProtocols { get; set; } = SslProtocols.None;

        public X509RevocationMode CertificateRevocationCheckMode { get; set; } = X509RevocationMode.NoCheck;
    }

    internal static class SslStreamCompatibilityExtensions
    {
        public static Task AuthenticateAsClientAsync(
            this SslStream sslStream,
            SslClientAuthenticationOptions sslOptions,
            CancellationToken cancellationToken)
        {
            var authenticateTask = sslStream.AuthenticateAsClientAsync(
                sslOptions.TargetHost ?? string.Empty,
                clientCertificates: sslOptions.ClientCertificates,
                enabledSslProtocols: sslOptions.EnabledSslProtocols,
                checkCertificateRevocation: sslOptions.CertificateRevocationCheckMode != X509RevocationMode.NoCheck);

            return authenticateTask.WaitAsync(cancellationToken);
        }
    }

    internal sealed class NegotiateAuthenticationClientOptions
    {
        public string? Package { get; set; }

        public string? TargetName { get; set; }

        public ProtectionLevel RequiredProtectionLevel { get; set; }

        public System.Security.Principal.TokenImpersonationLevel AllowedImpersonationLevel { get; set; }

        public System.Net.NetworkCredential? Credential { get; set; }
    }

    internal enum NegotiateAuthenticationStatusCode
    {
        Completed,
        ContinueNeeded
    }

    internal sealed class NegotiateAuthentication : IDisposable
    {
        public NegotiateAuthentication(NegotiateAuthenticationClientOptions options)
        {
            throw new PlatformNotSupportedException("GSSAPI is not available on the netstandard2.0 compatibility target.");
        }

        public byte[]? GetOutgoingBlob(ReadOnlySpan<byte> incomingBlob, out NegotiateAuthenticationStatusCode statusCode)
        {
            GC.KeepAlive(this);
            statusCode = default;
            throw new PlatformNotSupportedException("GSSAPI is not available on the netstandard2.0 compatibility target.");
        }

        public void Dispose()
        {
        }
    }
}

#endif
