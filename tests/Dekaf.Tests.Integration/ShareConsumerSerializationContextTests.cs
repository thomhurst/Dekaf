using Dekaf.Admin;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Integration;

[Category("ShareConsumer")]
[SupportsKafka(420)]
[NotInParallel("ShareConsumerKafka42")]
public class ShareConsumerSerializationContextTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task BufferedNestedConsumer_PreservesOuterValueTopic(bool prepared)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var nestedTopic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var outerTopic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var nestedGroup = $"nested-context-{Guid.NewGuid():N}";
        var outerGroup = $"outer-context-{Guid.NewGuid():N}";
        await using (var admin = Kafka.CreateAdminClient().WithBootstrapServers(KafkaContainer.BootstrapServers).Build())
        {
            await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
            {
                [new ConfigResource { Type = ConfigResourceType.Group, Name = nestedGroup }] = [ConfigAlter.Set("share.auto.offset.reset", "earliest")],
                [new ConfigResource { Type = ConfigResourceType.Group, Name = outerGroup }] = [ConfigAlter.Set("share.auto.offset.reset", "earliest")]
            }, cancellationToken: timeout.Token);
        }
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithLinger(TimeSpan.FromMinutes(1)).BuildAsync(timeout.Token);
        await producer.FireAsync(nestedTopic, "nested-key-0", "nested-0");
        await producer.FireAsync(nestedTopic, "nested-key-1", "nested-1");
        await producer.FireAsync(outerTopic, "outer-key", "outer-value");
        await producer.FlushAsync(timeout.Token);

        var nestedValue = new TopicCapturingDeserializer(prepared);
        await using var nested = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(nestedGroup)
            .WithValueDeserializer(nestedValue).WithMaxPollRecords(1)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit).BuildAsync(timeout.Token);
        nested.Subscribe(nestedTopic);
        await using (var first = nested.PollAsync(timeout.Token).GetAsyncEnumerator())
        {
            await Assert.That(await first.MoveNextAsync()).IsTrue();
            await Assert.That(first.Current.Value).IsEqualTo("nested-0");
            nested.Acknowledge(first.Current);
            await nested.CommitAsync(timeout.Token);
        }

        // The remaining record is acquired in the same producer batch but not yet parsed.
        // Reading it in the outer key callback reenters the same generic parser on this thread.
        await using var nestedPoll = nested.PollAsync(timeout.Token).GetAsyncEnumerator();
        Task<bool>? unexpectedPendingMove = null;
        var outerKey = new CallbackDeserializer(() =>
        {
            var move = nestedPoll.MoveNextAsync();
            if (!move.IsCompletedSuccessfully)
            {
                unexpectedPendingMove = move.AsTask();
                throw new InvalidOperationException("The acquired nested record must be available synchronously.");
            }
            if (!move.GetAwaiter().GetResult()) throw new InvalidOperationException("Missing acquired nested record.");
        });
        var outerValue = new TopicCapturingDeserializer(prepared);
        await using var outer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(outerGroup)
            .WithKeyDeserializer(outerKey).WithValueDeserializer(outerValue).WithMaxPollRecords(1)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit).BuildAsync(timeout.Token);
        outer.Subscribe(outerTopic);
        try
        {
            await using var outerPoll = outer.PollAsync(timeout.Token).GetAsyncEnumerator();
            await Assert.That(await outerPoll.MoveNextAsync()).IsTrue();
            await Assert.That(nestedPoll.Current.Value).IsEqualTo("nested-1");
            await Assert.That(nestedValue.Topic).IsEqualTo(nestedTopic);
            await Assert.That(outerValue.Topic).IsEqualTo(outerTopic);
            outer.Acknowledge(outerPoll.Current);
            nested.Acknowledge(nestedPoll.Current);
            await outer.CommitAsync(timeout.Token);
            await nested.CommitAsync(timeout.Token);
        }
        finally
        {
            if (unexpectedPendingMove is not null)
            {
                await timeout.CancelAsync();
                try { await unexpectedPendingMove; }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }
            }
        }
    }

    private sealed class CallbackDeserializer(Action callback) : IDeserializer<string>
    {
        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            callback();
            return Serializers.String.Deserialize(data, context);
        }
    }

    private sealed class TopicCapturingDeserializer(bool prepared) : IDeserializer<string>,
        IAsyncDeserializerPreparer<string>, IAsyncDeserializerPreparationRequirement
    {
        public bool RequiresPreparation => prepared;
        internal string? Topic { get; private set; }
        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            Topic = context.Topic;
            return Serializers.String.Deserialize(data, context);
        }
        public bool TryDeserialize(ReadOnlyMemory<byte> data, SerializationContext context, out string value)
        {
            value = Deserialize(data, context);
            return true;
        }
        public ValueTask PrepareAsync(ReadOnlyMemory<byte> data, SerializationContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The test deserializer is warm.");
    }
}
