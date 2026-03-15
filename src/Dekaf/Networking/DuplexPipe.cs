using System.IO.Pipelines;
using System.Net.Sockets;

namespace Dekaf.Networking;

/// <summary>
/// Decouples reading from a <see cref="Stream"/> by pumping data into an internal <see cref="Pipe"/>.
/// This avoids a known .NET issue where <c>PipeReader.Create(Stream).ReadAsync</c> can block
/// indefinitely when concurrent reads and writes target the same underlying socket.
/// Used for TLS connections where <see cref="System.Net.Security.SslStream"/> requires the
/// <see cref="Stream"/> abstraction. Plain TCP connections use <see cref="SocketPipe"/> instead.
/// Writing is handled directly by <see cref="KafkaConnection"/> via <c>PipeWriter.Create(stream)</c>.
/// <para/>
/// <b>Ownership:</b> This class takes full ownership of both the <see cref="Stream"/> (typically
/// <see cref="System.Net.Security.SslStream"/>) and the underlying <see cref="System.Net.Sockets.Socket"/>.
/// It disposes the stream (which cascades to the inner <see cref="System.Net.Sockets.NetworkStream"/>
/// via <c>leaveInnerStreamOpen: false</c>) and then disposes the socket.
/// The caller must not dispose either resource separately.
/// </summary>
internal sealed class DuplexPipe : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly Socket _socket;
    private readonly Pipe _inputPipe;
    private readonly Task _readPumpTask;
    private readonly int _readBufferSize;
    private int _disposed;

    /// <summary>
    /// The application-facing reader. Data is pumped from the stream into this pipe
    /// by the read pump, decoupling stream reads from pipe reads.
    /// </summary>
    public PipeReader Input => _inputPipe.Reader;

    public DuplexPipe(Stream stream, Socket socket, PipeOptions inputPipeOptions, int readBufferSize = 65536)
    {
        _stream = stream;
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
                var bytesRead = await _stream.ReadAsync(memory).ConfigureAwait(false);

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

        try
        {
            // Dispose the stream to abort any pending _stream.ReadAsync in the read pump.
            // The read pump's finally block then calls _inputPipe.Writer.CompleteAsync.
            await _stream.DisposeAsync().ConfigureAwait(false);

            // Wait for the read pump to finish (observe exceptions from stream abort)
            try
            {
                await _readPumpTask.ConfigureAwait(false);
            }
            catch
            {
                // Read pump exceptions are expected during shutdown (e.g. stream disposed)
            }
        }
        finally
        {
            // Dispose the socket. The SslStream disposal above cascaded to the NetworkStream
            // (via leaveInnerStreamOpen: false), but NetworkStream was created with ownsSocket: false,
            // so the socket must be disposed separately.
            // Wrapped in finally to ensure the socket is disposed even if stream disposal throws.
            _socket.Dispose();
        }
    }
}
