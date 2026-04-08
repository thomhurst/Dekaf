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

    // Budgets in the split tests are kept at or below the producer ceiling (256 MiB) so the
    // ceiling clamp is a no-op. Auto-tuning must not grow a single instance above the historical
    // per-instance default — see CeilingAppliedWhenBudgetExceedsCap below for the clamp test.
    private const ulong TestBudget = 256UL * 1024 * 1024; // exactly the producer ceiling

    [Test]
    public async Task SingleProducer_GetsFullProducerShare()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(TestBudget);

        var p = new FakeInstance();
        DekafMemoryBudget.RegisterProducer(p);

        await Assert.That(p.CurrentLimit).IsEqualTo(TestBudget);

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

        await Assert.That(p.CurrentLimit).IsEqualTo((ulong)(TestBudget * 0.75));
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

        await Assert.That(p1.CurrentLimit).IsEqualTo(TestBudget / 2);
        await Assert.That(p2.CurrentLimit).IsEqualTo(TestBudget / 2);

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
        await Assert.That(p1.CurrentLimit).IsEqualTo(TestBudget);

        var p2 = new FakeInstance();
        DekafMemoryBudget.RegisterProducer(p2);

        await Assert.That(p1.CurrentLimit).IsEqualTo(TestBudget / 2);
        await Assert.That(p2.CurrentLimit).IsEqualTo(TestBudget / 2);

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

        await Assert.That(p1.CurrentLimit).IsEqualTo(TestBudget);

        DekafMemoryBudget.UnregisterProducer(p1);
    }

    [Test]
    public async Task ExplicitOverride_SubtractedFromBudget_BeforeAutoSplit()
    {
        DekafMemoryBudget.ResetForTesting();
        // Budget 320 MiB, reserve 100 MiB → 220 MiB to auto, below the 256 MiB ceiling so we
        // actually observe the subtraction rather than the clamp.
        DekafMemoryBudget.SetBudget(320UL * 1024 * 1024);

        DekafMemoryBudget.ReserveExplicit(100UL * 1024 * 1024);

        var p = new FakeInstance();
        DekafMemoryBudget.RegisterProducer(p);

        await Assert.That(p.CurrentLimit).IsEqualTo(220UL * 1024 * 1024);

        DekafMemoryBudget.UnregisterProducer(p);
        DekafMemoryBudget.ReleaseExplicit(100UL * 1024 * 1024);
    }

    [Test]
    public async Task CeilingAppliedWhenBudgetExceedsCap()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(16UL * 1024 * 1024 * 1024); // 16 GiB — simulates a large host

        var p = new FakeInstance();
        var c = new FakeInstance();
        DekafMemoryBudget.RegisterProducer(p);
        DekafMemoryBudget.RegisterConsumer(c);

        // Producers clamped to 256 MiB, consumers clamped to 64 MiB regardless of how much
        // headroom the global budget has. This is the regression guard for the stress-test
        // blow-up that auto-tuned a single producer to multi-GB BufferMemory.
        await Assert.That(p.CurrentLimit).IsEqualTo(256UL * 1024 * 1024);
        await Assert.That(c.CurrentLimit).IsEqualTo(64UL * 1024 * 1024);

        DekafMemoryBudget.UnregisterProducer(p);
        DekafMemoryBudget.UnregisterConsumer(c);
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
