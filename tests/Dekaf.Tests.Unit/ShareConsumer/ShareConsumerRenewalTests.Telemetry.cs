using System.Buffers;
using Dekaf.Protocol.Records;
using System.Reflection;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;
using Dekaf.Telemetry;
using Dekaf.Consumer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerRenewalTests
{
    private const string ShareMetricPrefix = "org.apache.kafka.consumer.share.fetch.manager.";

    [Test]
    [Arguments(false, 0)]
    [Arguments(true, 0)]
    [Arguments(false, 1)]
    [Arguments(true, 1)]
    [Arguments(false, 2)]
    [Arguments(true, 2)]
    public async Task Telemetry_FailedParsing_DiscardsUndisclosedRecords(bool batchApi, int preparationMode)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = CreateFetchResponse(0, 42, recordCount: 2)
        };
        var deserializer = preparationMode == 0
            ? new TelemetryFailingDeserializer()
            : new TelemetryFailingPreparer(preparationMode == 2);
        await using var fixture = CreateFixture(connection, valueDeserializer: deserializer);
        var metrics = EnableShareTelemetry(fixture.Consumer);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        if (batchApi)
        {
            await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
            await Assert.That(async () => await poll.MoveNextAsync()).Throws<InvalidOperationException>();
        }
        else
        {
            await using var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator();
            await Assert.That(async () => await poll.MoveNextAsync()).Throws<InvalidOperationException>();
        }

        await Assert.That(deserializer.Calls).IsGreaterThanOrEqualTo(1);
        await Assert.That(ShareMetricValue(metrics, "records.consumed.total")).IsEqualTo(0d);
        await Assert.That(ShareMetricValue(metrics, "bytes.consumed.total")).IsEqualTo(0d);
        await Assert.That(ShareMetricValue(metrics, "records.per.request.max")).IsEqualTo(0d);

        deserializer.Fail = false;
        if (batchApi)
        {
            await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
        }
        else
        {
            await using var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator();
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
        }
        await Assert.That(ShareMetricValue(metrics, "records.consumed.total")).IsEqualTo(2d);
        await Assert.That(ShareMetricValue(metrics, "bytes.consumed.total")).IsEqualTo(32d);
    }

    [Test]
    [Arguments(CoordinatorState.Stable, 0d)]
    [Arguments(CoordinatorState.Joining, 1d)]
    public async Task Telemetry_IdenticalAssignment_CountsOnlyCompletedRejoin(CoordinatorState state, double expected)
    {
        await using var fixture = CreateFixture(new CapturingConnection(ApiKey.ShareFetch, 2));
        var metrics = EnableShareTelemetry(fixture.Consumer);
        PrepareForPoll(fixture.Consumer);
        var coordinator = IdleCoordinator(fixture.Consumer);
        var assignment = coordinator.Assignment;
        typeof(ShareConsumerCoordinator).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(coordinator, state);
        PublishAssignment(coordinator, assigned: true);
        await Assert.That(ReferenceEquals(coordinator.Assignment, assignment)).IsTrue();
        var snapshot = new List<ClientTelemetryMetric>();
        metrics.Collect(new ClientTelemetrySubscription(Guid.Empty, 1, 0, 60000, 4096, false,
            ["org.apache.kafka.consumer.share.coordinator."]), snapshot);
        await Assert.That(snapshot.Single(metric => metric.Name.EndsWith("rebalance.total", StringComparison.Ordinal)).Value)
            .IsEqualTo(expected);
    }

    private class TelemetryFailingDeserializer : IDeserializer<string>
    {
        public int Calls { get; private set; }
        public bool Fail { get; set; } = true;

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            if (++Calls == 2 && Fail)
                throw new InvalidOperationException("Second record failed.");
            return Serializers.String.Deserialize(data, context);
        }
    }

    private sealed class TelemetryFailingPreparer(bool failPreparation)
        : TelemetryFailingDeserializer, IAsyncDeserializerPreparer<string>
    {
        public bool TryDeserialize(ReadOnlyMemory<byte> data, SerializationContext context, out string value)
        {
            if (Fail && failPreparation && Calls == 1)
            {
                value = string.Empty;
                return false;
            }
            value = Deserialize(data, context);
            return true;
        }

        public ValueTask PrepareAsync(ReadOnlyMemory<byte> data, SerializationContext context, CancellationToken cancellationToken)
            => ValueTask.FromException(new InvalidOperationException("Second record preparation failed."));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Telemetry_AcknowledgementOutcome_ExcludesGaps(bool failed)
    {
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2)
        {
            ShareAcknowledgeResponse = new ShareAcknowledgeResponse
            {
                ErrorCode = failed ? ErrorCode.InvalidRequest : ErrorCode.None,
                Responses = [], NodeEndpoints = []
            }
        };
        await using var fixture = CreateFixture(connection);
        var metrics = EnableShareTelemetry(fixture.Consumer);
        var acknowledgements = new Dictionary<TopicPartition, List<AcknowledgementBatchData>>
        {
            [new TopicPartition("topic", 0)] =
            [new AcknowledgementBatchData(42, 44, [(byte)AcknowledgeType.Accept, (byte)AcknowledgeType.Gap, (byte)AcknowledgeType.Release])]
        };
        await InvokeShareAcknowledgeAsync(fixture.Consumer, acknowledgements);
        await Assert.That(ShareMetricValue(metrics, "acknowledgements.send.total")).IsEqualTo(2d);
        await Assert.That(ShareMetricValue(metrics, "acknowledgements.error.total")).IsEqualTo(failed ? 2d : 0d);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Telemetry_PartialConsumption_CountsParsedRecordsOnce(bool batchApi)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = CreateFetchResponse(0, 42, recordCount: 3)
        };
        await using var fixture = CreateFixture(connection);
        var metrics = EnableShareTelemetry(fixture.Consumer);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        if (batchApi)
        {
            await using var enumerator = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
            await Assert.That(await enumerator.MoveNextAsync()).IsTrue();
            var records = enumerator.Current.GetEnumerator();
            await Assert.That(records.MoveNext()).IsTrue();
        }
        else
        {
            await using var enumerator = fixture.Consumer.PollAsync().GetAsyncEnumerator();
            await Assert.That(await enumerator.MoveNextAsync()).IsTrue();
        }
        await Assert.That(ShareMetricValue(metrics, "fetch.total")).IsEqualTo(1d);
        await Assert.That(ShareMetricValue(metrics, "records.consumed.total")).IsEqualTo(3d);
        await Assert.That(ShareMetricValue(metrics, "bytes.consumed.total")).IsGreaterThan(0d);
        await Assert.That(ShareMetricValue(metrics, "records.per.request.max")).IsEqualTo(3d);
    }

    [Test]
    public async Task Telemetry_FetchRetry_CountsEachSubmittedAttemptAndItsAcknowledgementFailure()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponses = new Queue<ShareFetchResponse>([
                new ShareFetchResponse { ErrorCode = ErrorCode.RequestTimedOut, Responses = [], NodeEndpoints = [] },
                new ShareFetchResponse { ErrorCode = ErrorCode.None, Responses = [], NodeEndpoints = [] }
            ])
        };
        await using var fixture = CreateFixture(connection);
        var metrics = EnableShareTelemetry(fixture.Consumer);
        await InvokeShareFetchAsync(fixture.Consumer, RenewalAcknowledgements());
        await Assert.That(ShareMetricValue(metrics, "fetch.total")).IsEqualTo(2d);
        await Assert.That(ShareMetricValue(metrics, "acknowledgements.send.total")).IsEqualTo(2d);
        await Assert.That(ShareMetricValue(metrics, "acknowledgements.error.total")).IsEqualTo(1d);
        await Assert.That(ShareMetricValue(metrics, "records.consumed.total")).IsEqualTo(0d);
    }

    [Test]
    public async Task Telemetry_RenewalReplay_DoesNotCountFetchedRecordsAgain()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = new ShareFetchResponse
            {
                ErrorCode = ErrorCode.None, AcquisitionLockTimeoutMs = 30000,
                Responses = [], NodeEndpoints = []
            }
        };
        await using var fixture = CreateFixture(connection);
        var metrics = EnableShareTelemetry(fixture.Consumer);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        var original = CreateRecord();
        fixture.Consumer.Acknowledge(original, AcknowledgeType.Renew);
        await using var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        await Assert.That(ReferenceEquals(poll.Current, original)).IsTrue();
        await Assert.That(ShareMetricValue(metrics, "records.consumed.total")).IsEqualTo(0d);
        await Assert.That(ShareMetricValue(metrics, "bytes.consumed.total")).IsEqualTo(0d);
        await Assert.That(ShareMetricValue(metrics, "acknowledgements.send.total")).IsEqualTo(1d);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Telemetry_FetchTotals_ExcludeUnacquiredRecordsAndCompactedGaps(bool batchApi)
    {
        var bytes = new ArrayBufferWriter<byte>();
        using (var batch = new RecordBatch
        {
            BaseOffset = 42, LastOffsetDelta = 5,
            Records = [
                new Record { OffsetDelta = 0, IsKeyNull = true, Value = "v"u8.ToArray() },
                new Record { OffsetDelta = 2, IsKeyNull = true, Value = "v"u8.ToArray() },
                new Record { OffsetDelta = 5, IsKeyNull = true, Value = "v"u8.ToArray() }
            ]
        }) batch.Write(bytes);
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = new ShareFetchResponse
            {
                ErrorCode = ErrorCode.None, NodeEndpoints = [],
                Responses = [new ShareFetchResponseTopic
                {
                    TopicId = TopicId,
                    Partitions = [new ShareFetchResponsePartition
                    {
                        PartitionIndex = 0, CurrentLeader = new ShareFetchLeaderIdAndEpoch(), RecordBytes = bytes.WrittenMemory,
                        AcquiredRecords = [new ShareFetchAcquiredRecords { FirstOffset = 42, LastOffset = 45, DeliveryCount = 1 }]
                    }]
                }]
            }
        };
        await using var fixture = CreateFixture(connection);
        var metrics = EnableShareTelemetry(fixture.Consumer);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        if (batchApi)
        {
            await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
            await Assert.That(poll.Current.Count).IsEqualTo(2);
        }
        else
        {
            await using var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator();
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
        }
        await Assert.That(ShareMetricValue(metrics, "records.consumed.total")).IsEqualTo(2d);
        await Assert.That(ShareMetricValue(metrics, "bytes.consumed.total")).IsEqualTo(16d);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Telemetry_ColdPreparationCountsRecordsOnlyAfterSuccessfulDeserialization(bool batchApi)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = CreateFetchResponse(0, 42, recordCount: 2)
        };
        var preparer = new PausedDeserializerPreparer();
        await using var fixture = CreateFixture(connection, valueDeserializer: preparer);
        var metrics = EnableShareTelemetry(fixture.Consumer);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        try
        {
            if (batchApi)
            {
                await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
                var pending = poll.MoveNextAsync();
                await preparer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await Assert.That(ShareMetricValue(metrics, "records.consumed.total")).IsEqualTo(0d);
                preparer.Release.TrySetResult();
                await Assert.That(await pending).IsTrue();
            }
            else
            {
                await using var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator();
                var pending = poll.MoveNextAsync();
                await preparer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await Assert.That(ShareMetricValue(metrics, "records.consumed.total")).IsEqualTo(0d);
                preparer.Release.TrySetResult();
                await Assert.That(await pending).IsTrue();
            }
            await Assert.That(ShareMetricValue(metrics, "records.consumed.total")).IsEqualTo(2d);
            await Assert.That(ShareMetricValue(metrics, "bytes.consumed.total")).IsEqualTo(32d);
        }
        finally
        {
            preparer.Release.TrySetResult();
        }
    }

    private static ShareConsumerTelemetryMetrics EnableShareTelemetry(KafkaShareConsumer<string, string> consumer)
    {
        var collector = (ClientTelemetryMetricCollector)typeof(KafkaShareConsumer<string, string>)
            .GetField("_telemetryMetricCollector", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(consumer)!;
        var metrics = collector.ShareConsumerMetrics!;
        metrics.Subscribe(["org.apache.kafka.consumer.share."]);
        return metrics;
    }

    private static double ShareMetricValue(ShareConsumerTelemetryMetrics metrics, string suffix)
    {
        var snapshot = new List<ClientTelemetryMetric>();
        metrics.Collect(new ClientTelemetrySubscription(Guid.Empty, 1, 0, 60000, 4096, false, [ShareMetricPrefix]), snapshot);
        return snapshot.Single(metric => metric.Name == ShareMetricPrefix + suffix).Value;
    }
}
