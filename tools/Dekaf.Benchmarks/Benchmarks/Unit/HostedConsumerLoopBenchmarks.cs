using System.Reflection;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Extensions.Hosting;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Logging.Abstractions;

#if NET10_0_OR_GREATER
using StringSet = System.Collections.Generic.IReadOnlySet<string>;
using PartitionSet = System.Collections.Generic.IReadOnlySet<Dekaf.TopicPartition>;
#else
using StringSet = System.Collections.Generic.IReadOnlyCollection<string>;
using PartitionSet = System.Collections.Generic.IReadOnlyCollection<Dekaf.TopicPartition>;
#endif

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures the hosted consumer service's complete consume loop: one pass through
/// <c>ExecuteAsync</c> delivers <see cref="Operations"/> records, so startup is amortized.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class HostedConsumerLoopBenchmarks
{
    private const int Operations = 1024;
    private LoopService _service = null!;

    [GlobalSetup]
    public void Setup()
    {
        var consumer = DispatchProxy.Create<IBenchmarkConsumer, FixedRecordsConsumerProxy>();
        ((FixedRecordsConsumerProxy)(object)consumer).Records = new FixedRecords(Operations);
        _service = new LoopService(consumer);
    }

    [Benchmark(OperationsPerInvoke = Operations)]
    public Task ConsumeLoop() => _service.RunAsync();

    // A manual-commit consumer passes the service's retry-safe offset validation.
    public interface IBenchmarkConsumer : IKafkaConsumer<string, string>, IConsumerCommitConfiguration;

    // Only startup calls reach the proxy; records come from the reusable enumerable.
    public class FixedRecordsConsumerProxy : DispatchProxy
    {
        public FixedRecords Records { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            "InitializeAsync" => default(ValueTask),
            "Subscribe" => this,
            "ConsumeAsync" => Records,
            "get_OffsetCommitMode" => OffsetCommitMode.Manual,
            "get_EnableAutoOffsetStore" => false,
            "get_HasConsumerGroup" => true,
            _ => throw new NotSupportedException(targetMethod?.Name)
        };
    }

    public sealed class FixedRecords(int count) : IAsyncEnumerable<ConsumeResult<string, string>>
    {
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

        public async IAsyncEnumerator<ConsumeResult<string, string>> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
        {
            for (var index = 0; index < count; index++)
                yield return _result;

            await Task.CompletedTask.ConfigureAwait(false);
        }
    }

    private sealed class LoopService(IKafkaConsumer<string, string> consumer)
        : KafkaConsumerService<string, string>(consumer, NullLogger.Instance)
    {
        protected override IEnumerable<string> Topics => ["orders"];

        public Task RunAsync() => ExecuteAsync(CancellationToken.None);

        protected override ValueTask ProcessAsync(
            ConsumeResult<string, string> result,
            CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }
}

/// <summary>
/// Measures the hosted share consumer service's complete poll loop. Each invocation runs one
/// service through startup, <see cref="Operations"/> accepted records, and its final commit.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class HostedShareConsumerLoopBenchmarks
{
    private const int Operations = 1024;
    private readonly FixedRecordsShareConsumer _consumer = new(Operations);

    [Benchmark(OperationsPerInvoke = Operations)]
    public async Task PollLoop()
    {
        await using var service = new LoopService(_consumer);
        await service.RunAsync().ConfigureAwait(false);
    }

    private sealed class LoopService(IKafkaShareConsumer<string, string> consumer)
        : KafkaShareConsumerService<string, string>(consumer, NullLogger.Instance)
    {
        protected override IEnumerable<string> Topics => ["orders"];

        public Task RunAsync() => ExecuteAsync(CancellationToken.None);

        protected override ValueTask ProcessAsync(
            ShareConsumeResult<string, string> result,
            CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }

    private sealed class FixedRecordsShareConsumer(int count)
        : IKafkaShareConsumer<string, string>, IShareConsumerConfiguration
    {
        private readonly ShareConsumeResult<string, string> _record = new()
        {
            Topic = "orders",
            Partition = 0,
            Offset = 42,
            Key = "key",
            Value = "value",
            DeliveryCount = 1,
            Headers = []
        };

        public ShareAcknowledgementMode AcknowledgementMode => ShareAcknowledgementMode.Explicit;
        public StringSet Subscription { get; } = new HashSet<string>();
        public PartitionSet Assignment { get; } = new HashSet<TopicPartition>();
        public string? MemberId => "benchmark-member";

        // No broker-reported lock timeout: the service skips acquisition deadline checks.
        public int? AcquisitionLockTimeoutMs => null;

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => default;
        public IKafkaShareConsumer<string, string> Subscribe(params string[] topics) => this;
        public IKafkaShareConsumer<string, string> Unsubscribe() => this;

        public async IAsyncEnumerable<ShareConsumeResult<string, string>> PollAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (var index = 0; index < count; index++)
                yield return _record;

            await Task.CompletedTask.ConfigureAwait(false);
        }

        public void Acknowledge(ShareConsumeResult<string, string> record, AcknowledgeType type = AcknowledgeType.Accept)
        {
        }

        public ValueTask CommitAsync(CancellationToken cancellationToken = default) => default;
        public ValueTask CloseAsync(CancellationToken cancellationToken = default) => default;
        public ValueTask DisposeAsync() => default;
    }
}
