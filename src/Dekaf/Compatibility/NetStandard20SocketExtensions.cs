#if NETSTANDARD2_0
namespace System.Net.Sockets;

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
            var read = await socket.ReceiveAsync(new ArraySegment<byte>(rented, 0, buffer.Length), socketFlags).ConfigureAwait(false);
            rented.AsSpan(0, read).CopyTo(buffer.Span);
            return read;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public static async Task ConnectAsync(this Socket socket, EndPoint remoteEndPoint, CancellationToken cancellationToken)
    {
        var connect = socket.ConnectAsync(remoteEndPoint);
        await connect.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
#endif
