using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// FetchRequest.LastFetchedEpoch must describe the record at FetchOffset - 1. The prefetch
/// position runs ahead of the consumed position, so pairing it with the last CONSUMED epoch
/// makes the broker report a divergence after every leader change that falls between the two,
/// although nothing was truncated.
/// </summary>
public sealed class ConsumerLastFetchedEpochTests
{
    private const string Topic = "fetched-epoch-topic";
    private static readonly TopicPartition Partition = new(Topic, 0);

    [Test]
    public async Task Prefetch_AheadAcrossEpochBoundary_SendsEpochOfLastFetchedBatch()
    {
        // Epoch 0 wrote offsets 0-2, the leader changed, epoch 1 wrote offsets 3-5.
        var broker = new EpochAwareBroker((Epoch: 0, EndOffset: 3), (Epoch: 1, EndOffset: 6));
        var harness = await Harness.CreateAsync(broker, AutoOffsetReset.Latest);
        await using var _ = harness;

        // The position comes from a commit made under epoch 0; nothing is consumed afterwards,
        // so the consumed epoch stays 0 while the prefetch runs ahead into epoch 1.
        harness.Consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, 0, leaderEpoch: 0)]);

        await harness.PrefetchAsync();
        await harness.PrefetchAsync();

        await Assert.That(broker.Requests).Count().IsEqualTo(2);
        await Assert.That(broker.Requests[0]).IsEqualTo((0L, 0));
        await Assert.That(broker.Requests[1]).IsEqualTo((6L, 1));
        await Assert.That(broker.DivergingResponses).IsEqualTo(0);
    }

    [Test]
    public async Task Prefetch_AheadAcrossEpochBoundary_WithNoAutoReset_DoesNotReportLogTruncation()
    {
        var broker = new EpochAwareBroker((Epoch: 0, EndOffset: 3), (Epoch: 1, EndOffset: 6));
        var harness = await Harness.CreateAsync(broker, AutoOffsetReset.None);
        await using var _ = harness;
        harness.Consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, 0, leaderEpoch: 0)]);

        await harness.PrefetchAsync();
        await harness.PrefetchAsync();

        // The foreground poll applies staged divergences here; with AutoOffsetReset.None a
        // staged divergence surfaces as LogTruncationException.
        await Assert.That(harness.ApplyStagedFetchClears()).IsFalse();
        await Assert.That(broker.DivergingResponses).IsEqualTo(0);
        await Assert.That(harness.GetFetchPosition()).IsEqualTo(6);
    }

    [Test]
    public async Task Prefetch_GenuineTruncationOfFetchedEpoch_IsStillReported()
    {
        var broker = new EpochAwareBroker((Epoch: 0, EndOffset: 3), (Epoch: 1, EndOffset: 6));
        var harness = await Harness.CreateAsync(broker, AutoOffsetReset.None);
        await using var _ = harness;
        harness.Consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, 0, leaderEpoch: 0)]);

        await harness.PrefetchAsync();

        // An unclean election: the new leader never had offsets 4-5 of epoch 1 and writes
        // offsets 4-7 under epoch 2. The client fetched up to 6 under epoch 1.
        broker.ReplaceLog((Epoch: 0, EndOffset: 3), (Epoch: 1, EndOffset: 4), (Epoch: 2, EndOffset: 8));

        await harness.PrefetchAsync();

        await Assert.That(broker.Requests[1]).IsEqualTo((6L, 1));
        await Assert.That(broker.DivergingResponses).IsEqualTo(1);
        var exception = await Assert.That(() => harness.ApplyStagedFetchClears())
            .Throws<LogTruncationException>();
        await Assert.That(exception!.Message).Contains(Topic);
    }

    [Test]
    public async Task Seek_AfterPrefetchAhead_SendsTheSeekEpochNotTheFetchedOne()
    {
        var broker = new EpochAwareBroker((Epoch: 0, EndOffset: 3), (Epoch: 1, EndOffset: 6));
        var harness = await Harness.CreateAsync(broker, AutoOffsetReset.Latest);
        await using var _ = harness;
        harness.Consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, 0, leaderEpoch: 0)]);

        await harness.PrefetchAsync();

        // Rewinding the fetch position must rewind the epoch it is validated against, and a
        // seek to the very offset the prefetch reached must use the caller's epoch.
        harness.Consumer.Seek(new TopicPartitionOffset(Topic, 0, 2, leaderEpoch: 0));
        harness.ApplyStagedFetchClears();
        await harness.PrefetchAsync();
        await Assert.That(broker.Requests[1]).IsEqualTo((2L, 0));

        harness.Consumer.Seek(new TopicPartitionOffset(Topic, 0, 6));
        harness.ApplyStagedFetchClears();
        await harness.PrefetchAsync();
        await Assert.That(broker.Requests[2]).IsEqualTo((6L, -1));
    }

    /// <summary>
    /// Answers a fetch the way a partition leader does: a LastFetchedEpoch whose log ends
    /// before the fetch offset is a divergence, anything else returns the records from the
    /// fetch offset on, one batch per leader epoch.
    /// </summary>
    private sealed class EpochAwareBroker((int Epoch, long EndOffset)[] log)
    {
        private (int Epoch, long EndOffset)[] _log = log;

        public EpochAwareBroker((int Epoch, long EndOffset) first, (int Epoch, long EndOffset) second)
            : this([first, second])
        {
        }

        public List<(long FetchOffset, int LastFetchedEpoch)> Requests { get; } = [];

        public int DivergingResponses { get; private set; }

        public void ReplaceLog(params (int Epoch, long EndOffset)[] log) => _log = log;

        public FetchResponse Fetch(FetchRequest request)
        {
            var partition = request.Topics[0].Partitions[0];
            Requests.Add((partition.FetchOffset, partition.LastFetchedEpoch));

            var response = new FetchResponsePartition
            {
                PartitionIndex = 0,
                HighWatermark = _log[^1].EndOffset
            };

            if (partition.LastFetchedEpoch >= 0
                && TryGetDivergence(partition.FetchOffset, partition.LastFetchedEpoch, out var diverging))
            {
                DivergingResponses++;
                response.DivergingEpoch = diverging;
            }
            else
            {
                response.Records = ReadFrom(partition.FetchOffset);
            }

            return new FetchResponse
            {
                Responses = [new FetchResponseTopic { Topic = Topic, Partitions = [response] }]
            };
        }

        private bool TryGetDivergence(long fetchOffset, int lastFetchedEpoch, out EpochEndOffset diverging)
        {
            // The end offset of the largest epoch at or below the requested one.
            var epoch = -1;
            var endOffset = 0L;
            foreach (var segment in _log)
            {
                if (segment.Epoch > lastFetchedEpoch)
                    break;
                epoch = segment.Epoch;
                endOffset = segment.EndOffset;
            }

            diverging = new EpochEndOffset { Epoch = epoch, EndOffset = endOffset };
            return epoch < lastFetchedEpoch || endOffset < fetchOffset;
        }

        private List<RecordBatch>? ReadFrom(long fetchOffset)
        {
            List<RecordBatch>? batches = null;
            var startOffset = 0L;
            foreach (var segment in _log)
            {
                var first = Math.Max(startOffset, fetchOffset);
                if (first < segment.EndOffset)
                {
                    var records = new Record[segment.EndOffset - first];
                    for (var i = 0; i < records.Length; i++)
                        records[i] = new Record { OffsetDelta = i, Value = "value"u8.ToArray() };

                    (batches ??= []).Add(new RecordBatch
                    {
                        BaseOffset = first,
                        LastOffsetDelta = records.Length - 1,
                        PartitionLeaderEpoch = segment.Epoch,
                        Records = records
                    });
                }

                startOffset = segment.EndOffset;
            }

            return batches;
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly MetadataManager _metadataManager;

        private Harness(KafkaConsumer<string, string> consumer, MetadataManager metadataManager)
        {
            Consumer = consumer;
            _metadataManager = metadataManager;
        }

        public KafkaConsumer<string, string> Consumer { get; }

        public static Task<Harness> CreateAsync(EpochAwareBroker broker, AutoOffsetReset autoOffsetReset)
        {
            var pool = Substitute.For<IConnectionPool>();
            var connection = Substitute.For<IKafkaConnection>();
            connection.SendAsync<FetchRequest, FetchResponse>(
                    Arg.Any<FetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => new ValueTask<FetchResponse>(broker.Fetch(call.Arg<FetchRequest>())));
            pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>())
                .Returns(new ValueTask<IKafkaConnection>(connection));

            var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
            metadataManager.SetApiVersion(
                ApiKey.Fetch, FetchRequest.LowestSupportedVersion, FetchRequest.HighestSupportedVersion);
            var consumer = new KafkaConsumer<string, string>(
                new ConsumerOptions
                {
                    BootstrapServers = ["localhost:9092"],
                    ClientId = "test-consumer",
                    AutoOffsetReset = autoOffsetReset
                },
                Serializers.String,
                Serializers.String,
                pool,
                metadataManager);
            return Task.FromResult(new Harness(consumer, metadataManager));
        }

        public ValueTask PrefetchAsync()
        {
            var method = typeof(KafkaConsumer<string, string>).GetMethod(
                "PrefetchFromBrokerAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("PrefetchFromBrokerAsync method not found");
            return (ValueTask)method.Invoke(
                Consumer,
                [1, new List<TopicPartition> { Partition }, 0, 1, 0, GetFetchBufferEpoch(), CancellationToken.None])!;
        }

        public bool ApplyStagedFetchClears()
        {
            var method = typeof(KafkaConsumer<string, string>).GetMethod(
                "ClearFetchBufferForPendingCoordinatorRevocations",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    "ClearFetchBufferForPendingCoordinatorRevocations method not found");
            return method.CreateDelegate<Func<bool>>(Consumer)();
        }

        public long GetFetchPosition()
        {
            var field = typeof(KafkaConsumer<string, string>).GetField(
                "_fetchPositions", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("_fetchPositions field not found");
            return ((System.Collections.Concurrent.ConcurrentDictionary<TopicPartition, long>)field.GetValue(Consumer)!)[Partition];
        }

        private int GetFetchBufferEpoch()
        {
            var field = typeof(KafkaConsumer<string, string>).GetField(
                "_fetchBufferEpoch", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("_fetchBufferEpoch field not found");
            return (int)field.GetValue(Consumer)!;
        }

        public async ValueTask DisposeAsync()
        {
            await Consumer.DisposeAsync();
            await _metadataManager.DisposeAsync();
        }
    }
}
