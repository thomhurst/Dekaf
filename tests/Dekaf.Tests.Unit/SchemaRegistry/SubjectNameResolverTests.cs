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
}
