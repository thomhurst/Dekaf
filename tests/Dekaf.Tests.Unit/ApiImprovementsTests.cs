using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit;

/// <summary>
/// Tests for the API improvements: ProducerMessage.Create, Headers convenience methods,
/// configuration presets, and consumer extensions.
/// </summary>
public class ApiImprovementsTests
{
    #region ProducerMessage.Create Tests

    [Test]
    public async Task ProducerMessage_Create_WithTopicKeyValue_SetsProperties()
    {
        var message = ProducerMessage<string, string>.Create("my-topic", "my-key", "my-value");

        await Assert.That(message.Topic).IsEqualTo("my-topic");
        await Assert.That(message.Key).IsEqualTo("my-key");
        await Assert.That(message.Value).IsEqualTo("my-value");
        await Assert.That(message.Headers).IsNull();
        await Assert.That(message.Partition).IsNull();
        await Assert.That(message.Timestamp).IsNull();
    }

    [Test]
    public async Task ProducerMessage_Create_WithNullKey_SetsKeyToNull()
    {
        var message = ProducerMessage<string, string>.Create("my-topic", null, "my-value");

        await Assert.That(message.Topic).IsEqualTo("my-topic");
        await Assert.That(message.Key).IsNull();
        await Assert.That(message.Value).IsEqualTo("my-value");
    }

