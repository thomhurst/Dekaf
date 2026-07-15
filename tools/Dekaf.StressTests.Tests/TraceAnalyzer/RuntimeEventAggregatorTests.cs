using Dekaf.TraceAnalyzer;

namespace Dekaf.StressTests.Tests.TraceAnalyzer;

public class RuntimeEventAggregatorTests
{
    [Test]
    public async Task Allocations_AreRankedBySampledBytes()
    {
        var aggregator = new RuntimeEventAggregator();
        aggregator.AddAllocationTick("System.Byte[]", 100_000, isLargeObject: true);
        aggregator.AddAllocationTick("System.String", 300_000, isLargeObject: false);
        aggregator.AddAllocationTick("System.String", 300_000, isLargeObject: false);

        var markdown = aggregator.RenderMarkdown();

        var stringIndex = markdown.IndexOf("System.String", StringComparison.Ordinal);
        var byteIndex = markdown.IndexOf("System.Byte[]", StringComparison.Ordinal);
        await Assert.That(stringIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(stringIndex).IsLessThan(byteIndex);
        await Assert.That(markdown).Contains("585.94 KB");
        await Assert.That(markdown).Contains("| 1 |");
    }

    [Test]
    public async Task Allocations_UnknownTypeNameFallsBackToPlaceholder()
    {
        var aggregator = new RuntimeEventAggregator();
        aggregator.AddAllocationTick("", 1024, isLargeObject: false);

        await Assert.That(aggregator.RenderMarkdown()).Contains("(unknown)");
    }

    [Test]
    public async Task Contention_ReportsTotalsMaxAndRate()
    {
        var aggregator = new RuntimeEventAggregator();
        aggregator.SetTraceDuration(10);
        aggregator.AddContention(1.5);
        aggregator.AddContention(4.5);

        var markdown = aggregator.RenderMarkdown();

        await Assert.That(markdown).Contains("2 events (0.2/s)");
        await Assert.That(markdown).Contains("total wait 6.0ms");
        await Assert.That(markdown).Contains("max 4.50ms");
    }

    [Test]
    public async Task Gc_ReportsGenerationReasonAndPauseStats()
    {
        var aggregator = new RuntimeEventAggregator();
        aggregator.AddGcStart(0, "AllocSmall");
        aggregator.AddGcStart(2, "Induced");
        aggregator.AddGcPause(3.0);
        aggregator.AddGcPause(7.0);

        var markdown = aggregator.RenderMarkdown();

        await Assert.That(markdown).Contains("Gen0: 1");
        await Assert.That(markdown).Contains("Gen2: 1");
        await Assert.That(markdown).Contains("Reason AllocSmall: 1");
        await Assert.That(markdown).Contains("Pauses: 2, total 10.0ms, max 7.00ms");
    }

    [Test]
    public async Task ThreadPoolAndExceptions_ReportCountsByReasonAndType()
    {
        var aggregator = new RuntimeEventAggregator();
        aggregator.AddThreadPoolAdjustment("Starvation");
        aggregator.AddThreadPoolAdjustment("Starvation");
        aggregator.AddException("System.TimeoutException");

        var markdown = aggregator.RenderMarkdown();

        await Assert.That(markdown).Contains("Starvation: 2");
        await Assert.That(markdown).Contains("`System.TimeoutException`: 1");
    }

    [Test]
    public async Task EmptyTrace_RendersExplicitNoDataNotes()
    {
        var markdown = new RuntimeEventAggregator().RenderMarkdown();

        await Assert.That(markdown).Contains("No allocation tick events");
        await Assert.That(markdown).Contains("No contention events");
        await Assert.That(markdown).Contains("No GC events");
        await Assert.That(markdown).Contains("No thread-pool adjustment events");
        await Assert.That(markdown).Contains("No exception events");
    }
}
