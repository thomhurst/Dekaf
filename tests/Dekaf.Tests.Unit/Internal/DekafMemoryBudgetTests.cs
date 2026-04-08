namespace Dekaf.Tests.Unit.Internal;

using Dekaf.Internal;

[NotInParallel("DekafMemoryBudget")]
public class DekafMemoryBudgetTests
{
    [Test]
    public async Task TotalBudget_DefaultsTo40PercentOfAvailableMemory()
    {
        DekafMemoryBudget.ResetForTesting();

        var available = (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var expected = (ulong)(available * 0.40);

        await Assert.That(DekafMemoryBudget.TotalBudget).IsEqualTo(expected);
    }

    [Test]
    public async Task SetBudget_WithExplicitBytes_OverridesDefault()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(1_073_741_824UL); // 1 GB

        await Assert.That(DekafMemoryBudget.TotalBudget).IsEqualTo(1_073_741_824UL);
    }

    [Test]
    public async Task SetBudget_WithPercent_ComputesFromAvailableMemory()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(percentOfAvailable: 0.20);

        var available = (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var expected = (ulong)(available * 0.20);

        await Assert.That(DekafMemoryBudget.TotalBudget).IsEqualTo(expected);
    }

    [Test]
    public async Task SetBudget_WithInvalidPercent_Throws()
    {
        DekafMemoryBudget.ResetForTesting();

        await Assert.That(() => DekafMemoryBudget.SetBudget(percentOfAvailable: 0.0))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => DekafMemoryBudget.SetBudget(percentOfAvailable: 1.5))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task SetBudget_WithPercent_ClearsPriorExplicitBudget()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(1_073_741_824UL); // 1 GB explicit
        DekafMemoryBudget.SetBudget(percentOfAvailable: 0.10);

        var available = (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var expected = (ulong)(available * 0.10);

        await Assert.That(DekafMemoryBudget.TotalBudget).IsEqualTo(expected);
    }

    [Test]
    public async Task SetBudget_WithZeroBytes_Throws()
    {
        DekafMemoryBudget.ResetForTesting();
        await Assert.That(() => DekafMemoryBudget.SetBudget(0UL))
            .Throws<ArgumentOutOfRangeException>();
    }

    private sealed class FakeInstance : IBudgetedInstance
    {
        public ulong CurrentLimit { get; private set; }
        public int RebalanceCount { get; private set; }

        public void OnBudgetChanged(ulong newLimit)
        {
            CurrentLimit = newLimit;
            RebalanceCount++;
        }
    }

    // BufferMemory is scaled down by a 4x overhead divisor for producers (see
    // DekafMemoryBudget.ProducerOverheadDivisor) to account for the amplification between
    // auto-tuned BufferMemory and actual resident producer memory. Consumer limits have no
    // divisor. Budgets below must be divisible by 4 so assertions are exact.
    private const ulong TestBudget = 1024UL * 1024 * 1024; // 1 GiB
    private const int ProducerDivisor = 4;

    [Test]
    public async Task SingleProducer_GetsFullProducerShare()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(TestBudget);

        var p = new FakeInstance();
        DekafMemoryBudget.RegisterProducer(p);

        await Assert.That(p.CurrentLimit).IsEqualTo(TestBudget / ProducerDivisor);

        DekafMemoryBudget.UnregisterProducer(p);
    }

    [Test]
    public async Task ProducerAndConsumer_SplitSeventyFiveTwentyFive()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(TestBudget);

        var p = new FakeInstance();
        var c = new FakeInstance();
        DekafMemoryBudget.RegisterProducer(p);
        DekafMemoryBudget.RegisterConsumer(c);

        await Assert.That(p.CurrentLimit).IsEqualTo((ulong)(TestBudget * 0.75) / ProducerDivisor);
        await Assert.That(c.CurrentLimit).IsEqualTo((ulong)(TestBudget * 0.25));

        DekafMemoryBudget.UnregisterProducer(p);
        DekafMemoryBudget.UnregisterConsumer(c);
    }

    [Test]
    public async Task TwoProducers_SplitProducerShareEqually()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(TestBudget);

        var p1 = new FakeInstance();
        var p2 = new FakeInstance();
        DekafMemoryBudget.RegisterProducer(p1);
        DekafMemoryBudget.RegisterProducer(p2);

        await Assert.That(p1.CurrentLimit).IsEqualTo(TestBudget / 2 / ProducerDivisor);
        await Assert.That(p2.CurrentLimit).IsEqualTo(TestBudget / 2 / ProducerDivisor);

        DekafMemoryBudget.UnregisterProducer(p1);
        DekafMemoryBudget.UnregisterProducer(p2);
    }

    [Test]
    public async Task RegisterTriggersRebalanceOnExistingInstances()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(TestBudget);

        var p1 = new FakeInstance();
        DekafMemoryBudget.RegisterProducer(p1);
        await Assert.That(p1.CurrentLimit).IsEqualTo(TestBudget / ProducerDivisor);

        var p2 = new FakeInstance();
        DekafMemoryBudget.RegisterProducer(p2);

        await Assert.That(p1.CurrentLimit).IsEqualTo(TestBudget / 2 / ProducerDivisor);
        await Assert.That(p2.CurrentLimit).IsEqualTo(TestBudget / 2 / ProducerDivisor);

        DekafMemoryBudget.UnregisterProducer(p1);
        DekafMemoryBudget.UnregisterProducer(p2);
    }

    [Test]
    public async Task UnregisterTriggersRebalanceOnRemainingInstances()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(TestBudget);

        var p1 = new FakeInstance();
        var p2 = new FakeInstance();
        DekafMemoryBudget.RegisterProducer(p1);
        DekafMemoryBudget.RegisterProducer(p2);

        DekafMemoryBudget.UnregisterProducer(p2);

        await Assert.That(p1.CurrentLimit).IsEqualTo(TestBudget / ProducerDivisor);

        DekafMemoryBudget.UnregisterProducer(p1);
    }

    [Test]
    public async Task ExplicitOverride_SubtractedFromBudget_BeforeAutoSplit()
    {
        DekafMemoryBudget.ResetForTesting();
        // Budget 320 MiB, reserve 100 MiB → 220 MiB to auto. Producer gets 220 / 4 = 55 MiB.
        DekafMemoryBudget.SetBudget(320UL * 1024 * 1024);

        DekafMemoryBudget.ReserveExplicit(100UL * 1024 * 1024);

        var p = new FakeInstance();
        DekafMemoryBudget.RegisterProducer(p);

        await Assert.That(p.CurrentLimit).IsEqualTo(55UL * 1024 * 1024);

        DekafMemoryBudget.UnregisterProducer(p);
        DekafMemoryBudget.ReleaseExplicit(100UL * 1024 * 1024);
    }

    [Test]
    public async Task MinimumFloor_AppliedWhenBudgetTooSmall()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(1_000_000UL);

        var p = new FakeInstance();
        DekafMemoryBudget.RegisterProducer(p);

        await Assert.That(p.CurrentLimit).IsEqualTo(32UL * 1024 * 1024);

        DekafMemoryBudget.UnregisterProducer(p);
    }
}
