using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.Hosting;
using Dekaf.Internal;
using Dekaf.Producer;
using Dekaf.ShareConsumer;
using Dekaf.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures the running hosted service loop with reusable acquired records.</summary>
[MemoryDiagnoser]
public class HostedShareConsumerProcessingBenchmarks
{
    private const int RecordsPerBatch = 1024;
    private Service _service = null!;
    private BenchmarkConsumer _consumer = null!;
    private readonly PendingHandler _handler = new();

    [Params(false, true)]
    public bool RoutingConfigured { get; set; }

    [Params(false, true)]
    public bool HandlerSuspends { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _consumer = new BenchmarkConsumer();
        _service = new Service(_consumer, HandlerSuspends ? _handler : null,
            RoutingConfigured ? new DeadLetterOptions() : null);
        await _service.StartAsync(default);
        await _consumer.Ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    [Benchmark(OperationsPerInvoke = RecordsPerBatch)]
    public async ValueTask<long> ProcessBatch()
    {
        var before = _consumer.Accepted;
        _consumer.Available.Signal();
        if (HandlerSuspends)
        {
            // Each handler suspends on a reusable IValueTaskSource. The caller then completes
            // it, so the production service exercises asynchronous completion without adding
            // application Task allocations or artificial thread-pool work to the measurement.
            for (var index = 0; index < RecordsPerBatch; index++)
            {
                if (!await _handler.Entered.WaitAsync(30_000))
                    throw new TimeoutException("The hosted handler did not start.");
                _handler.Complete();
            }
        }
        if (!await _consumer.Completed.WaitAsync(30_000) || _consumer.Accepted - before != RecordsPerBatch)
            throw new InvalidOperationException("Every supplied record must be processed and accepted.");
        return _consumer.Accepted;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _service.StopAsync(default);
        await _service.DisposeAsync();
        _handler.Entered.Dispose();
    }

    private sealed class Service(BenchmarkConsumer consumer, PendingHandler? handler, DeadLetterOptions? options)
        : KafkaShareConsumerService<string, string>(consumer, NullLogger.Instance, options)
    {
        protected override IEnumerable<string> Topics => ["orders"];
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> result, CancellationToken cancellationToken)
            => handler?.Begin() ?? ValueTask.CompletedTask;
        protected override IKafkaProducer<byte[]?, byte[]?> CreateDeadLetterProducer()
            => new InMemoryProducer<byte[]?, byte[]?>(new InMemoryKafkaCluster());
    }

    private sealed class PendingHandler : IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<bool> _core;
        internal readonly AsyncAutoResetSignal Entered = new(inlineContinuations: true);
        internal ValueTask Begin()
        {
            _core.Reset();
            Entered.Signal();
            return new ValueTask(this, _core.Version);
        }
        internal void Complete() => _core.SetResult(true);
        public void GetResult(short token) => _core.GetResult(token);
        public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);
        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => _core.OnCompleted(continuation, state, token, flags);
    }

    // The single, long-lived iterator supplies batches on demand. BDN controls invocation
    // counts and measurement. The batch size amortizes caller/service signaling; it does not
    // fix BDN invocation counts or bypass steady-state calibration. Existing share result/string
    // allocation, network I/O, raw capture, actual renewal requests and shutdown are excluded.
    private sealed class BenchmarkConsumer : IKafkaShareConsumer<string, string>, IShareConsumerConfiguration, IRawShareRecordAccessor
    {
        private readonly ShareConsumeResult<string, string> _record = new()
        { Topic = "orders", Partition = 0, Offset = 42, Key = "key", Value = "value", DeliveryCount = 1 };
        internal readonly AsyncAutoResetSignal Available = new(inlineContinuations: true);
        internal readonly AsyncAutoResetSignal Completed = new(inlineContinuations: true);
        internal readonly TaskCompletionSource Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal long Accepted;
        public ShareAcknowledgementMode AcknowledgementMode => ShareAcknowledgementMode.Explicit;
        public IReadOnlySet<string> Subscription { get; } = new HashSet<string> { "orders" };
        public IReadOnlySet<TopicPartition> Assignment { get; } = new HashSet<TopicPartition>();
        public string? MemberId => "benchmark";
        public int? AcquisitionLockTimeoutMs => 30_000;
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public IKafkaShareConsumer<string, string> Subscribe(params string[] topics) => this;
        public IKafkaShareConsumer<string, string> Unsubscribe() => this;
        public async IAsyncEnumerable<ShareConsumeResult<string, string>> PollAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Available.RegisterShutdownToken(cancellationToken);
            Ready.TrySetResult();
            while (!cancellationToken.IsCancellationRequested)
            {
                await Available.WaitAsync(Timeout.Infinite);
                for (var index = 0; index < RecordsPerBatch; index++)
                    yield return _record;
                Completed.Signal();
            }
        }
        public void Acknowledge(ShareConsumeResult<string, string> record, AcknowledgeType type = AcknowledgeType.Accept)
        {
            if (type != AcknowledgeType.Accept) throw new InvalidOperationException("The success fixture must only accept records.");
            Accepted++;
        }
        public ValueTask CommitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask CloseAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() { Available.Dispose(); Completed.Dispose(); return ValueTask.CompletedTask; }
        public void EnableRawRecordTracking() { }
        public bool TryGetRawRecord(TopicPartitionOffset record, out byte[]? key, out byte[]? value)
        { key = null; value = null; return false; }
    }
}
