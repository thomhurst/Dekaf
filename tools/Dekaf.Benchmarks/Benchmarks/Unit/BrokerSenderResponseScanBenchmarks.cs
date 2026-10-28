using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Compression;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// The pending-response scan in <c>BrokerSender.ProcessCompletedResponses</c>: the send loop runs it
/// every time it wakes, and with a pipelined connection most entries are still on the wire. Every
/// entry here is unanswered, so each call measures the per-entry scan cost alone, without the
/// response handling that follows a claim. The send loop is stopped in setup, so the measured
/// thread owns the pending lists. The private method and fields are bound once through
/// reflection, so the fixture compiles against revisions that do not expose them.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class BrokerSenderResponseScanBenchmarks
{
    private static readonly ReadyBatch[] EmptyBatches = [];
    private static readonly int[] EmptyGenerations = [];

    private readonly TaskCompletionSource<ProduceResponse> _unanswered =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly BrokerSender.PartitionCarryOver _carryOver = new();
    private readonly Dictionary<BrokerSender.ProduceResponseKey, ProduceResponsePartitionData> _responseLookup = new();
    private BrokerSender _sender = null!;
    private RecordAccumulator _accumulator = null!;
    private MetadataManager _metadata = null!;
    private List<BrokerSender.PendingResponse> _pending = null!;
    private Action<BrokerSender.PartitionCarryOver, CancellationToken, Dictionary<BrokerSender.ProduceResponseKey, ProduceResponsePartitionData>?> _scan = null!;

    [Params(1, 8)]
    public int PendingCount { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            EnableAdaptiveConnections = false,
            EnableIdempotence = false,
            LingerMs = 1_000,
        };
        var pool = new Pool();
        _metadata = new MetadataManager(pool, options.BootstrapServers);
        _accumulator = new RecordAccumulator(options);
        _sender = new BrokerSender(
            brokerId: 1,
            pool,
            _metadata,
            _accumulator,
            options,
            new CompressionCodecRegistry(),
            new PartitionInflightTracker(),
            getProduceApiVersion: static () => 9,
            setProduceApiVersion: static _ => { },
            isTransactional: static () => false,
            tryEnsurePartitionsInTransaction: null,
            bumpEpoch: null,
            getProducerState: null,
            rerouteBatch: null,
            onAcknowledgement: null,
            logger: null);

        // Stop the send loop so it cannot scan, time out or claim the entries added below.
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        var senderType = typeof(BrokerSender);
        ((CancellationTokenSource)senderType.GetField("_cts", Instance)!.GetValue(_sender)!).Cancel();
        try
        {
            await ((Task)senderType.GetField("_sendLoopTask", Instance)!.GetValue(_sender)!).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _pending = ((List<BrokerSender.PendingResponse>[])senderType
            .GetField("_pendingResponsesByConnection", Instance)!
            .GetValue(_sender)!)[0];
        for (var i = 0; i < PendingCount; i++)
        {
            _pending.Add(new BrokerSender.PendingResponse(
                new PipelinedResponse<ProduceResponse>(_unanswered.Task),
                EmptyBatches,
                EmptyGenerations,
                topicIds: null,
                apiVersion: 12,
                count: 1,
                encodedBytes: 0,
                dataBytes: 0,
                requestStartTime: 0,
                default));
        }

        _scan = senderType
            .GetMethod("ProcessCompletedResponses", Instance)!
            .CreateDelegate<Action<BrokerSender.PartitionCarryOver, CancellationToken, Dictionary<BrokerSender.ProduceResponseKey, ProduceResponsePartitionData>?>>(_sender);
    }

    [Benchmark]
    public int ScanUnansweredResponses()
    {
        _scan(_carryOver, CancellationToken.None, _responseLookup);
        if (_pending.Count != PendingCount)
            throw new InvalidOperationException("An unanswered response must stay pending.");

        return _pending.Count;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _pending.Clear();
        await _sender.DisposeAsync();
        await _accumulator.DisposeAsync();
        await _metadata.DisposeAsync();
    }

    private sealed class Pool : IConnectionPool
    {
        public ValueTask<IKafkaConnection> GetConnectionAsync(int brokerId, CancellationToken token = default) => throw new NotSupportedException();
        public ValueTask<IKafkaConnection> GetConnectionAsync(string host, int port, CancellationToken token = default) => throw new NotSupportedException();
        public ValueTask<IKafkaConnection> GetConnectionByIndexAsync(int brokerId, int index, CancellationToken token = default) => throw new NotSupportedException();
        public void RegisterBroker(int id, string host, int port) { }
        public ValueTask<int> ScaleConnectionGroupAsync(int id, int count, CancellationToken token = default) => ValueTask.FromResult(1);
        public ValueTask<IKafkaConnection?> ShrinkConnectionGroupAsync(int id, int count, CancellationToken token = default) => ValueTask.FromResult<IKafkaConnection?>(null);
        public ValueTask RemoveConnectionAsync(int id) => ValueTask.CompletedTask;
        public ValueTask CloseAllAsync() => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
