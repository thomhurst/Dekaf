using Dekaf.Consumer.DeadLetter;

namespace Dekaf.Tests.Unit.Consumer.DeadLetter;

public class DeadLetterQueueBuilderTests
{
    [Test]
    public async Task Build_WithDefaults_ReturnsDefaultOptions()
    {
        var builder = new DeadLetterQueueBuilder();
        var options = builder.Build();

        await Assert.That(options.TopicSuffix).IsEqualTo(".DLQ");
        await Assert.That(options.MaxFailures).IsEqualTo(1);
        await Assert.That(options.IncludeExceptionInHeaders).IsTrue();
        await Assert.That(options.AwaitDelivery).IsFalse();
    }

    [Test]
    public async Task Build_WithCustomSuffix_ReturnsCustomSuffix()
    {
        var options = new DeadLetterQueueBuilder()
            .WithTopicSuffix("-dead")
            .Build();

        await Assert.That(options.TopicSuffix).IsEqualTo("-dead");
    }

    [Test]
    public async Task Build_WithMaxFailures_ReturnsConfiguredValue()
    {
        var options = new DeadLetterQueueBuilder()
            .WithMaxFailures(5)
            .Build();

        await Assert.That(options.MaxFailures).IsEqualTo(5);
    }

    [Test]
    public async Task Build_WithAwaitDelivery_SetsFlag()
    {
        var options = new DeadLetterQueueBuilder()
            .AwaitDelivery()
            .Build();

        await Assert.That(options.AwaitDelivery).IsTrue();
    }

    [Test]
    public async Task Build_ExcludeExceptionInHeaders_ClearsFlag()
    {
        var options = new DeadLetterQueueBuilder()
            .ExcludeExceptionFromHeaders()
            .Build();

        await Assert.That(options.IncludeExceptionInHeaders).IsFalse();
    }

    [Test]
    public async Task FluentChaining_AllMethodsReturnBuilder()
    {
        var builder = new DeadLetterQueueBuilder();

        var result = builder
            .WithTopicSuffix(".DLQ")
            .WithMaxFailures(3)
            .AwaitDelivery()
            .ExcludeExceptionFromHeaders();

        await Assert.That(result).IsEqualTo(builder);
    }
}
