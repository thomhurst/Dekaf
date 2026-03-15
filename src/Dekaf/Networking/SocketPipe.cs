using System.IO.Pipelines;
using System.Net.Sockets;

namespace Dekaf.Networking;

/// <summary>
/// High-performance read pump that reads directly from a <see cref="Socket"/> into a <see cref="Pipe"/>,
/// bypassing the <see cref="Stream"/> abstraction. Uses <see cref="PipeScheduler.Inline"/> to eliminate
/// thread pool context switches — the reader continuation runs directly on the pump thread.
/// <para/>
/// This avoids a known .NET issue where <c>PipeReader.Create(Stream).ReadAsync</c> can block
/// indefinitely when concurrent reads and writes target the same underlying socket.
/// Used for plain TCP connections. TLS connections use <see cref="DuplexPipe"/> instead
/// (because <see cref="System.Net.Security.SslStream"/> requires the <see cref="Stream"/> abstraction).
/// <para/>
/// <b>Ownership:</b> This class takes ownership of the <see cref="Socket"/> and closes it on disposal
/// via <see cref="Socket.Close(int)"/>. The caller must not dispose the socket separately.
/// The caller remains responsible for disposing any <see cref="Stream"/> wrapper (e.g., <see cref="System.Net.Sockets.NetworkStream"/>).
/// </summary>
internal sealed class SocketPipe : IAsyncDisposable
{
    private readonly Socket _socket;
    private readonly Pipe _inputPipe;
    private readonly Task _readPumpTask;
    private readonly int _readBufferSize;
    private int _disposed;

    /// <summary>
    /// The application-facing reader. Data is pumped from the socket into this pipe
    /// by the read pump, decoupling socket reads from pipe reads.
    /// </summary>
    public PipeReader Input => _inputPipe.Reader;

    public SocketPipe(Socket socket, PipeOptions inputPipeOptions, int readBufferSize = 65536)
    {
        _socket = socket;
        _inputPipe = new Pipe(inputPipeOptions);
        _readBufferSize = readBufferSize;

        // Start pump task inline — it yields at its first await
        _readPumpTask = ReadPumpAsync();
    }

    private async Task ReadPumpAsync()
    {
        Exception? error = null;
        try
        {
            while (true)
            {
                var memory = _inputPipe.Writer.GetMemory(_readBufferSize);
                var bytesRead = await _socket.ReceiveAsync(memory, SocketFlags.None).ConfigureAwait(false);

                if (bytesRead == 0)
                    break; // EOF

                _inputPipe.Writer.Advance(bytesRead);

                var flushResult = await _inputPipe.Writer.FlushAsync().ConfigureAwait(false);

                if (flushResult.IsCompleted || flushResult.IsCanceled)
                    break;
            }
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            // Only complete the Writer (producer side). The Reader is owned by ReceiveLoopAsync
            // and will see IsCompleted when the writer is done — completing it here could race
            // with an active ReadAsync if the pump exits before disposal.
            await _inputPipe.Writer.CompleteAsync(error).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        // Complete the pipe reader to unblock the pump if it is paused on
        // PipeWriter.FlushAsync() due to backpressure (pipe buffer full, no reader consuming).
        // FlushAsync returns with IsCompleted=true, allowing the pump to break out of its loop.
        await _inputPipe.Reader.CompleteAsync().ConfigureAwait(false);

        // Close the socket to abort any pending ReceiveAsync in the pump.
        // Socket.Shutdown(SocketShutdown.Receive) is insufficient on Windows — it only
        // affects future receives, not already-posted IOCP operations. Closing the socket
        // handle aborts the pending ReceiveAsync with a SocketException.
        try
        {
            _socket.Close(0);
        }
        catch
        {
            // Socket may already be closed
        }

        // Wait for the read pump to finish (observe exceptions from socket closure)
        try
        {
            await _readPumpTask.ConfigureAwait(false);
        }
        catch
        {
            // Read pump exceptions are expected during shutdown
        }
    }
}
