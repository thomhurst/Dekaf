using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Extensions.Hosting;
using Dekaf.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures the complete hosted-consumer processing chain for asynchronously completed handlers.</summary>
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
    private AsyncConsumerService _service = null!;
    private bool _hasInDoubtFailedRecord;

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
        _service = new AsyncConsumerService(consumer);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Operations)]
    public async ValueTask<bool> AdditionalAsyncWrapper()
    {
        for (var index = 0; index < Operations; index++)
            await ProcessTrackingInDoubtAsync(_result).ConfigureAwait(false);

        return _hasInDoubtFailedRecord;
    }

    [Benchmark(OperationsPerInvoke = Operations)]
    public async ValueTask<bool> ExistingLoopStateMachine()
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

    private sealed class AsyncConsumerService(IKafkaConsumer<string, string> consumer)
        : KafkaConsumerService<string, string>(consumer, NullLogger.Instance)
    {
        protected override IEnumerable<string> Topics => ["orders"];

        protected override async ValueTask ProcessAsync(
            ConsumeResult<string, string> result,
            CancellationToken cancellationToken)
        {
            await Task.Yield();
        }
    }
}
