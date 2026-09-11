using Dekaf.Admin;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Integration;

[Category("ShareConsumer")]
[SupportsKafka(420)]
[NotInParallel("ShareConsumerKafka42")]
public class ShareConsumerCloseTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Dispose_DuringPreparation_StopsDeliveryAndReleasesAcquisitions()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var group = $"share-dispose-preparation-{Guid.NewGuid():N}";
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.Group, Name = group }] =
                [ConfigAlter.Set("share.auto.offset.reset", "earliest")]
        });
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 2);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var preparer = new PausedPreparer();
        await using var first = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithValueDeserializer(preparer).BuildAsync();
        first.Subscribe(topic);
        await using (var poll = first.PollAsync(timeout.Token).GetAsyncEnumerator())
        {
            var pending = poll.MoveNextAsync().AsTask();
            bool delivered;
            try
            {
                await preparer.Entered.Task.WaitAsync(timeout.Token);
                await first.DisposeAsync();
            }
            finally
            {
                preparer.Release.TrySetResult();
                delivered = await pending.WaitAsync(timeout.Token);
            }
            await Assert.That(delivered).IsFalse();
        }

        await using var second = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group).BuildAsync();
        second.Subscribe(topic);
        var values = new List<string?>();
        await foreach (var record in second.PollAsync(timeout.Token))
        {
            values.Add(record.Value);
            second.Acknowledge(record);
            if (values.Count == 2)
                break;
        }
        string?[] expected = ["value-0", "value-1"];
        await Assert.That(values).IsEquivalentTo(expected);
        await second.CommitAsync(timeout.Token);
    }

    private sealed class PausedPreparer : IDeserializer<string>, IAsyncDeserializerPreparer<string>
    {
        private bool _prepared;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            Serializers.String.Deserialize(data, context);

        public bool TryDeserialize(ReadOnlyMemory<byte> data, SerializationContext context, out string value)
        {
            value = _prepared ? Deserialize(data, context) : string.Empty;
            return _prepared;
        }

        public async ValueTask PrepareAsync(ReadOnlyMemory<byte> data, SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            _prepared = true;
        }
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    public async Task Close_PartialPoll_RedeliversOnlyUnacknowledgedRecords(bool explicitAccept, bool processingThrows)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var group = $"share-close-{Guid.NewGuid():N}";
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.Group, Name = group }] =
                [ConfigAlter.Set("share.auto.offset.reset", "earliest")]
        });
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 3);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        string? firstValue = null;
        try
        {
            await using var first = await Kafka.CreateShareConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group).BuildAsync();
            first.Subscribe(topic);

            await foreach (var record in first.PollAsync(timeout.Token))
            {
                firstValue = record.Value;
                if (explicitAccept)
                    first.Acknowledge(record);
                if (processingThrows)
                    throw new ProcessingException();
                break;
            }
        }
        catch (ProcessingException) when (processingThrows)
        {
            // Await-using cleanup must not convert this failed delivery into Accept.
        }

        await Assert.That(firstValue).IsNotNull();
        await using var second = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group).BuildAsync();
        second.Subscribe(topic);
        var values = new List<string?>();
        await foreach (var record in second.PollAsync(timeout.Token))
        {
            values.Add(record.Value);
            second.Acknowledge(record);
            if (values.Count == (explicitAccept ? 2 : 3))
                break;
        }

        string?[] producedValues = ["value-0", "value-1", "value-2"];
        var expected = explicitAccept ? producedValues.Where(value => value != firstValue) : producedValues;
        await Assert.That(values).IsEquivalentTo(expected);
        await second.CommitAsync(timeout.Token);
    }

    private sealed class ProcessingException : Exception;
}