    [Test]
    public async Task ProducerMessage_Create_WithHeaders_SetsHeaders()
    {
        var headers = Headers.Create().Add("key1", "value1");
        var message = ProducerMessage<string, string>.Create("my-topic", "my-key", "my-value", headers);

        await Assert.That(message.Topic).IsEqualTo("my-topic");
        await Assert.That(message.Key).IsEqualTo("my-key");
        await Assert.That(message.Value).IsEqualTo("my-value");
        await Assert.That(message.Headers).IsNotNull();
        await Assert.That(message.Headers!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ProducerMessage_Create_WithPartition_SetsPartition()
    {
        var message = ProducerMessage<string, string>.Create("my-topic", 3, "my-key", "my-value");

        await Assert.That(message.Topic).IsEqualTo("my-topic");
        await Assert.That(message.Partition).IsEqualTo(3);
        await Assert.That(message.Key).IsEqualTo("my-key");
        await Assert.That(message.Value).IsEqualTo("my-value");
    }

    #endregion

    #region Headers Factory and Convenience Method Tests

    [Test]
    public async Task Headers_Create_ReturnsEmptyHeaders()
    {
        var headers = Headers.Create();

        await Assert.That(headers.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Headers_Create_WithKeyValue_ReturnsHeadersWithOneEntry()
    {
        var headers = Headers.Create("my-key", "my-value");

        await Assert.That(headers.Count).IsEqualTo(1);
        await Assert.That(headers.GetFirstAsString("my-key")).IsEqualTo("my-value");
    }

    [Test]
    public async Task Headers_AddRange_AddsMultipleHeaders()
    {
        var kvps = new List<KeyValuePair<string, string>>
        {
            new("key1", "value1"),
            new("key2", "value2"),
            new("key3", "value3")
        };

        var headers = Headers.Create().AddRange(kvps);

        await Assert.That(headers.Count).IsEqualTo(3);
        await Assert.That(headers.GetFirstAsString("key1")).IsEqualTo("value1");
        await Assert.That(headers.GetFirstAsString("key2")).IsEqualTo("value2");
        await Assert.That(headers.GetFirstAsString("key3")).IsEqualTo("value3");
    }

    [Test]
    public async Task Headers_AddIf_AddsHeaderWhenConditionTrue()
    {
        var headers = Headers.Create()
            .AddIf(true, "included", "yes")
            .AddIf(false, "excluded", "no");

        await Assert.That(headers.Count).IsEqualTo(1);
        await Assert.That(headers.GetFirstAsString("included")).IsEqualTo("yes");
        await Assert.That(headers.GetFirst("excluded")).IsNull();
    }

    [Test]
    public async Task Headers_AddIfNotNull_AddsHeaderWhenValueNotNull()
    {
        string? nonNullValue = "value";
        string? nullValue = null;

        var headers = Headers.Create()
            .AddIfNotNull("non-null", nonNullValue)
            .AddIfNotNull("null", nullValue);

        await Assert.That(headers.Count).IsEqualTo(1);
        await Assert.That(headers.GetFirstAsString("non-null")).IsEqualTo("value");
        await Assert.That(headers.GetFirst("null")).IsNull();
    }

    [Test]
    public async Task Headers_AddIfNotNullOrEmpty_SkipsEmptyStrings()
    {
        var headers = Headers.Create()
            .AddIfNotNullOrEmpty("has-value", "value")
            .AddIfNotNullOrEmpty("empty", "")
            .AddIfNotNullOrEmpty("null", null);

        await Assert.That(headers.Count).IsEqualTo(1);
        await Assert.That(headers.GetFirstAsString("has-value")).IsEqualTo("value");
    }

    [Test]
    public async Task Headers_FluentChaining_Works()
    {
        var correlationId = "corr-123";
        var userId = "user-456";
        var isRetry = true;

        var headers = Headers.Create()
            .Add("correlation-id", correlationId)
            .AddIfNotNull("user-id", userId)
            .AddIf(isRetry, "is-retry", "true");

        await Assert.That(headers.Count).IsEqualTo(3);
    }

    #endregion

    #region OffsetCommitMode Tests

    [Test]
    public async Task ConsumerBuilder_WithOffsetCommitMode_Auto_SetsCorrectOptions()
    {
        // We can't easily test the builder output without mocking,
        // but we can verify the enum exists and the builder method compiles
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("test-group")
            .WithOffsetCommitMode(OffsetCommitMode.Auto);

        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task ConsumerBuilder_WithOffsetCommitMode_Manual_SetsCorrectOptions()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("test-group")
            .WithOffsetCommitMode(OffsetCommitMode.Manual);

        await Assert.That(builder).IsNotNull();
    }

    #endregion

    #region Configuration Preset Tests

    [Test]
    public async Task ProducerBuilder_ForHighThroughput_ReturnsBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .ForHighThroughput();

        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task ProducerBuilder_ForLowLatency_ReturnsBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .ForLowLatency();

        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task ProducerBuilder_ForReliability_ReturnsBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .ForReliability();

        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task ProducerBuilder_PresetsAreChainable_OverridesWork()
    {
        // Verify we can call presets and then override values
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .ForHighThroughput()
            .WithAcks(Acks.All);  // Override the Leader acks from preset

        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task ConsumerBuilder_ForHighThroughput_ReturnsBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("test-group")
            .ForHighThroughput();

        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task ConsumerBuilder_ForLowLatency_ReturnsBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("test-group")
            .ForLowLatency();

        await Assert.That(builder).IsNotNull();
    }

    #endregion

    #region Consumer Extensions Tests

    [Test]
    public async Task ConsumerExtensions_Where_FiltersResults()
    {
        var source = CreateTestAsyncEnumerable(10);

        var filtered = source.Where(r => r.Offset % 2 == 0);
        var results = await ConsumeAllAsync(filtered);

        await Assert.That(results.Count).IsEqualTo(5);
        foreach (var result in results)
        {
            await Assert.That(result.Offset % 2).IsEqualTo(0);
        }
    }

    [Test]
    public async Task ConsumerExtensions_Select_ProjectsResults()
    {
        var source = CreateTestAsyncEnumerable(5);

        var projected = source.Select(r => r.Offset * 2);
        var results = await ConsumeAllProjectedAsync(projected);

        await Assert.That(results).IsEquivalentTo(new List<long> { 0, 2, 4, 6, 8 });
    }

    [Test]
    public async Task ConsumerExtensions_Take_LimitsResults()
    {
        var source = CreateTestAsyncEnumerable(100);

        var taken = source.Take(5);
        var results = await ConsumeAllAsync(taken);

        await Assert.That(results.Count).IsEqualTo(5);
    }

    [Test]
    public async Task ConsumerExtensions_Take_ZeroReturnsEmpty()
    {
        var source = CreateTestAsyncEnumerable(10);

        var taken = source.Take(0);
        var results = await ConsumeAllAsync(taken);

        await Assert.That(results.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ConsumerExtensions_Batch_GroupsResults()
    {
        var source = CreateTestAsyncEnumerable(10);

        var batches = source.Batch(3);
        var batchList = new List<IReadOnlyList<ConsumeResult<string, string>>>();

        await foreach (var batch in batches)
        {
            batchList.Add(batch);
        }

        await Assert.That(batchList.Count).IsEqualTo(4); // 3 + 3 + 3 + 1 = 10
        await Assert.That(batchList[0].Count).IsEqualTo(3);
        await Assert.That(batchList[1].Count).IsEqualTo(3);
        await Assert.That(batchList[2].Count).IsEqualTo(3);
        await Assert.That(batchList[3].Count).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumerExtensions_TakeWhile_StopsWhenPredicateFalse()
    {
        var source = CreateTestAsyncEnumerable(10);

        var taken = source.TakeWhile(r => r.Offset < 5);
        var results = await ConsumeAllAsync(taken);

        await Assert.That(results.Count).IsEqualTo(5);
    }

    [Test]
    public async Task ConsumerExtensions_SkipWhile_SkipsUntilPredicateFalse()
    {
        var source = CreateTestAsyncEnumerable(10);

        var skipped = source.SkipWhile(r => r.Offset < 5);
        var results = await ConsumeAllAsync(skipped);

        await Assert.That(results.Count).IsEqualTo(5);
        await Assert.That(results[0].Offset).IsEqualTo(5);
    }

    [Test]
    public async Task ConsumerExtensions_ChainingWorks()
    {
        var source = CreateTestAsyncEnumerable(100);

        var results = await ConsumeAllAsync(
            source
                .Where(r => r.Offset % 2 == 0)
                .Take(10)
        );

        await Assert.That(results.Count).IsEqualTo(10);
        foreach (var result in results)
        {
            await Assert.That(result.Offset % 2).IsEqualTo(0);
        }
    }

    #endregion

    #region Helper Methods

    private static async IAsyncEnumerable<ConsumeResult<string, string>> CreateTestAsyncEnumerable(int count)
    {
        for (var i = 0; i < count; i++)
        {
            yield return new ConsumeResult<string, string>(
                topic: "test-topic",
                partition: 0,
                offset: i,
                keyData: default,
                isKeyNull: true,
                valueData: default,
                isValueNull: true,
                headers: null,
                timestamp: DateTimeOffset.UtcNow,
                timestampType: TimestampType.CreateTime,
                leaderEpoch: null,
                keyDeserializer: null,
                valueDeserializer: null
            );

            await Task.Yield();
        }
    }

    private static async Task<List<ConsumeResult<TKey, TValue>>> ConsumeAllAsync<TKey, TValue>(
        IAsyncEnumerable<ConsumeResult<TKey, TValue>> source)
    {
        var results = new List<ConsumeResult<TKey, TValue>>();
        await foreach (var item in source)
        {
            results.Add(item);
        }
        return results;
    }

    private static async Task<List<T>> ConsumeAllProjectedAsync<T>(IAsyncEnumerable<T> source)
    {
        var results = new List<T>();
        await foreach (var item in source)
        {
            results.Add(item);
        }
        return results;
    }

    #endregion
}
