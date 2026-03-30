using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Tests for the <see cref="AdaptiveFetchSizer"/> adaptive fetch sizing algorithm.
/// Validates growth when keeping up, shrinkage when falling behind, stable zone behavior,
/// min/max bounds, and stable window count requirements.
/// </summary>
public class AdaptiveFetchSizerTests
{
    #region Growth — consumer keeping up

    [Test]
    public async Task EvaluateAndAdjust_GrowsWhenKeepingUp_AfterStableWindow()
    {
        var sizer = CreateSizer();
        var initialPartitionBytes = sizer.CurrentPartitionFetchBytes;
        var initialFetchMaxBytes = sizer.CurrentFetchMaxBytes;

        // Send enough grow signals to hit the stable window (default 3)
        for (var i = 0; i < 3; i++)
            sizer.EvaluateAndAdjust(0.3); // Below grow threshold of 0.5

        await Assert.That(sizer.CurrentPartitionFetchBytes).IsGreaterThan(initialPartitionBytes);
        await Assert.That(sizer.CurrentFetchMaxBytes).IsGreaterThan(initialFetchMaxBytes);
    }

    [Test]
    public async Task EvaluateAndAdjust_DoesNotGrowBeforeStableWindow()
    {
        var sizer = CreateSizer();
        var initialPartitionBytes = sizer.CurrentPartitionFetchBytes;

        // Send fewer grow signals than the stable window requires
        sizer.EvaluateAndAdjust(0.3);
        sizer.EvaluateAndAdjust(0.3);

        await Assert.That(sizer.CurrentPartitionFetchBytes).IsEqualTo(initialPartitionBytes);
        await Assert.That(sizer.ConsecutiveGrowSignals).IsEqualTo(2);
    }

    [Test]
    public async Task EvaluateAndAdjust_GrowsByExpectedFactor()
    {
        var options = new AdaptiveFetchSizingOptions
        {
            MinPartitionFetchBytes = 500_000,
            InitialPartitionFetchBytes = 1_000_000,
            InitialFetchMaxBytes = 50_000_000,
            GrowthFactor = 2.0,
            StableWindowCount = 1 // Immediate growth for testing
        };
        var sizer = new AdaptiveFetchSizer(options);

        sizer.EvaluateAndAdjust(0.3);

        await Assert.That(sizer.CurrentPartitionFetchBytes).IsEqualTo(2_000_000);
        await Assert.That(sizer.CurrentFetchMaxBytes).IsEqualTo(100_000_000);
    }

    [Test]
    public async Task EvaluateAndAdjust_MultipleGrowSteps()
    {
        var options = new AdaptiveFetchSizingOptions
        {
            MinPartitionFetchBytes = 500_000,
            InitialPartitionFetchBytes = 1_000_000,
            MaxPartitionFetchBytes = 100_000_000,
            MinFetchMaxBytes = 5_000_000,
            InitialFetchMaxBytes = 10_000_000,
            MaxFetchMaxBytes = 1_000_000_000,
            GrowthFactor = 1.5,
            StableWindowCount = 1
        };
        var sizer = new AdaptiveFetchSizer(options);

        // Step 1: 1M -> 1.5M
        sizer.EvaluateAndAdjust(0.3);
        await Assert.That(sizer.CurrentPartitionFetchBytes).IsEqualTo(1_500_000);

        // Step 2: 1.5M -> 2.25M
        sizer.EvaluateAndAdjust(0.3);
        await Assert.That(sizer.CurrentPartitionFetchBytes).IsEqualTo(2_250_000);
    }

    #endregion

    #region Shrinkage — consumer falling behind

    [Test]
    public async Task EvaluateAndAdjust_ShrinksWhenFallingBehind_AfterStableWindow()
    {
        var options = new AdaptiveFetchSizingOptions
        {
            InitialPartitionFetchBytes = 4_000_000,
            InitialFetchMaxBytes = 100_000_000,
            StableWindowCount = 3
        };
        var sizer = new AdaptiveFetchSizer(options);

        // Send enough shrink signals to hit the stable window
        for (var i = 0; i < 3; i++)
            sizer.EvaluateAndAdjust(3.0); // Above shrink threshold of 2.0

        await Assert.That(sizer.CurrentPartitionFetchBytes).IsLessThan(4_000_000);
        await Assert.That(sizer.CurrentFetchMaxBytes).IsLessThan(100_000_000);
    }

