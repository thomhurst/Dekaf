using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Linq.Expressions;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Telemetry;

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
    private Func<ApiVersionsRequest, short, CancellationToken, ValueTask<ApiVersionsResponse>> _sendObserved = null!;
    private Func<ApiVersionsRequest, short, CancellationToken, ValueTask<ApiVersionsResponse>> _sendObservedAfterWrite = null!;

    private static readonly Action WriteStarted = static () => { };
    private ClientTelemetryMetricCollector _telemetryCollector = null!;
    private TelemetrySender _sendTelemetry = null!;
    private PipelinedTelemetrySender _sendPipelinedTelemetry = null!;

    private delegate ValueTask<ApiVersionsResponse> TelemetrySender(ApiVersionsRequest request,
        short version, ClientTelemetryMetricCollector collector, Action callback, CancellationToken cancellationToken);
    private delegate ValueTask<PipelinedResponse<ApiVersionsResponse>> PipelinedTelemetrySender(ApiVersionsRequest request,
        short version, ClientTelemetryMetricCollector collector, Action callback, CancellationToken cancellationToken);

    [GlobalSetup]
    public async Task Setup()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        var acceptTask = _listener.AcceptTcpClientAsync();
        _connection = new KafkaConnection(1, IPAddress.Loopback.ToString(), port);
        var connectTask = _connection.ConnectAsync();
        _serverClient = await acceptTask.ConfigureAwait(false);
        _serverCancellation = new CancellationTokenSource();
        _serverTask = RunServerAsync(_serverClient.GetStream(), _serverCancellation.Token);
        await connectTask.ConfigureAwait(false);
        _sendObserved = CreateObservedSender();
        _sendObservedAfterWrite = CreateObservedAfterWriteSender();
        _telemetryCollector = new(ClientTelemetryClientRole.Producer);
        _telemetryCollector.RecordRequestLatency(1, TimeSpan.FromMilliseconds(1));
        _sendTelemetry = CreateTelemetrySender();
        _sendPipelinedTelemetry = CreatePipelinedTelemetrySender();
    }

    private Func<ApiVersionsRequest, short, CancellationToken, ValueTask<ApiVersionsResponse>> CreateObservedSender()
    {
        // Bind outside measurement so the same fixture source builds against the baseline,
        // whose ordinary SendAsync has one cancellation token for the whole request.
        var assembly = typeof(KafkaConnection).Assembly;
        var capability = assembly.GetType("Dekaf.Networking.IKafkaRequestCancellationConnection");
        var contextType = assembly.GetType("Dekaf.Networking.KafkaRequestWriteContext");
        if (capability is null || contextType is null)
            return _connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>;
        var context = Activator.CreateInstance(contextType, BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, args: [_serverCancellation.Token], culture: null)!;
        var method = capability.GetMethod("SendWithResponseCancellationAsync")!
            .MakeGenericMethod(typeof(ApiVersionsRequest), typeof(ApiVersionsResponse));
        var request = Expression.Parameter(typeof(ApiVersionsRequest));
        var version = Expression.Parameter(typeof(short));
        var token = Expression.Parameter(typeof(CancellationToken));
        return Expression.Lambda<Func<ApiVersionsRequest, short, CancellationToken, ValueTask<ApiVersionsResponse>>>(
            Expression.Call(Expression.Convert(Expression.Constant(_connection), capability), method,
                request, version, Expression.Constant(context, contextType), token),
            request, version, token).Compile();
    }

    private Func<ApiVersionsRequest, short, CancellationToken, ValueTask<ApiVersionsResponse>> CreateObservedAfterWriteSender()
    {
        // The close-leave send: the caller's token bounds the frame write, the context's token
        // only the response wait. A baseline without it measures the response-cancellation send.
        var contextType = typeof(KafkaConnection).Assembly.GetType("Dekaf.Networking.KafkaRequestWriteContext");
        var method = typeof(KafkaConnection).GetMethod("SendWithResponseCancellationAfterWriteAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method is null || contextType is null)
            return _sendObserved;
        var context = Activator.CreateInstance(contextType, BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, args: [_serverCancellation.Token], culture: null)!;
        var request = Expression.Parameter(typeof(ApiVersionsRequest));
        var version = Expression.Parameter(typeof(short));
        var token = Expression.Parameter(typeof(CancellationToken));
        return Expression.Lambda<Func<ApiVersionsRequest, short, CancellationToken, ValueTask<ApiVersionsResponse>>>(
            Expression.Call(Expression.Constant(_connection),
                method.MakeGenericMethod(typeof(ApiVersionsRequest), typeof(ApiVersionsResponse)),
                request, version, Expression.Constant(null, typeof(ClientTelemetryMetricCollector)),
                Expression.Constant(context, contextType), token),
            request, version, token).Compile();
    }

    private TelemetrySender CreateTelemetrySender()
    {
        // Bind once so this fixture also compiles against the pre-attribution baseline.
        var method = typeof(KafkaConnection).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .SingleOrDefault(candidate => candidate.Name == "SendWithTelemetryAsync" && candidate.GetParameters().Length == 5);
        if (method is null)
            return (request, version, _, callback, token) =>
                ((IKafkaRequestWriteObserverConnection)_connection).SendWithWriteObservationAsync<ApiVersionsRequest, ApiVersionsResponse>(
                    request, version, callback, token);
        return method.MakeGenericMethod(typeof(ApiVersionsRequest), typeof(ApiVersionsResponse))
            .CreateDelegate<TelemetrySender>(_connection);
    }

    private PipelinedTelemetrySender CreatePipelinedTelemetrySender()
    {
        var method = typeof(KafkaConnection).GetMethod("SendPipelinedWithTelemetryAfterWriteAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method is null)
            return (request, version, _, callback, token) =>
                ((IKafkaRequestWriteObserverConnection)_connection).SendPipelinedWithWriteObservationAfterWriteAsync<ApiVersionsRequest, ApiVersionsResponse>(
                    request, version, callback, token);
        return method.MakeGenericMethod(typeof(ApiVersionsRequest), typeof(ApiVersionsResponse))
            .CreateDelegate<PipelinedTelemetrySender>(_connection);
    }

    [Benchmark]
    public async ValueTask<ErrorCode> SharedObservedControlRequest()
    {
        var response = await _sendTelemetry(CreateRequest(), 3, _telemetryCollector, WriteStarted, CancellationToken.None);
        return response.ErrorCode;
    }

    [Benchmark]
    public async ValueTask<ErrorCode> SharedPipelinedControlRequest()
    {
        var pending = await _sendPipelinedTelemetry(CreateRequest(), 3, _telemetryCollector, WriteStarted, CancellationToken.None);
        var response = await pending.AsValueTask();
        return response.ErrorCode;
    }

    [Benchmark]
    public async ValueTask<ErrorCode> OrdinaryRequest()
    {
        var response = await _connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(CreateRequest(), 3);
        return response.ErrorCode;
    }

    [Benchmark]
    public async ValueTask<ErrorCode> ObservedRequest()
    {
        var response = await _sendObserved(CreateRequest(), 3, CancellationToken.None);
        return response.ErrorCode;
    }

    [Benchmark]
    public async ValueTask<ErrorCode> ObservedAfterWriteRequest()
    {
        var response = await _sendObservedAfterWrite(CreateRequest(), 3, CancellationToken.None);
        return response.ErrorCode;
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
