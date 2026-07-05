#if NETSTANDARD2_0
namespace System.Buffers
{
internal sealed class ArrayBufferWriter<T> : IBufferWriter<T>
{
    private const int DefaultInitialBufferSize = 256;
    private const int ArrayMaxLength = 0x7FFFFFC7;

    private T[] _buffer;
    private int _index;

    public ArrayBufferWriter()
        : this(DefaultInitialBufferSize)
    {
    }

    public ArrayBufferWriter(int initialCapacity)
    {
        if (initialCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));

        _buffer = initialCapacity == 0 ? Array.Empty<T>() : new T[initialCapacity];
    }

    public ReadOnlyMemory<T> WrittenMemory => _buffer.AsMemory(0, _index);
    public ReadOnlySpan<T> WrittenSpan => _buffer.AsSpan(0, _index);
    public int WrittenCount => _index;
    public int Capacity => _buffer.Length;
    public int FreeCapacity => _buffer.Length - _index;

    public void Advance(int count)
    {
        if ((uint)count > (uint)FreeCapacity)
            throw new ArgumentException("Cannot advance past the end of the buffer.", nameof(count));

        _index += count;
    }

    public Memory<T> GetMemory(int sizeHint = 0)
    {
        CheckAndResizeBuffer(sizeHint);
        return _buffer.AsMemory(_index);
    }

    public Span<T> GetSpan(int sizeHint = 0)
    {
        CheckAndResizeBuffer(sizeHint);
        return _buffer.AsSpan(_index);
    }

    private void CheckAndResizeBuffer(int sizeHint)
    {
        if (sizeHint < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeHint));

        if (sizeHint == 0)
            sizeHint = 1;

        if (sizeHint <= FreeCapacity)
            return;

        var currentLength = _buffer.Length;
        var growBy = Math.Max(sizeHint, currentLength);
        var newSize = currentLength == 0 ? Math.Max(growBy, DefaultInitialBufferSize) : currentLength + growBy;
        if ((uint)newSize > ArrayMaxLength)
        {
            newSize = Math.Max(currentLength + sizeHint, ArrayMaxLength);
        }

        if (newSize < currentLength + sizeHint)
            throw new InvalidOperationException("Cannot grow buffer: maximum size reached.");

        Array.Resize(ref _buffer, newSize);
    }
}

internal ref struct SequenceReader<T>
{
    private readonly ReadOnlySequence<T> _sequence;
    private SequencePosition _position;
    private long _consumed;

    public SequenceReader(ReadOnlySequence<T> sequence)
    {
        _sequence = sequence;
        _position = sequence.Start;
        _consumed = 0;
    }

    public readonly long Consumed => _consumed;
    public readonly long Remaining => _sequence.Length - _consumed;
    public readonly bool End => Remaining == 0;
    public readonly ReadOnlySequence<T> UnreadSequence => _sequence.Slice(_position);
    public readonly ReadOnlySpan<T> UnreadSpan => GetUnreadSpan(_sequence, _position);

    public bool TryRead(out T value)
    {
        if (End)
        {
            value = default!;
            return false;
        }

        if (!TryMoveToNonEmptySpan(out var span))
        {
            value = default!;
            return false;
        }

        value = span[0];
        Advance(1);
        return true;
    }

    public readonly bool TryCopyTo(Span<T> destination)
    {
        if (Remaining < destination.Length)
            return false;

        UnreadSequence.Slice(0, destination.Length).CopyTo(destination);
        return true;
    }

    public void Advance(long count)
    {
        if ((ulong)count > (ulong)Remaining)
            throw new ArgumentOutOfRangeException(nameof(count));

        _position = _sequence.GetPosition(count, _position);
        _consumed += count;
    }

    private bool TryMoveToNonEmptySpan(out ReadOnlySpan<T> span)
    {
        var current = _position;
        var next = current;
        while (_sequence.TryGet(ref next, out var memory))
        {
            if (memory.Length != 0)
            {
                span = memory.Span;
                _position = current;
                return true;
            }

            current = next;
        }

        span = default;
        return false;
    }

    private static ReadOnlySpan<T> GetUnreadSpan(ReadOnlySequence<T> sequence, SequencePosition position)
    {
        while (sequence.TryGet(ref position, out var memory))
        {
            if (memory.Length != 0)
                return memory.Span;
        }

        return default;
    }
}

}