    [Test]
    public async Task EvaluateAndAdjust_DoesNotShrinkBeforeStableWindow()
    {
        var options = new AdaptiveFetchSizingOptions
        {
            InitialPartitionFetchBytes = 4_000_000,
            StableWindowCount = 3
        };
        var sizer = new AdaptiveFetchSizer(options);

        sizer.EvaluateAndAdjust(3.0);
        sizer.EvaluateAndAdjust(3.0);

        await Assert.That(sizer.CurrentPartitionFetchBytes).IsEqualTo(4_000_000);
        await Assert.That(sizer.ConsecutiveShrinkSignals).IsEqualTo(2);
    }

    [Test]
    public async Task EvaluateAndAdjust_ShrinksByExpectedFactor()
    {
        var options = new AdaptiveFetchSizingOptions
        {
            InitialPartitionFetchBytes = 4_000_000,
            InitialFetchMaxBytes = 100_000_000,
            ShrinkFactor = 0.5,
            StableWindowCount = 1
        };
        var sizer = new AdaptiveFetchSizer(options);

        sizer.EvaluateAndAdjust(3.0);

        await Assert.That(sizer.CurrentPartitionFetchBytes).IsEqualTo(2_000_000);
        await Assert.That(sizer.CurrentFetchMaxBytes).IsEqualTo(50_000_000);
    }

    #endregion

    #region Bounds enforcement

    [Test]
    public async Task EvaluateAndAdjust_RespectsMaxPartitionFetchBytes()
    {
        var options = new AdaptiveFetchSizingOptions
        {
            InitialPartitionFetchBytes = 15_000_000,
            MaxPartitionFetchBytes = 16_000_000,
            GrowthFactor = 1.5,
            StableWindowCount = 1
        };
        var sizer = new AdaptiveFetchSizer(options);

        sizer.EvaluateAndAdjust(0.3); // Would grow to 22.5M but max is 16M

        await Assert.That(sizer.CurrentPartitionFetchBytes).IsEqualTo(16_000_000);
    }

    [Test]
    public async Task EvaluateAndAdjust_RespectsMinPartitionFetchBytes()
    {
        var options = new AdaptiveFetchSizingOptions
        {
            InitialPartitionFetchBytes = 1_500_000,
            MinPartitionFetchBytes = 1_048_576,
            ShrinkFactor = 0.5,
            StableWindowCount = 1
        };
        var sizer = new AdaptiveFetchSizer(options);

        // Shrink to 750K — should clamp to min of 1048576
        sizer.EvaluateAndAdjust(3.0);

        await Assert.That(sizer.CurrentPartitionFetchBytes).IsEqualTo(1_048_576);
    }

    [Test]
    public async Task EvaluateAndAdjust_RespectsMaxFetchMaxBytes()
    {
        var options = new AdaptiveFetchSizingOptions
        {
            InitialFetchMaxBytes = 90_000_000,
            MaxFetchMaxBytes = 100_000_000,
            GrowthFactor = 1.5,
            StableWindowCount = 1
        };
        var sizer = new AdaptiveFetchSizer(options);

        sizer.EvaluateAndAdjust(0.3); // Would grow to 135M but max is 100M

        await Assert.That(sizer.CurrentFetchMaxBytes).IsEqualTo(100_000_000);
    }

    [Test]
    public async Task EvaluateAndAdjust_RespectsMinFetchMaxBytes()
    {
        var options = new AdaptiveFetchSizingOptions
        {
            InitialFetchMaxBytes = 2_000_000,
            MinFetchMaxBytes = 1_048_576,
            ShrinkFactor = 0.25,
            StableWindowCount = 1
        };
        var sizer = new AdaptiveFetchSizer(options);

        // Shrink to 500K — should clamp to min of 1048576
        sizer.EvaluateAndAdjust(3.0);

        await Assert.That(sizer.CurrentFetchMaxBytes).IsEqualTo(1_048_576);
    }

    #endregion

    #region Stable zone — no adjustment

    [Test]
    public async Task EvaluateAndAdjust_StableZone_NoChange()
    {
        var sizer = CreateSizer();
        var initialPartitionBytes = sizer.CurrentPartitionFetchBytes;
        var initialFetchMaxBytes = sizer.CurrentFetchMaxBytes;

        // Ratio between grow (0.5) and shrink (2.0) thresholds — stable zone
        for (var i = 0; i < 10; i++)
            sizer.EvaluateAndAdjust(1.0);

        await Assert.That(sizer.CurrentPartitionFetchBytes).IsEqualTo(initialPartitionBytes);
        await Assert.That(sizer.CurrentFetchMaxBytes).IsEqualTo(initialFetchMaxBytes);
    }

