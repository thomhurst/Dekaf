using Dekaf.ShareConsumer;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public class InMemoryShareCloseTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Close_PreservesExplicitAccept_AndReleasesImplicit(bool explicitAccept)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("topic", "key", "first");
        await producer.ProduceAsync("topic", "key", "second");
        await using (var first = new InMemoryShareConsumer<string, string>(cluster))
        {
            first.Subscribe("topic");
            var record = await first.PollAsync().FirstAsync();
            if (explicitAccept)
                first.Acknowledge(record);
        }

        await using var second = new InMemoryShareConsumer<string, string>(cluster);
        second.Subscribe("topic");
        var redelivered = await second.PollAsync().FirstAsync();
        await Assert.That(redelivered.Value).IsEqualTo(explicitAccept ? "second" : "first");
    }

    [Test]
    public async Task Close_AfterFailedImplicitCommit_PreservesSubmittedAccept()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("topic", "key", "first");
        await producer.ProduceAsync("topic", "key", "second");
        await using (var first = new InMemoryShareConsumer<string, string>(cluster))
        {
            first.Subscribe("topic");
            _ = await first.PollAsync().FirstAsync();
            cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.ShareAcknowledge), new InvalidOperationException("ack failure"));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await first.CommitAsync());
        }

        await using var second = new InMemoryShareConsumer<string, string>(cluster);
        second.Subscribe("topic");
        await Assert.That((await second.PollAsync().FirstAsync()).Value).IsEqualTo("second");
    }
}