namespace System.Threading
{
internal sealed class PeriodicTimer : IDisposable
{
    private readonly TimeSpan _period;
    private readonly CancellationTokenSource _disposed = new();

    public PeriodicTimer(TimeSpan period)
    {
        _period = period;
    }

    public async ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposed.Token);
        try
        {
            await Task.Delay(_period, linked.Token).ConfigureAwait(false);
            return !_disposed.IsCancellationRequested;
        }
        catch (OperationCanceledException) when (_disposed.IsCancellationRequested)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _disposed.Cancel();
        _disposed.Dispose();
    }
}

internal static class CancellationCompatibilityExtensions
{
    public static bool TryReset(this CancellationTokenSource source)
        => !source.IsCancellationRequested;
}
}

namespace System.Collections.Concurrent
{
internal static class ConcurrentCollectionCompatibilityExtensions
{
    public static bool TryRemove<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        KeyValuePair<TKey, TValue> item)
        where TKey : notnull
        => ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).Remove(item);

    public static TValue AddOrUpdate<TKey, TValue, TArg>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        TKey key,
        Func<TKey, TArg, TValue> addValueFactory,
        Func<TKey, TValue, TArg, TValue> updateValueFactory,
        TArg factoryArgument)
        where TKey : notnull
        => dictionary.AddOrUpdate(
            key,
            k => addValueFactory(k, factoryArgument),
            (k, existing) => updateValueFactory(k, existing, factoryArgument));
}
}

namespace System.Net.Sockets
{
using System.Buffers;
using System.Runtime.InteropServices;

internal static class SocketCompatibilityExtensions
{
    public static async ValueTask<int> ReceiveAsync(this Socket socket, Memory<byte> buffer, SocketFlags socketFlags)
    {
        if (MemoryMarshal.TryGetArray<byte>(buffer, out var segment))
            return await socket.ReceiveAsync(segment, socketFlags).ConfigureAwait(false);

        var rented = ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            var read = await socket.ReceiveAsync(new ArraySegment<byte>(rented, 0, buffer.Length), socketFlags)
                .ConfigureAwait(false);
            rented.AsSpan(0, read).CopyTo(buffer.Span);
            return read;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
}

namespace Polyfills
{
static partial class DekafValueTaskPolyfill
{
    extension(ValueTask)
    {
        public static ValueTask FromCanceled(CancellationToken cancellationToken)
            => new(Task.FromCanceled(cancellationToken));

        public static ValueTask FromException(Exception exception)
            => new(Task.FromException(exception));
    }
}
}

namespace Dekaf.NetStandard
{
using System.Net;
using System.Security.Cryptography;

internal static class ArrayCompat
{
    public const int MaxLength = 0x7FFFFFC7;

    public static T[] Empty<T>() => System.Array.Empty<T>();

    public static void Resize<T>(ref T[] array, int newSize) => System.Array.Resize(ref array, newSize);

    public static void Copy(System.Array sourceArray, System.Array destinationArray, int length)
        => System.Array.Copy(sourceArray, destinationArray, length);

    public static void Copy(System.Array sourceArray, int sourceIndex, System.Array destinationArray, int destinationIndex, int length)
        => System.Array.Copy(sourceArray, sourceIndex, destinationArray, destinationIndex, length);

    public static int FindIndex<T>(T[] array, Predicate<T> match) => System.Array.FindIndex(array, match);

    public static void Clear(System.Array array) => System.Array.Clear(array, 0, array.Length);

    public static void Clear(System.Array array, int index, int length) => System.Array.Clear(array, index, length);

    public static void Fill<T>(T[] array, T value)
    {
        for (var i = 0; i < array.Length; i++)
            array[i] = value;
    }
}

internal static class AsciiCompat
{
    public static bool IsValid(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] > 0x7F)
                return false;
        }

        return true;
    }
}

internal static class BitOperationsCompat
{
    public static int Log2(uint value)
    {
        var result = 0;
        while ((value >>= 1) != 0)
            result++;
        return result;
    }

    public static int Log2(ulong value)
    {
        var result = 0;
        while ((value >>= 1) != 0)
            result++;
        return result;
    }

