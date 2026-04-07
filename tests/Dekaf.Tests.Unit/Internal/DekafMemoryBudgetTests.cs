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
}
