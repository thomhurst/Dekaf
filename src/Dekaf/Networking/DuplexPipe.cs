using System.IO.Pipelines;

namespace Dekaf.Networking;

/// <summary>
/// Decouples reading from a <see cref="Stream"/> by pumping data into an internal <see cref="Pipe"/>.
/// This avoids a known .NET issue where <c>PipeReader.Create(Stream).ReadAsync</c> can block
/// indefinitely when concurrent reads and writes target the same underlying socket.
/// Writing is handled directly by <see cref="KafkaConnection"/> via <c>PipeWriter.Create(stream)</c>.
/// </summary>
internal sealed class DuplexPipe : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly Pipe _inputPipe;
    private readonly Task _readPumpTask;
    private readonly int _readBufferSize;
    private int _disposed;

    /// <summary>
    /// The application-facing reader. Data is pumped from the stream into this pipe
    /// by the read pump, decoupling stream reads from pipe reads.
    /// </summary>
    public PipeReader Input => _inputPipe.Reader;

    public DuplexPipe(Stream stream, PipeOptions inputPipeOptions, int readBufferSize = 65536)
    {
        _stream = stream;
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
            await _inputPipe.Writer.CompleteAsync(error).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

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
}
