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
            await _inputPipe.Writer.CompleteAsync(error).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        // Shutdown the socket receive side to abort any pending ReceiveAsync in the pump.
        // The pump will see 0 bytes (EOF) or get a SocketException, then complete the pipe writer.
        try
        {
            _socket.Shutdown(SocketShutdown.Receive);
        }
        catch
        {
            // Socket may already be closed
        }

        // Wait for the read pump to finish (observe exceptions from socket shutdown)
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
