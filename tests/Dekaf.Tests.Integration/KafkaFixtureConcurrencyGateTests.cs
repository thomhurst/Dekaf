namespace Dekaf.Tests.Integration;

[Category("Infrastructure")]
public sealed class KafkaFixtureConcurrencyGateTests
{
    [Test]
    public async Task Gate_IsBoundedAndScopedPerKafkaFixture()
    {
        await using var firstFixture = new StubKafkaTestContainer("first");
        await using var secondFixture = new StubKafkaTestContainer("second");

        var firstGate = KafkaFixtureConcurrencyGate.Get(firstFixture);
        var sameFixtureGate = KafkaFixtureConcurrencyGate.Get(firstFixture);
        var secondFixtureGate = KafkaFixtureConcurrencyGate.Get(secondFixture);

        await Assert.That(firstGate.CurrentCount)
            .IsEqualTo(KafkaFixtureConcurrencyGate.MaximumConcurrencyPerFixture);
        await Assert.That(ReferenceEquals(firstGate, sameFixtureGate)).IsTrue();
        await Assert.That(ReferenceEquals(firstGate, secondFixtureGate)).IsFalse();
    }

    [Test]
    public async Task Gate_BlocksAfterMaximumConcurrencyUntilPermitReleases()
    {
        await using var fixture = new StubKafkaTestContainer("bounded");
        var gate = KafkaFixtureConcurrencyGate.Get(fixture);

        for (var i = 0; i < KafkaFixtureConcurrencyGate.MaximumConcurrencyPerFixture; i++)
            await gate.WaitAsync();

        var waiting = gate.WaitAsync();
        await Assert.That(waiting.IsCompleted).IsFalse();

        gate.Release();
        await waiting.WaitAsync(TimeSpan.FromSeconds(1));

        for (var i = 0; i < KafkaFixtureConcurrencyGate.MaximumConcurrencyPerFixture; i++)
            gate.Release();
    }

    private sealed class StubKafkaTestContainer(string name) : KafkaTestContainer
    {
        public override string ContainerName => name;
        public override int Version => 0;
    }
}
