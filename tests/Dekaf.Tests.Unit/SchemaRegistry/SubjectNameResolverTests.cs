using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SubjectNameResolverTests
{
    [Test]
    public async Task GetTopicSubjectName_EqualTopicInstancesReuseSubjects()
    {
        var firstTopic = new string("orders".AsSpan());
        var secondTopic = new string("orders".AsSpan());

        var firstKey = SubjectNameResolver.GetTopicSubjectName(firstTopic, isKey: true);
        var secondKey = SubjectNameResolver.GetTopicSubjectName(secondTopic, isKey: true);
        var firstValue = SubjectNameResolver.GetTopicSubjectName(firstTopic, isKey: false);
        var secondValue = SubjectNameResolver.GetTopicSubjectName(secondTopic, isKey: false);

        await Assert.That(firstTopic).IsNotSameReferenceAs(secondTopic);
        await Assert.That(firstKey).IsSameReferenceAs(secondKey);
        await Assert.That(firstValue).IsSameReferenceAs(secondValue);
        await Assert.That(firstKey).IsEqualTo("orders-key");
        await Assert.That(firstValue).IsEqualTo("orders-value");
    }

    [Test]
    public async Task DeserializerSubjectNameCache_CustomStrategyReusesEqualTopicValues()
    {
        var strategy = new CountingRecordSubjectNameStrategy();
        var cache = DeserializerSubjectNameCache.Create(
            new SchemaRegistryDeserializerConfig
            {
                CustomSubjectNameStrategy = strategy
            })!;
        var schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = """{ "type": "object", "title": "Order" }"""
        };
        var firstTopic = new string("orders".AsSpan());
        var secondTopic = new string("orders".AsSpan());

        var first = cache.GetSubjectName(1, schema, firstTopic, isKey: false, "Fallback");
        var second = cache.GetSubjectName(1, schema, secondTopic, isKey: false, "Fallback");

        await Assert.That(first).IsSameReferenceAs(second);
        await Assert.That(first).IsEqualTo("orders-Order");
        await Assert.That(strategy.CallCount).IsEqualTo(1);
    }

    [Test]
    public async Task SubjectCaches_StableTopicInstancesSurviveContentCacheEviction()
    {
        const int topicCount = 2048;
        var topics = new string[topicCount];
        var strategy = new CountingRecordSubjectNameStrategy();
        var cache = DeserializerSubjectNameCache.Create(
            new SchemaRegistryDeserializerConfig
            {
                CustomSubjectNameStrategy = strategy
            })!;
        var schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = """{ "type": "object", "title": "Order" }"""
        };

        topics[0] = "orders-0";
        var firstTopicSubject = SubjectNameResolver.GetTopicSubjectName(topics[0], isKey: false);
        var firstConfiguredSubject = cache.GetSubjectName(1, schema, topics[0], isKey: false, "Fallback");

        for (var i = 1; i < topics.Length; i++)
        {
            var topic = $"orders-{i}";
            topics[i] = topic;
            SubjectNameResolver.GetTopicSubjectName(topic, isKey: false);
            cache.GetSubjectName(1, schema, topic, isKey: false, "Fallback");
        }

        var topicSubject = SubjectNameResolver.GetTopicSubjectName(topics[0], isKey: false);
        var configuredSubject = cache.GetSubjectName(1, schema, topics[0], isKey: false, "Fallback");

        await Assert.That(topicSubject).IsSameReferenceAs(firstTopicSubject);
        await Assert.That(configuredSubject).IsSameReferenceAs(firstConfiguredSubject);
        await Assert.That(topicSubject).IsEqualTo("orders-0-value");
        await Assert.That(configuredSubject).IsEqualTo("orders-0-Order");
        await Assert.That(strategy.CallCount).IsEqualTo(topicCount);
    }

    [Test]
    public async Task DeserializerSubjectNameCache_OverflowRetainsIdentityEntries()
    {
        const int schemaCount = 1025;
        var strategy = new CountingRecordSubjectNameStrategy();
        var cache = DeserializerSubjectNameCache.Create(
            new SchemaRegistryDeserializerConfig
            {
                CustomSubjectNameStrategy = strategy
            })!;
        var schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = """{ "type": "object", "title": "Order" }"""
        };
        const string topic = "orders";

        for (var schemaId = 0; schemaId < schemaCount; schemaId++)
            cache.GetSubjectName(schemaId, schema, topic, isKey: false, "Fallback");

        cache.GetSubjectName(0, schema, topic, isKey: false, "Fallback");

        await Assert.That(strategy.CallCount).IsEqualTo(schemaCount);
    }

    private sealed class CountingRecordSubjectNameStrategy : ISubjectNameStrategy
    {
        public int CallCount { get; private set; }

        public string GetSubjectName(string topic, string? recordType, bool isKey)
        {
            CallCount++;
            return $"{topic}-{recordType}";
        }
    }
}
