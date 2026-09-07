using System.Reflection;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;
using Dekaf.Telemetry;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerRenewalTests
{
    [Test]
    [Arguments(false, 1)]
    [Arguments(false, 2)]
    [Arguments(true, 1)]
    [Arguments(true, 2)]
    public async Task Telemetry_PartialPollCountsOnlyParsedRecords_AndPreservesImplicitAcknowledgements(bool prepare, int maxRecords)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = CreateFetchResponse(0, 42, recordCount: 2)
        };
        var preparer = prepare ? new PausedDeserializerPreparer() : null;
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit,
            maxPollRecords: maxRecords, valueDeserializer: preparer);
        var collector = TelemetryCollector(fixture.Consumer);
        collector.ShareMetrics!.Configure([ShareConsumerTelemetryMetrics.Prefix]);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        await using (var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator())
        {
            var next = poll.MoveNextAsync();
            if (preparer is not null)
            {
                await preparer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                preparer.Release.SetResult();
            }
            await Assert.That(await next).IsTrue();
            await Assert.That(poll.Current.Offset).IsEqualTo(42);
        }
        var snapshot = ShareSnapshot(collector);
        await Assert.That(ShareValue(snapshot, "records.consumed.total")).IsEqualTo(maxRecords);
        await Assert.That(ShareValue(snapshot, "records.per.request.avg")).IsEqualTo(maxRecords);
        await Assert.That(ShareValue(snapshot, "bytes.consumed.total")).IsGreaterThan(0);
        await Assert.That(ShareValue(snapshot, "fetch.total")).IsEqualTo(1);
        var pending = FlushPendingAcknowledgements(fixture.Consumer);
        var acknowledged = pending[new TopicPartition("topic", 0)].Single();
        await Assert.That(acknowledged.FirstOffset).IsEqualTo(42);
        await Assert.That(acknowledged.LastOffset).IsEqualTo(42);
    }

    [Test]
    public async Task Telemetry_AcknowledgementRetryCountsEachAttempt_AndOnlyFailedPartition()
    {
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2)
        {
            ShareAcknowledgeResponses = new Queue<ShareAcknowledgeResponse>(
            [
                CreateAcknowledgeResponse((0, ErrorCode.None), (1, ErrorCode.NotLeaderOrFollower)),
                CreateAcknowledgeResponse((1, ErrorCode.None))
            ])
        };
        await using var fixture = CreateFixture(connection);
        var collector = TelemetryCollector(fixture.Consumer);
        collector.ShareMetrics!.Configure([ShareConsumerTelemetryMetrics.Prefix]);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40));
        fixture.Consumer.Acknowledge(CreateRecord(partition: 1, offset: 41));
        await fixture.Consumer.CommitAsync();
        var snapshot = ShareSnapshot(collector);
        await Assert.That(ShareValue(snapshot, "acknowledgements.send.total")).IsEqualTo(3);
        await Assert.That(ShareValue(snapshot, "acknowledgements.error.total")).IsEqualTo(1);
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();
    }

    [Test]
    public async Task Telemetry_FetchFailureCountsAttemptAndSentAcknowledgements()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchException = new InvalidOperationException("fetch failed")
        };
        await using var fixture = CreateFixture(connection);
        var collector = TelemetryCollector(fixture.Consumer);
        collector.ShareMetrics!.Configure([ShareConsumerTelemetryMetrics.Prefix]);
        await InvokeShareFetchAsync(fixture.Consumer, RenewalAcknowledgements());
        var snapshot = ShareSnapshot(collector);
        await Assert.That(ShareValue(snapshot, "fetch.total")).IsEqualTo(1);
        await Assert.That(ShareValue(snapshot, "acknowledgements.send.total")).IsEqualTo(1);
        await Assert.That(ShareValue(snapshot, "acknowledgements.error.total")).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Telemetry_InlineAcknowledgementErrorsCountAffectedRecords(bool topLevel)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = new ShareFetchResponse
            {
                ErrorCode = topLevel ? ErrorCode.InvalidRequest : ErrorCode.None,
                NodeEndpoints = [],
                Responses = [new ShareFetchResponseTopic
                {
                    TopicId = TopicId,
                    Partitions = [new ShareFetchResponsePartition
                    {
                        PartitionIndex = 0, CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
                        AcquiredRecords = [],
                        AcknowledgeErrorCode = topLevel ? ErrorCode.None : ErrorCode.InvalidRecordState
                    }]
                }]
            }
        };
        await using var fixture = CreateFixture(connection);
        var collector = TelemetryCollector(fixture.Consumer);
        collector.ShareMetrics!.Configure([ShareConsumerTelemetryMetrics.Prefix]);
        await InvokeShareFetchAsync(fixture.Consumer, RenewalAcknowledgements());
        var snapshot = ShareSnapshot(collector);
        await Assert.That(ShareValue(snapshot, "acknowledgements.send.total")).IsEqualTo(1);
        await Assert.That(ShareValue(snapshot, "acknowledgements.error.total")).IsEqualTo(1);
    }

    [Test]
    public async Task Telemetry_DoesNotCountAcknowledgementsOmittedFromWireRequest()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2);
        await using var fixture = CreateFixture(connection);
        var collector = TelemetryCollector(fixture.Consumer);
        collector.ShareMetrics!.Configure([ShareConsumerTelemetryMetrics.Prefix]);
        var missing = new TopicPartition("unknown-topic", 0);
        await InvokeShareFetchAsync(fixture.Consumer,
            new Dictionary<TopicPartition, List<AcknowledgementBatchData>>
            {
                [missing] = [new AcknowledgementBatchData(1, 1, [(byte)AcknowledgeType.Accept])]
            }, [missing]);
        await Assert.That(connection.ShareFetchRequest!.Topics.Count).IsEqualTo(0);
        await Assert.That(ShareValue(ShareSnapshot(collector), "acknowledgements.send.total")).IsEqualTo(0);
    }

    private static ClientTelemetryMetricCollector TelemetryCollector(KafkaShareConsumer<string, string> consumer) =>
        (ClientTelemetryMetricCollector)typeof(KafkaShareConsumer<string, string>)
            .GetField("_telemetryMetricCollector", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(consumer)!;

    private static ClientTelemetryMetricSnapshot ShareSnapshot(ClientTelemetryMetricCollector collector) =>
        collector.Collect(new ClientTelemetrySubscription(Guid.Empty, 1, 0, 1000, 100000, false, [ShareConsumerTelemetryMetrics.Prefix]));

    private static double ShareValue(ClientTelemetryMetricSnapshot snapshot, string suffix) =>
        snapshot.Metrics.Single(m => m.Name == ShareConsumerTelemetryMetrics.Prefix + "fetch.manager." + suffix).Value;
}