    public static int RoundUpToPowerOf2(int value)
    {
        if (value <= 1)
            return 1;

        var current = (uint)value - 1;
        current |= current >> 1;
        current |= current >> 2;
        current |= current >> 4;
        current |= current >> 8;
        current |= current >> 16;
        return (int)(current + 1);
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

internal static class DnsCompat
{
    public static Task<IPAddress[]> GetHostAddressesAsync(string hostNameOrAddress, CancellationToken cancellationToken)
        => System.Net.Dns.GetHostAddressesAsync(hostNameOrAddress).WaitAsync(cancellationToken);

    public static Task<IPHostEntry> GetHostEntryAsync(string hostNameOrAddress, CancellationToken cancellationToken)
        => System.Net.Dns.GetHostEntryAsync(hostNameOrAddress).WaitAsync(cancellationToken);
}

internal static class GCCompat
{
    public static T[] AllocateUninitializedArray<T>(int length, bool pinned = false) => new T[length];

    public static GCMemoryInfoCompat GetGCMemoryInfo()
        => new(0);
}

internal readonly struct GCMemoryInfoCompat
{
    public GCMemoryInfoCompat(long totalAvailableMemoryBytes)
    {
        TotalAvailableMemoryBytes = totalAvailableMemoryBytes;
    }

    public long TotalAvailableMemoryBytes { get; }
}

internal struct HashCodeCompat
{
    private int _hash;

    public void Add<T>(T value)
    {
        _hash = Combine(_hash, value);
    }

    public readonly int ToHashCode() => _hash;

    public static int Combine<T1, T2>(T1 value1, T2 value2)
        => Combine(value1?.GetHashCode() ?? 0, value2);

    private static int Combine<T>(int hash, T value)
    {
        unchecked
        {
            return (hash * 397) ^ (value?.GetHashCode() ?? 0);
        }
    }
}

internal static class HMACSHA256Compat
{
    public static byte[] HashData(byte[] key, byte[] source)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(key);
        return hmac.ComputeHash(source);
    }
}

internal static class HMACSHA512Compat
{
    public static byte[] HashData(byte[] key, byte[] source)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA512(key);
        return hmac.ComputeHash(source);
    }
}

internal static class Rfc2898DeriveBytesCompat
{
    public static byte[] Pbkdf2(
        byte[] password,
        byte[] salt,
        int iterations,
        HashAlgorithmName hashAlgorithm,
        int outputLength)
    {
        if (iterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(iterations));

        using var hmac = CreateHmac(hashAlgorithm, password);
        var result = new byte[outputLength];
        var block = new byte[hmac.HashSize / 8];
        var saltAndBlock = new byte[salt.Length + 4];
        salt.CopyTo(saltAndBlock, 0);

        var offset = 0;
        for (var blockIndex = 1; offset < outputLength; blockIndex++)
        {
            saltAndBlock[salt.Length] = (byte)(blockIndex >> 24);
            saltAndBlock[salt.Length + 1] = (byte)(blockIndex >> 16);
            saltAndBlock[salt.Length + 2] = (byte)(blockIndex >> 8);
            saltAndBlock[salt.Length + 3] = (byte)blockIndex;

            var u = hmac.ComputeHash(saltAndBlock);
            u.CopyTo(block, 0);
            for (var i = 1; i < iterations; i++)
            {
                u = hmac.ComputeHash(u);
                for (var j = 0; j < block.Length; j++)
                    block[j] ^= u[j];
            }

            var count = Math.Min(block.Length, outputLength - offset);
            System.Array.Copy(block, 0, result, offset, count);
            offset += count;
        }

        return result;
    }

    private static HMAC CreateHmac(HashAlgorithmName hashAlgorithm, byte[] key)
    {
        if (hashAlgorithm == HashAlgorithmName.SHA256)
            return new System.Security.Cryptography.HMACSHA256(key);
        if (hashAlgorithm == HashAlgorithmName.SHA512)
            return new System.Security.Cryptography.HMACSHA512(key);

        throw new NotSupportedException($"PBKDF2 hash algorithm '{hashAlgorithm.Name}' is not supported.");
    }
}

internal static class ThreadCompat
{
    public static int GetCurrentProcessorId() => Environment.CurrentManagedThreadId;
}
}
#endif
