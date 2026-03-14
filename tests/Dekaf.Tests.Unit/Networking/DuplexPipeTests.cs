using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Dekaf.Networking;

namespace Dekaf.Tests.Unit.Networking;

public sealed class DuplexPipeTests
{
    private static PipeOptions DefaultPipeOptions => new(useSynchronizationContext: false);

    /// <summary>
    /// Creates a pair of connected streams using TCP loopback for testing.
    /// </summary>
    private static async Task<(Stream Client, Stream Server)> CreateConnectedStreamsAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var client = new TcpClient();

        var acceptTask = listener.AcceptTcpClientAsync();
        await client.ConnectAsync(IPAddress.Loopback, port);
        var server = await acceptTask;

        listener.Stop();

        return (client.GetStream(), server.GetStream());
    }

    [Test]
    public async Task DataWrittenToStream_IsReadableFromInput()
    {
        var (clientStream, serverStream) = await CreateConnectedStreamsAsync();

        await using var pipe = new DuplexPipe(clientStream, DefaultPipeOptions, DefaultPipeOptions);

        var expected = "hello from broker"u8.ToArray();
        await serverStream.WriteAsync(expected);
        await serverStream.FlushAsync();

        var readResult = await pipe.Input.ReadAsync();
        var actual = BuffersExtensions.ToArray(readResult.Buffer);

        await Assert.That(actual).IsEquivalentTo(expected);

        pipe.Input.AdvanceTo(readResult.Buffer.End);
        await serverStream.DisposeAsync();
    }

    [Test]
    public async Task DataWrittenToOutput_AppearsInStream()
    {
        var (clientStream, serverStream) = await CreateConnectedStreamsAsync();

        await using var pipe = new DuplexPipe(clientStream, DefaultPipeOptions, DefaultPipeOptions);

        var expected = "hello from client"u8.ToArray();
        var memory = pipe.Output.GetMemory(expected.Length);
        expected.CopyTo(memory);
        pipe.Output.Advance(expected.Length);
        await pipe.Output.FlushAsync();

        var buffer = new byte[expected.Length];
        var totalRead = 0;
        while (totalRead < expected.Length)
        {
            var bytesRead = await serverStream.ReadAsync(buffer.AsMemory(totalRead));
            if (bytesRead == 0)
                break;
            totalRead += bytesRead;
        }

        await Assert.That(buffer).IsEquivalentTo(expected);

        await serverStream.DisposeAsync();
    }

    [Test]
    public async Task StreamEof_SetsIsCompletedOnInput()
    {
        var (clientStream, serverStream) = await CreateConnectedStreamsAsync();

        await using var pipe = new DuplexPipe(clientStream, DefaultPipeOptions, DefaultPipeOptions);

        // Close the server side to signal EOF
        await serverStream.DisposeAsync();

        var readResult = await pipe.Input.ReadAsync();

        await Assert.That(readResult.IsCompleted).IsTrue();

        pipe.Input.AdvanceTo(readResult.Buffer.End);
    }

    [Test]
    public async Task StreamReadError_PropagatesFromInput()
    {
        var (clientStream, serverStream) = await CreateConnectedStreamsAsync();

        // Dispose the client stream before wrapping to cause immediate read failure
        await serverStream.DisposeAsync();
        await clientStream.DisposeAsync();

        await using var pipe = new DuplexPipe(clientStream, DefaultPipeOptions, DefaultPipeOptions);

        await Assert.That(async () =>
        {
            var result = await pipe.Input.ReadAsync();
            pipe.Input.AdvanceTo(result.Buffer.End);
        }).ThrowsException();
    }

    [Test]
    public async Task DisposeAsync_StopsPumpsAndDisposesStream()
    {
        var (clientStream, serverStream) = await CreateConnectedStreamsAsync();

        var pipe = new DuplexPipe(clientStream, DefaultPipeOptions, DefaultPipeOptions);

        await pipe.DisposeAsync();

        // After dispose, the stream should be closed — server sees EOF
        var buffer = new byte[1];
        var bytesRead = await serverStream.ReadAsync(buffer);

        await Assert.That(bytesRead).IsEqualTo(0);

        await serverStream.DisposeAsync();
    }

    [Test]
    public async Task DoubleDispose_DoesNotThrow()
    {
        var (clientStream, serverStream) = await CreateConnectedStreamsAsync();

        var pipe = new DuplexPipe(clientStream, DefaultPipeOptions, DefaultPipeOptions);

        await pipe.DisposeAsync();

        await Assert.That(async () => await pipe.DisposeAsync()).ThrowsNothing();

        await serverStream.DisposeAsync();
    }

    [Test]
    public async Task BidirectionalCommunication_WorksSimultaneously()
    {
        var (clientStream, serverStream) = await CreateConnectedStreamsAsync();

        await using var pipe = new DuplexPipe(clientStream, DefaultPipeOptions, DefaultPipeOptions);

        var requestData = "request"u8.ToArray();
        var responseData = "response"u8.ToArray();

        // Server sends response
        await serverStream.WriteAsync(responseData);
        await serverStream.FlushAsync();

        // Client sends request via DuplexPipe
        var outMemory = pipe.Output.GetMemory(requestData.Length);
        requestData.CopyTo(outMemory);
        pipe.Output.Advance(requestData.Length);
        await pipe.Output.FlushAsync();

        // Verify client received response
        var readResult = await pipe.Input.ReadAsync();
        var receivedResponse = BuffersExtensions.ToArray(readResult.Buffer);
        await Assert.That(receivedResponse).IsEquivalentTo(responseData);
        pipe.Input.AdvanceTo(readResult.Buffer.End);

        // Verify server received request
        var serverBuffer = new byte[requestData.Length];
        var totalRead = 0;
        while (totalRead < requestData.Length)
        {
            var bytesRead = await serverStream.ReadAsync(serverBuffer.AsMemory(totalRead));
            if (bytesRead == 0)
                break;
            totalRead += bytesRead;
        }

        await Assert.That(serverBuffer).IsEquivalentTo(requestData);

        await serverStream.DisposeAsync();
    }
}