    [Test]
    public async Task EvaluateAndAdjust_StableZone_ResetsCounters()
    {
        var sizer = CreateSizer();

        // Accumulate 2 grow signals
        sizer.EvaluateAndAdjust(0.3);
        sizer.EvaluateAndAdjust(0.3);
        await Assert.That(sizer.ConsecutiveGrowSignals).IsEqualTo(2);

        // Enter stable zone — should reset grow counter
        sizer.EvaluateAndAdjust(1.0);
        await Assert.That(sizer.ConsecutiveGrowSignals).IsEqualTo(0);
        await Assert.That(sizer.ConsecutiveShrinkSignals).IsEqualTo(0);
    }

    #endregion

    #region Signal interruption — opposing signals reset counters

    [Test]
    public async Task EvaluateAndAdjust_ShrinkSignalResetsGrowCounter()
    {
        var sizer = CreateSizer();

        // Accumulate 2 grow signals
        sizer.EvaluateAndAdjust(0.3);
        sizer.EvaluateAndAdjust(0.3);
        await Assert.That(sizer.ConsecutiveGrowSignals).IsEqualTo(2);

        // A shrink signal should reset the grow counter
        sizer.EvaluateAndAdjust(3.0);
        await Assert.That(sizer.ConsecutiveGrowSignals).IsEqualTo(0);
        await Assert.That(sizer.ConsecutiveShrinkSignals).IsEqualTo(1);
    }

    [Test]
    public async Task EvaluateAndAdjust_GrowSignalResetsShrinkCounter()
    {
        var sizer = CreateSizer();

        // Accumulate 2 shrink signals
        sizer.EvaluateAndAdjust(3.0);
        sizer.EvaluateAndAdjust(3.0);
        await Assert.That(sizer.ConsecutiveShrinkSignals).IsEqualTo(2);

        // A grow signal should reset the shrink counter
        sizer.EvaluateAndAdjust(0.3);
        await Assert.That(sizer.ConsecutiveShrinkSignals).IsEqualTo(0);
        await Assert.That(sizer.ConsecutiveGrowSignals).IsEqualTo(1);
    }

    #endregion

    #region ReportProcessingComplete integration

    [Test]
    public async Task ReportProcessingComplete_IgnoredWithoutFetchTiming()
    {
        var sizer = CreateSizerWithWindowOne();
        var initial = sizer.CurrentPartitionFetchBytes;

        // No RecordFetchStart/End called — should be a no-op
        sizer.ReportProcessingComplete(TimeSpan.FromMilliseconds(100));

        await Assert.That(sizer.CurrentPartitionFetchBytes).IsEqualTo(initial);
    }

    #endregion

    #region Configuration defaults

    [Test]
    public async Task DefaultOptions_HaveSensibleValues()
    {
        var options = new AdaptiveFetchSizingOptions();

        await Assert.That(options.MinPartitionFetchBytes).IsEqualTo(1_048_576);
        await Assert.That(options.MaxPartitionFetchBytes).IsEqualTo(16 * 1024 * 1024);
        await Assert.That(options.InitialPartitionFetchBytes).IsEqualTo(1_048_576);
        await Assert.That(options.MinFetchMaxBytes).IsEqualTo(1_048_576);
        await Assert.That(options.MaxFetchMaxBytes).IsEqualTo(100 * 1024 * 1024);
        await Assert.That(options.InitialFetchMaxBytes).IsEqualTo(52_428_800);
        await Assert.That(options.GrowthFactor).IsEqualTo(1.5);
        await Assert.That(options.ShrinkFactor).IsEqualTo(0.75);
        await Assert.That(options.GrowThreshold).IsEqualTo(0.5);
        await Assert.That(options.ShrinkThreshold).IsEqualTo(2.0);
        await Assert.That(options.StableWindowCount).IsEqualTo(3);
    }

    #endregion

    #region Custom stable window count

