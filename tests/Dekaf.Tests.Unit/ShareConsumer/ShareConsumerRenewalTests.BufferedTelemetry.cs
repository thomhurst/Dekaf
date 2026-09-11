using Dekaf.Protocol;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerRenewalTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Telemetry_BufferedWindows_AccumulateOriginalFetch(bool prepared)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = CreateFetchResponse(0, 42, recordCount: 3)
        };
        await using var fixture = CreateFixture(connection, maxPollRecords: 1,
            valueDeserializer: prepared ? new BufferedTelemetryPreparer() : Serializers.String);
        var metrics = EnableShareTelemetry(fixture.Consumer);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");

        for (var index = 0; index < 3; index++)
        {
            await using var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator();
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
            await Assert.That(poll.Current.Offset).IsEqualTo(42L + index);
            await Assert.That(ShareMetricValue(metrics, "records.consumed.total")).IsEqualTo((double)index + 1);
            await Assert.That(ShareMetricValue(metrics, "bytes.consumed.total")).IsEqualTo(16d * (index + 1));
            await Assert.That(ShareMetricValue(metrics, "records.per.request.max")).IsEqualTo((double)index + 1);
        }

        await Assert.That(connection.ShareFetchRequests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Telemetry_CancelledBufferedPreparation_DiscardsPendingCounts()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = CreateFetchResponse(0, 42, recordCount: 4)
        };
        var preparer = new BufferedTelemetryPreparer(blockAfter: 3);
        await using var fixture = CreateFixture(connection, maxPollRecords: 2, valueDeserializer: preparer);
        var metrics = EnableShareTelemetry(fixture.Consumer);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");

        await using (var first = fixture.Consumer.PollAsync().GetAsyncEnumerator())
        {
            await Assert.That(await first.MoveNextAsync()).IsTrue();
            await Assert.That(first.Current.Offset).IsEqualTo(42L);
            await Assert.That(await first.MoveNextAsync()).IsTrue();
            await Assert.That(first.Current.Offset).IsEqualTo(43L);
        }
        await Assert.That(ShareMetricValue(metrics, "records.consumed.total")).IsEqualTo(2d);

        using var cancellation = new CancellationTokenSource();
        await using (var cancelled = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator())
        {
            var pending = cancelled.MoveNextAsync().AsTask();
            await preparer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.That(async () => await pending).Throws<OperationCanceledException>();
        }
        await Assert.That(ShareMetricValue(metrics, "records.consumed.total")).IsEqualTo(2d);

        preparer.Block = false;
        await using (var retry = fixture.Consumer.PollAsync().GetAsyncEnumerator())
        {
            await Assert.That(await retry.MoveNextAsync()).IsTrue();
            await Assert.That(retry.Current.Offset).IsEqualTo(44L);
            await Assert.That(await retry.MoveNextAsync()).IsTrue();
            await Assert.That(retry.Current.Offset).IsEqualTo(45L);
        }
        await Assert.That(ShareMetricValue(metrics, "records.consumed.total")).IsEqualTo(4d);
        await Assert.That(ShareMetricValue(metrics, "bytes.consumed.total")).IsEqualTo(64d);
        await Assert.That(ShareMetricValue(metrics, "records.per.request.max")).IsEqualTo(4d);
        await Assert.That(connection.ShareFetchRequests.Count).IsEqualTo(1);
    }

    private sealed class BufferedTelemetryPreparer(int blockAfter = int.MaxValue)
        : IDeserializer<string>, IAsyncDeserializerPreparer<string>
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Block { get; set; } = true;

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            _calls++;
            return Serializers.String.Deserialize(data, context);
        }

        public bool TryDeserialize(ReadOnlyMemory<byte> data, SerializationContext context, out string value)
        {
            if (Block && _calls == blockAfter)
            {
                value = string.Empty;
                return false;
            }
            value = Deserialize(data, context);
            return true;
        }

        public ValueTask PrepareAsync(ReadOnlyMemory<byte> data, SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            return new ValueTask(_release.Task.WaitAsync(cancellationToken));
        }
    }
}
