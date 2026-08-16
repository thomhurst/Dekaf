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
