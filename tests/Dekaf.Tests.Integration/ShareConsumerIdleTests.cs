using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Integration;

[Category("ShareConsumer")]
[SupportsKafka(420)]
[NotInParallel("ShareConsumerKafka42")]
public sealed class ShareConsumerIdleTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task TopicRevocation_UnsubscribeWakesIdlePoll(bool batch)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"share-idle-{Guid.NewGuid():N}")
            .WithFetchMaxWaitMs(10_000)
            .BuildAsync();
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        consumer.Subscribe(topic);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(75));
        await using var records = consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        await using var batches = consumer.PollBatchesAsync(cancellation.Token).GetAsyncEnumerator();
        var pending = (batch ? batches.MoveNextAsync() : records.MoveNextAsync()).AsTask();
        try
        {
            // Keep the first fetch active: cancelling a priming request would change
            // the session epoch and would not isolate assignment waiting.
            await WaitForConditionAsync(
                () => consumer.Assignment.Count == 1 && consumer.AcquisitionLockTimeoutMs > 0,
                TimeSpan.FromSeconds(30), description: "initial share assignment and successful fetch");
            await admin.DeleteTopicsAsync([topic], cancellationToken: cancellation.Token);
            await WaitForConditionAsync(() => consumer.Assignment.Count == 0,
                TimeSpan.FromSeconds(30), description: "broker revokes the deleted topic");
            await Assert.That(pending.IsCompleted).IsFalse();
            consumer.Unsubscribe();
            await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(3))).IsFalse();
            await consumer.CloseAsync(cancellation.Token);
        }
        finally
        {
            await cancellation.CancelAsync();
            try { await pending; }
            catch (OperationCanceledException) { }
        }
    }
}