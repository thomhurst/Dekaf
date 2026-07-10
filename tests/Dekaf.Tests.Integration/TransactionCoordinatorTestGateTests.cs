namespace Dekaf.Tests.Integration;

[Category("Transaction")]
public sealed class TransactionCoordinatorTestGateTests
{
    [Test]
    public async Task Gate_IsLimitedAndScopedPerKafkaFixture()
    {
        await using var firstFixture = new StubKafkaTestContainer("first");
        await using var secondFixture = new StubKafkaTestContainer("second");

        var firstGate = TransactionCoordinatorTestGate.Get(firstFixture);
        var sameFixtureGate = TransactionCoordinatorTestGate.Get(firstFixture);
        var secondFixtureGate = TransactionCoordinatorTestGate.Get(secondFixture);

        await Assert.That(firstGate.CurrentCount)
            .IsEqualTo(TransactionCoordinatorTestGate.MaximumConcurrencyPerFixture);
        await Assert.That(ReferenceEquals(firstGate, sameFixtureGate)).IsTrue();
        await Assert.That(ReferenceEquals(firstGate, secondFixtureGate)).IsFalse();
    }

    private sealed class StubKafkaTestContainer(string name) : KafkaTestContainer
    {
        private static readonly Version s_version = new(0, 0, 0);

        public override string ContainerName => name;
        public override Version Version => s_version;
    }
}
