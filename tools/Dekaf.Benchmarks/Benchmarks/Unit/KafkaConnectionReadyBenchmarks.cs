using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Networking;
using Dekaf.Protocol;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures TCP connection readiness including the mandatory per-connection ApiVersions RPC.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class KafkaConnectionReadyBenchmarks
{
    private TcpListener _listener = null!;
    private CancellationTokenSource _serverCancellation = null!;
    private Task _serverTask = null!;
    private int _port;

    [GlobalSetup]
    public void Setup()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _serverCancellation = new CancellationTokenSource();
        _serverTask = RunServerAsync(_serverCancellation.Token);
    }

    [Benchmark]
    [BenchmarkCategory("ConnectionSetup")]
    public async Task ConnectionReadyLatency()
    {
        await using var connection = new KafkaConnection(1, IPAddress.Loopback.ToString(), _port);
        await connection.ConnectAsync().ConfigureAwait(false);
        if (!connection.IsConnected)
            throw new InvalidOperationException("Connection was exposed before capability negotiation completed.");
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _serverCancellation.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        try
        {
            await _serverTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException) when (_serverCancellation.IsCancellationRequested)
        {
        }

        _serverCancellation.Dispose();
    }

    private async Task RunServerAsync(CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[sizeof(int)];
        var peerClosedBuffer = new byte[1];
        var response = BuildResponseFrame();

        while (!cancellationToken.IsCancellationRequested)
        {
            using var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var stream = client.GetStream();
                await stream.ReadExactlyAsync(lengthBuffer, cancellationToken).ConfigureAwait(false);
                var length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
                var request = ArrayPool<byte>.Shared.Rent(length);
                try
                {
                    await stream.ReadExactlyAsync(request.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
                    var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
                    BinaryPrimitives.WriteInt32BigEndian(response.AsSpan(sizeof(int)), correlationId);
                    await stream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
                    await stream.ReadAtLeastAsync(
                        peerClosedBuffer,
                        minimumBytes: 1,
                        throwOnEndOfStream: false,
                        cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(request);
                }
            }
            catch (IOException) when (!cancellationToken.IsCancellationRequested)
            {
                // KafkaConnection aborts the socket during disposal; continue serving the next setup.
            }
        }
    }

    private static byte[] BuildResponseFrame()
    {
        var body = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(body);
        writer.WriteInt16(0);
        writer.WriteUnsignedVarInt(2);
        writer.WriteInt16((short)ApiKey.ApiVersions);
        writer.WriteInt16(0);
        writer.WriteInt16(3);
        writer.WriteEmptyTaggedFields();
        writer.WriteInt32(0);
        writer.WriteEmptyTaggedFields();

        var frame = new byte[sizeof(int) + sizeof(int) + body.WrittenCount];
        BinaryPrimitives.WriteInt32BigEndian(frame, frame.Length - sizeof(int));
        body.WrittenSpan.CopyTo(frame.AsSpan(sizeof(int) * 2));
        return frame;
    }
}
