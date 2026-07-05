#if NETSTANDARD2_0
namespace System.IO;

using System.Buffers;
using System.Runtime.InteropServices;

internal static class StreamCompatibilityExtensions
{
    public static async ValueTask<int> ReadAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (MemoryMarshal.TryGetArray<byte>(buffer, out var segment))
            return await stream.ReadAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken).ConfigureAwait(false);

        var rented = ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            var read = await stream.ReadAsync(rented, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            rented.AsSpan(0, read).CopyTo(buffer.Span);
            return read;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public static async Task WriteAsync(this Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (MemoryMarshal.TryGetArray<byte>(buffer, out var segment))
        {
            await stream.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken).ConfigureAwait(false);
            return;
        }

        var rented = ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            buffer.Span.CopyTo(rented);
            await stream.WriteAsync(rented, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public static ValueTask DisposeAsync(this Stream stream)
    {
        stream.Dispose();
        return default;
    }
}
#endif
