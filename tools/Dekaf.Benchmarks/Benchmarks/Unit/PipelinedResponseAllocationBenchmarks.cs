using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures one small pipelined request/response round trip. The public baseline retains
/// its compatibility <see cref="Task{TResult}"/> adapter; the producer path consumes the
/// pooled response source directly.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 3)]
public class PipelinedResponseAllocationBenchmarks
{
    private TcpListener _listener = null!;
    private TcpClient _serverClient = null!;
    private KafkaConnection _connection = null!;
    private CancellationTokenSource _serverCancellation = null!;
    private Task _serverTask = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        var acceptTask = _listener.AcceptTcpClientAsync();
        _connection = new KafkaConnection(IPAddress.Loopback.ToString(), port);
        var connectTask = _connection.ConnectAsync();
        _serverClient = await acceptTask.ConfigureAwait(false);
        _serverCancellation = new CancellationTokenSource();
        _serverTask = RunServerAsync(_serverClient.GetStream(), _serverCancellation.Token);
        await connectTask.ConfigureAwait(false);
    }

    [Benchmark(Baseline = true)]
    public async Task<ErrorCode> PublicTaskAdapter()
    {
        var response = await _connection.SendPipelinedAsync<ApiVersionsRequest, ApiVersionsResponse>(
            CreateRequest(),
            apiVersion: 3).ConfigureAwait(false);
        return response.ErrorCode;
    }

    [Benchmark]
    public async ValueTask<ErrorCode> PooledProducerPath()
    {
        var response = await ((IKafkaPipelinedWriteCompletionConnection)_connection)
            .SendPipelinedAfterWriteAsync<ApiVersionsRequest, ApiVersionsResponse>(
                CreateRequest(),
                apiVersion: 3).ConfigureAwait(false);
        var parsed = await response.AsValueTask().ConfigureAwait(false);
        return parsed.ErrorCode;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _serverCancellation.CancelAsync().ConfigureAwait(false);
        await _connection.DisposeAsync().ConfigureAwait(false);
        try
        {
            await _serverTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _serverClient.Dispose();
        _listener.Stop();
        _serverCancellation.Dispose();
    }

    private static ApiVersionsRequest CreateRequest() => new()
    {
        ClientSoftwareName = "benchmark",
        ClientSoftwareVersion = "1.0"
    };

    private static async Task RunServerAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];
        while (!cancellationToken.IsCancellationRequested)
        {
            await stream.ReadExactlyAsync(lengthBuffer, cancellationToken).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
            var request = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                await stream.ReadExactlyAsync(request.AsMemory(0, length), cancellationToken)
                    .ConfigureAwait(false);
                var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4, 4));
                await stream.WriteAsync(BuildResponseFrame(correlationId), cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(request);
            }
        }
    }

    private static byte[] BuildResponseFrame(int correlationId)
    {
        var bodyBuffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(bodyBuffer);
        writer.WriteInt16(0);
        writer.WriteUnsignedVarInt(2);
        writer.WriteInt16((short)ApiKey.ApiVersions);
        writer.WriteInt16(0);
        writer.WriteInt16(3);
        writer.WriteEmptyTaggedFields();
        writer.WriteInt32(0);
        writer.WriteEmptyTaggedFields();

        var frame = new byte[8 + bodyBuffer.WrittenCount];
        BinaryPrimitives.WriteInt32BigEndian(frame, frame.Length - 4);
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(4), correlationId);
        bodyBuffer.WrittenSpan.CopyTo(frame.AsSpan(8));
        return frame;
    }
}
