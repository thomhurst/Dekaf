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
    /// Returns the client socket alongside the streams so DuplexPipe can take ownership.
    /// </summary>
    private static async Task<(Socket ClientSocket, Stream ClientStream, Stream ServerStream)> CreateConnectedStreamsAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var client = new TcpClient();

        var acceptTask = listener.AcceptTcpClientAsync();
        await client.ConnectAsync(IPAddress.Loopback, port);
        var server = await acceptTask;

        listener.Stop();

        return (client.Client, client.GetStream(), server.GetStream());
    }

    [Test]
    public async Task DataWrittenToStream_IsReadableFromInput()
    {
        var (clientSocket, clientStream, serverStream) = await CreateConnectedStreamsAsync();

        await using var pipe = new DuplexPipe(clientStream, clientSocket, DefaultPipeOptions);

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
    public async Task StreamEof_SetsIsCompletedOnInput()
    {
        var (clientSocket, clientStream, serverStream) = await CreateConnectedStreamsAsync();

        await using var pipe = new DuplexPipe(clientStream, clientSocket, DefaultPipeOptions);

        // Close the server side to signal EOF
        await serverStream.DisposeAsync();

        var readResult = await pipe.Input.ReadAsync();

        await Assert.That(readResult.IsCompleted).IsTrue();

        pipe.Input.AdvanceTo(readResult.Buffer.End);
    }

    [Test]
    public async Task StreamReadError_PropagatesFromInput()
    {
        var (clientSocket, clientStream, serverStream) = await CreateConnectedStreamsAsync();

        // Dispose the client stream before wrapping to cause immediate read failure
        await serverStream.DisposeAsync();
        await clientStream.DisposeAsync();

        await using var pipe = new DuplexPipe(clientStream, clientSocket, DefaultPipeOptions);

        await Assert.That(async () =>
        {
            var result = await pipe.Input.ReadAsync();
            pipe.Input.AdvanceTo(result.Buffer.End);
        }).ThrowsException();
    }

    [Test]
    public async Task StreamErrorDuringActiveRead_PropagatesFromInput()
    {
        var (clientSocket, clientStream, serverStream) = await CreateConnectedStreamsAsync();

        await using var pipe = new DuplexPipe(clientStream, clientSocket, DefaultPipeOptions);

        // Successfully exchange data first
        var data = "hello"u8.ToArray();
        await serverStream.WriteAsync(data);
        await serverStream.FlushAsync();

        var readResult = await pipe.Input.ReadAsync();
        await Assert.That(readResult.Buffer.Length).IsGreaterThanOrEqualTo(data.Length);
        pipe.Input.AdvanceTo(readResult.Buffer.End);

        // Now kill the server mid-operation — simulates broker crash
        await serverStream.DisposeAsync();

        // Next read should see EOF (IsCompleted) or propagate an error
        readResult = await pipe.Input.ReadAsync();
        await Assert.That(readResult.IsCompleted).IsTrue();
        pipe.Input.AdvanceTo(readResult.Buffer.End);
    }

    [Test]
    public async Task DisposeAsync_StopsPumpAndDisposesStream()
    {
        var (clientSocket, clientStream, serverStream) = await CreateConnectedStreamsAsync();

        var pipe = new DuplexPipe(clientStream, clientSocket, DefaultPipeOptions);

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
        var (clientSocket, clientStream, serverStream) = await CreateConnectedStreamsAsync();

        var pipe = new DuplexPipe(clientStream, clientSocket, DefaultPipeOptions);

        await pipe.DisposeAsync();

        await Assert.That(async () => await pipe.DisposeAsync()).ThrowsNothing();

        await serverStream.DisposeAsync();
    }
}
