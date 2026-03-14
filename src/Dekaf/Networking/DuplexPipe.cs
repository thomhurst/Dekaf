using System.IO.Pipelines;

namespace Dekaf.Networking;

/// <summary>
/// Bridges a <see cref="Stream"/> to the <see cref="IDuplexPipe"/> pattern using two internal
/// <see cref="Pipe"/> objects and async pump loops. This avoids a known .NET issue where
/// concurrent <c>StreamPipeReader</c> + <c>StreamPipeWriter</c> on the same stream causes
/// <c>PipeReader.ReadAsync</c> to block indefinitely.
/// </summary>
internal sealed class DuplexPipe : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly Pipe _inputPipe;
    private readonly Pipe _outputPipe;
    private readonly Task _readPumpTask;
    private readonly Task _writePumpTask;

    /// <summary>
    /// The application-facing reader. <see cref="ReceiveLoopAsync"/> reads responses from here.
    /// Data is pumped from the stream into this pipe by the read pump.
    /// </summary>
    public PipeReader Input => _inputPipe.Reader;

    /// <summary>
    /// The application-facing writer. <see cref="WriteRequestAsync"/> writes requests here.
    /// Data is drained from this pipe to the stream by the write pump.
    /// </summary>
    public PipeWriter Output => _outputPipe.Writer;

    public DuplexPipe(Stream stream, PipeOptions inputPipeOptions, PipeOptions outputPipeOptions)
    {
        _stream = stream;
        _inputPipe = new Pipe(inputPipeOptions);
        _outputPipe = new Pipe(outputPipeOptions);

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
                var memory = _inputPipe.Writer.GetMemory(4096);
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

                foreach (var segment in buffer)
                {
                    await _stream.WriteAsync(segment).ConfigureAwait(false);
                }

                if (!buffer.IsEmpty)
                    await _stream.FlushAsync().ConfigureAwait(false);

                _outputPipe.Reader.AdvanceTo(buffer.End);

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
        // Signal pumps to stop via pipe completion
        await _inputPipe.Reader.CompleteAsync().ConfigureAwait(false);
        await _outputPipe.Writer.CompleteAsync().ConfigureAwait(false);

        // Dispose the stream to abort any pending ReadAsync/WriteAsync calls
        // that would otherwise block the pump tasks indefinitely.
        // This must happen before awaiting the pump tasks.
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
