using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Extensions.Hosting;
using Dekaf.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures the complete hosted-consumer processing chain before and after wrapper removal.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class HostedConsumerProcessingBenchmarks
{
    private const int Operations = 1024;
    private readonly ConsumeResult<string, string> _result = new(
        topic: "orders",
        partition: 1,
        offset: 42,
        keyData: default,
        isKeyNull: true,
        valueData: default,
        isValueNull: true,
        headers: null,
        timestampMs: 0,
        timestampType: TimestampType.NotAvailable,
        leaderEpoch: null,
        keyDeserializer: null,
        valueDeserializer: null);
    private ConsumerService _service = null!;
    private bool _hasInDoubtFailedRecord;

    [Params(false, true)]
    public bool HandlerSuspends { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var consumer = new InMemoryConsumer<string, string>(
            new InMemoryKafkaCluster(),
            new InMemoryConsumerOptions
            {
                OffsetCommitMode = OffsetCommitMode.Manual,
                EnableAutoOffsetStore = false
            });
        _service = new ConsumerService(consumer, HandlerSuspends);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Operations)]
    public async ValueTask<bool> Before_AdditionalAsyncWrapper()
    {
        for (var index = 0; index < Operations; index++)
            await ProcessTrackingInDoubtAsync(_result).ConfigureAwait(false);

        return _hasInDoubtFailedRecord;
    }

    [Benchmark(OperationsPerInvoke = Operations)]
    public async ValueTask<bool> After_ExistingLoopStateMachine()
    {
        for (var index = 0; index < Operations; index++)
        {
            try
            {
                await _service.ProcessWithRetriesAsync(_result, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                _hasInDoubtFailedRecord = true;
                throw;
            }
        }

        return _hasInDoubtFailedRecord;
    }

    private async ValueTask ProcessTrackingInDoubtAsync(ConsumeResult<string, string> result)
    {
        try
        {
            await _service.ProcessWithRetriesAsync(result, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            _hasInDoubtFailedRecord = true;
            throw;
        }
    }

    private sealed class ConsumerService(
        IKafkaConsumer<string, string> consumer,
        bool handlerSuspends)
        : KafkaConsumerService<string, string>(consumer, NullLogger.Instance)
    {
        protected override IEnumerable<string> Topics => ["orders"];

        protected override ValueTask ProcessAsync(
            ConsumeResult<string, string> result,
            CancellationToken cancellationToken)
            => handlerSuspends ? SuspendAsync() : ValueTask.CompletedTask;

        private static async ValueTask SuspendAsync()
        {
            await Task.Yield();
        }
    }
}

/// <summary>Measures durable discard handling in strict manual offset-store mode.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
[InvocationCount(256)]
public class HostedConsumerDiscardBenchmarks
{
    private const int Operations = 1024;
    private readonly ConsumeResult<string, string> _result = new(
        topic: "orders",
        partition: 1,
        offset: 42,
        keyData: default,
        isKeyNull: true,
        valueData: default,
        isValueNull: true,
        headers: null,
        timestampMs: 0,
        timestampType: TimestampType.NotAvailable,
        leaderEpoch: null,
        keyDeserializer: null,
        valueDeserializer: null);
    private DiscardingConsumerService _before = null!;
    private DiscardingConsumerService _after = null!;

    [GlobalSetup]
    public void Setup()
    {
        _before = CreateService(enableAutoOffsetStore: true);
        _after = CreateService(enableAutoOffsetStore: false);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Operations)]
    public async ValueTask Before_DiscardWithoutExplicitStore()
    {
        for (var index = 0; index < Operations; index++)
            await _before.ProcessWithRetriesAsync(_result, CancellationToken.None).ConfigureAwait(false);
    }

    [Benchmark(OperationsPerInvoke = Operations)]
    public async ValueTask After_StrictManualDiscardWithStore()
    {
        for (var index = 0; index < Operations; index++)
            await _after.ProcessWithRetriesAsync(_result, CancellationToken.None).ConfigureAwait(false);
    }

    private static DiscardingConsumerService CreateService(bool enableAutoOffsetStore)
    {
        var consumer = new InMemoryConsumer<string, string>(
            new InMemoryKafkaCluster(),
            new InMemoryConsumerOptions
            {
                GroupId = "hosted-discard-benchmark",
                OffsetCommitMode = OffsetCommitMode.Manual,
                EnableAutoOffsetStore = enableAutoOffsetStore
            });
        return new DiscardingConsumerService(consumer);
    }

    private sealed class DiscardingConsumerService(IKafkaConsumer<string, string> consumer)
        : KafkaConsumerService<string, string>(consumer, NullLogger.Instance)
    {
        private static readonly InvalidOperationException Failure = new("Invalid record");

        protected override IEnumerable<string> Topics => ["orders"];

        protected override ValueTask ProcessAsync(
            ConsumeResult<string, string> result,
            CancellationToken cancellationToken)
            => throw Failure;

        protected override ValueTask OnErrorAsync(
            Exception exception,
            ConsumeResult<string, string>? result,
            CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        protected override ValueTask<MessageFailureDisposition> GetFailureDispositionAsync(
            MessageFailureContext<string, string> context,
            CancellationToken cancellationToken)
            => new(MessageFailureDisposition.Discard);
    }
}