    [Test]
    public async Task EvaluateAndAdjust_CustomStableWindowOf5_RequiresFiveSignals()
    {
        var options = new AdaptiveFetchSizingOptions
        {
            MinPartitionFetchBytes = 500_000,
            InitialPartitionFetchBytes = 1_000_000,
            StableWindowCount = 5,
            GrowthFactor = 1.5
        };
        var sizer = new AdaptiveFetchSizer(options);

        // 4 signals should not trigger growth
        for (var i = 0; i < 4; i++)
            sizer.EvaluateAndAdjust(0.3);
        await Assert.That(sizer.CurrentPartitionFetchBytes).IsEqualTo(1_000_000);

        // 5th signal triggers growth
        sizer.EvaluateAndAdjust(0.3);
        await Assert.That(sizer.CurrentPartitionFetchBytes).IsEqualTo(1_500_000);
    }

    #endregion

    #region Counters reset after adjustment

    [Test]
    public async Task EvaluateAndAdjust_CountersResetAfterGrow()
    {
        var sizer = CreateSizerWithWindowOne();

        sizer.EvaluateAndAdjust(0.3); // Triggers grow, resets counter
        await Assert.That(sizer.ConsecutiveGrowSignals).IsEqualTo(0);
    }

    [Test]
    public async Task EvaluateAndAdjust_CountersResetAfterShrink()
    {
        var options = new AdaptiveFetchSizingOptions
        {
            InitialPartitionFetchBytes = 4_000_000,
            StableWindowCount = 1
        };
        var sizer = new AdaptiveFetchSizer(options);

        sizer.EvaluateAndAdjust(3.0); // Triggers shrink, resets counter
        await Assert.That(sizer.ConsecutiveShrinkSignals).IsEqualTo(0);
    }

    #endregion

    #region Input validation

    [Test]
    public async Task Constructor_ThrowsWhenMinPartitionFetchBytesExceedsInitial()
    {
        var options = new AdaptiveFetchSizingOptions
        {
            MinPartitionFetchBytes = 2_000_000,
            InitialPartitionFetchBytes = 1_000_000
        };

        var act = () => new AdaptiveFetchSizer(options);

        await Assert.That(act).Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_ThrowsWhenInitialPartitionFetchBytesExceedsMax()
    {
        var options = new AdaptiveFetchSizingOptions
        {
            InitialPartitionFetchBytes = 20_000_000,
            MaxPartitionFetchBytes = 16_000_000
        };

        var act = () => new AdaptiveFetchSizer(options);

        await Assert.That(act).Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_ThrowsWhenMinFetchMaxBytesExceedsInitial()
    {
        var options = new AdaptiveFetchSizingOptions
        {
            MinFetchMaxBytes = 60_000_000,
            InitialFetchMaxBytes = 50_000_000
        };

        var act = () => new AdaptiveFetchSizer(options);

        await Assert.That(act).Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_ThrowsWhenInitialFetchMaxBytesExceedsMax()
    {
        var options = new AdaptiveFetchSizingOptions
        {
            InitialFetchMaxBytes = 200_000_000,
            MaxFetchMaxBytes = 100_000_000
        };

        var act = () => new AdaptiveFetchSizer(options);

        await Assert.That(act).Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_ThrowsWhenGrowthFactorIsOne()
    {
        var options = new AdaptiveFetchSizingOptions { GrowthFactor = 1.0 };

        var act = () => new AdaptiveFetchSizer(options);

        await Assert.That(act).Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_ThrowsWhenGrowthFactorIsLessThanOne()
    {
        var options = new AdaptiveFetchSizingOptions { GrowthFactor = 0.5 };

        var act = () => new AdaptiveFetchSizer(options);

        await Assert.That(act).Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_ThrowsWhenShrinkFactorIsZero()
    {
        var options = new AdaptiveFetchSizingOptions { ShrinkFactor = 0.0 };

        var act = () => new AdaptiveFetchSizer(options);

        await Assert.That(act).Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_ThrowsWhenShrinkFactorIsOne()
    {
        var options = new AdaptiveFetchSizingOptions { ShrinkFactor = 1.0 };

        var act = () => new AdaptiveFetchSizer(options);

        await Assert.That(act).Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_ThrowsWhenStableWindowCountIsZero()
    {
        var options = new AdaptiveFetchSizingOptions { StableWindowCount = 0 };

        var act = () => new AdaptiveFetchSizer(options);

        await Assert.That(act).Throws<ArgumentException>();
    }

    #endregion

    #region Helpers

    private static AdaptiveFetchSizer CreateSizer() =>
        new(new AdaptiveFetchSizingOptions());

    private static AdaptiveFetchSizer CreateSizerWithWindowOne() =>
        new(new AdaptiveFetchSizingOptions { StableWindowCount = 1 });

    #endregion
}
