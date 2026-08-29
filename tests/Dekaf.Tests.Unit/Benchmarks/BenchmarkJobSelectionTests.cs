using Dekaf.Benchmarks.Infrastructure;

namespace Dekaf.Tests.Unit.Benchmarks;

public sealed class BenchmarkJobSelectionTests
{
    [Test]
    public async Task GetExplicitJob_SeparatedOptions_ResolvesJob()
    {
        await Assert.That(BenchmarkJobSelection.GetExplicitJob(["--filter", "*Producer*", "--job", "dry"]))
            .IsEqualTo(BenchmarkJob.Dry);
        await Assert.That(BenchmarkJobSelection.GetExplicitJob(["-j", "Short"]))
            .IsEqualTo(BenchmarkJob.Short);
    }

    [Test]
    public async Task GetExplicitJob_EqualsOptions_ResolvesJob()
    {
        await Assert.That(BenchmarkJobSelection.GetExplicitJob(["--job=Medium"]))
            .IsEqualTo(BenchmarkJob.Medium);
        await Assert.That(BenchmarkJobSelection.GetExplicitJob(["-j=Long"]))
            .IsEqualTo(BenchmarkJob.Long);
        await Assert.That(BenchmarkJobSelection.GetExplicitJob(["--job=Default"]))
            .IsEqualTo(BenchmarkJob.Default);
    }

    [Test]
    public async Task GetExplicitJob_MissingOrInvalidValue_ReturnsNull()
    {
        await Assert.That(BenchmarkJobSelection.GetExplicitJob(["--filter", "*Producer*"]))
            .IsNull();
        await Assert.That(BenchmarkJobSelection.GetExplicitJob(["--job"]))
            .IsNull();
        await Assert.That(BenchmarkJobSelection.GetExplicitJob(["--job=Unknown"]))
            .IsNull();
    }
}
