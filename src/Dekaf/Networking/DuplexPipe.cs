using System.IO.Pipelines;

namespace Dekaf.Networking;

/// <summary>
/// Bridges a <see cref="Stream"/> to two internal <see cref="Pipe"/> objects with async pump loops,
/// following the Kestrel SocketConnection pattern. This avoids a known .NET issue where concurrent
/// <c>StreamPipeReader</c> + <c>StreamPipeWriter</c> on the same stream causes
/// <c>PipeReader.ReadAsync</c> to block indefinitely.
///
/// Both pipes use <see cref="PipeScheduler.Inline"/> for the consumer-side scheduler so that
/// continuations run inline without thread pool dispatch — matching the original direct
/// <c>PipeReader.Create</c>/<c>PipeWriter.Create</c> latency characteristics.
/// </summary>
internal sealed class DuplexPipe : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly Pipe _inputPipe;
    private readonly Pipe _outputPipe;
    private readonly Task _readPumpTask;
    private readonly Task _writePumpTask;
    private readonly int _readBufferSize;
    private int _disposed;

    /// <summary>
    /// The application-facing reader. Data is pumped from the stream into this pipe
    /// by the read pump, decoupling stream reads from pipe reads.
    /// </summary>
    public PipeReader Input => _inputPipe.Reader;

    /// <summary>
    /// The application-facing writer. Data written here is drained to the stream
    /// by the write pump.
    /// </summary>
    public PipeWriter Output => _outputPipe.Writer;

    public DuplexPipe(Stream stream, PipeOptions inputPipeOptions, PipeOptions outputPipeOptions, int readBufferSize = 65536)
    {
        _stream = stream;
        _inputPipe = new Pipe(inputPipeOptions);
        _outputPipe = new Pipe(outputPipeOptions);
        _readBufferSize = readBufferSize;

        // Start pump tasks inline — they yield at their first await
        _readPumpTask = ReadPumpAsync();
        _writePumpTask = WritePumpAsync();
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

    private async Task WritePumpAsync()
    {
        Exception? error = null;
        try
        {
            while (true)
            {
                var readResult = await _outputPipe.Reader.ReadAsync().ConfigureAwait(false);
                var buffer = readResult.Buffer;

                try
                {
                    if (!buffer.IsEmpty)
                    {
                        if (buffer.IsSingleSegment)
                        {
                            await _stream.WriteAsync(buffer.First).ConfigureAwait(false);
                        }
                        else
                        {
                            foreach (var segment in buffer)
                            {
                                await _stream.WriteAsync(segment).ConfigureAwait(false);
                            }
                        }

                        await _stream.FlushAsync().ConfigureAwait(false);
                    }
                }
                finally
                {
                    // Safe even on partial write failure: any exception propagates to the
                    // outer catch, which completes the pipe with the error and tears down
                    // the connection. The partially-consumed data is never re-read.
                    _outputPipe.Reader.AdvanceTo(buffer.End);
                }

                if (readResult.IsCompleted)
                    break;
            }
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            await _outputPipe.Reader.CompleteAsync(error).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        // Signal the write pump to stop: completing the writer causes the write pump's
        // _outputPipe.Reader.ReadAsync to return IsCompleted=true. This is necessary because
        // the write pump reads from the pipe (not the stream), so stream disposal alone
        // won't unblock it if it's waiting for more data.
        await _outputPipe.Writer.CompleteAsync().ConfigureAwait(false);

        // Dispose the stream to abort any pending _stream.ReadAsync/_stream.WriteAsync calls
        // in the pump tasks. The read pump is blocked on _stream.ReadAsync, so this is what
        // unblocks it (the read pump's finally block then calls _inputPipe.Writer.CompleteAsync).
        await _stream.DisposeAsync().ConfigureAwait(false);

        // Wait for pumps to finish (observe exceptions from stream abort)
        try
        {
            await Task.WhenAll(_readPumpTask, _writePumpTask).ConfigureAwait(false);
        }
        catch
        {
            // Pump exceptions are expected during shutdown (e.g. stream disposed)
        }
    }
}
