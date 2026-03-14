using System.IO.Pipelines;

namespace Dekaf.Networking;

/// <summary>
/// Decouples reading from a <see cref="Stream"/> by pumping data into an internal <see cref="Pipe"/>,
/// while writing goes directly to the stream via <see cref="PipeWriter.Create(Stream, StreamPipeWriterOptions?)"/>.
/// This avoids a known .NET issue where concurrent <c>StreamPipeReader</c> + <c>StreamPipeWriter</c>
/// on the same stream causes <c>PipeReader.ReadAsync</c> to block indefinitely.
/// Only the read path needs the pump — the write path was never affected by the bug.
/// </summary>
internal sealed class DuplexPipe : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly Pipe _inputPipe;
    private readonly PipeWriter _writer;
    private readonly Task _readPumpTask;
    private readonly int _readBufferSize;
    private int _disposed;

    /// <summary>
    /// The application-facing reader. Data is pumped from the stream into this pipe
    /// by the read pump, decoupling stream reads from pipe reads.
    /// </summary>
    public PipeReader Input => _inputPipe.Reader;

    /// <summary>
    /// The application-facing writer. Writes go directly to the stream via
    /// <see cref="PipeWriter.Create(Stream, StreamPipeWriterOptions?)"/> — no pump needed.
    /// </summary>
    public PipeWriter Output => _writer;

    public DuplexPipe(
        Stream stream,
        PipeOptions inputPipeOptions,
        StreamPipeWriterOptions writerOptions,
        int readBufferSize = 65536)
    {
        _stream = stream;
        _inputPipe = new Pipe(inputPipeOptions);
        _writer = PipeWriter.Create(stream, writerOptions);
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

        // Complete the direct writer so no more data can be sent.
        // StreamPipeWriter.CompleteAsync may throw if it tries to flush pending data
        // to an already-broken stream — catch and continue with disposal.
        try
        {
            await _writer.CompleteAsync().ConfigureAwait(false);
        }
        catch
        {
            // Stream already broken (e.g. peer disconnected) — safe to ignore
        }

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
