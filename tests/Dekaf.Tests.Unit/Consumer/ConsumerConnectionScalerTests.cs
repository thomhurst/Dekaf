using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public class ConsumerConnectionScalerTests
{
    [Test]
    public async Task MaybeScale_PipelineSaturated_ScalesUpAfterSustainedPeriod()
    {
        var scaleUpCount = 0;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: ct => { scaleUpCount++; return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { return ValueTask.CompletedTask; },
            pipelineDepth: 3);

        scaler.ReportPipelineUtilization(3, 3);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(0);

        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        scaler.ReportPipelineUtilization(3, 3);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(1);
        await Assert.That(scaler.CurrentConnectionCount).IsEqualTo(3);
    }

    [Test]
    public async Task MaybeScale_LowUtilization_ScalesDownAfterSustainedPeriod()
    {
        var scaleDownCount = 0;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: ct => { return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { scaleDownCount++; return ValueTask.CompletedTask; },
            pipelineDepth: 3);

        scaler.TestSetConnectionCount(3);
        scaler.ReportPipelineUtilization(0, 3);
        scaler.MaybeScale();
        await Assert.That(scaleDownCount).IsEqualTo(0);

        scaler.TestAdvanceTime(TimeSpan.FromSeconds(121));
        scaler.ReportPipelineUtilization(0, 3);
        scaler.MaybeScale();
        await Assert.That(scaleDownCount).IsEqualTo(1);
        await Assert.That(scaler.CurrentConnectionCount).IsEqualTo(2);
    }

    [Test]
    public async Task MaybeScale_RespectsMaxConnectionCount()
    {
        var scaleUpCount = 0;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 3,
            scaleUpAsync: ct => { scaleUpCount++; return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { return ValueTask.CompletedTask; },
            pipelineDepth: 3);

        scaler.ReportPipelineUtilization(3, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        scaler.ReportPipelineUtilization(3, 3);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(1);
        await Assert.That(scaler.CurrentConnectionCount).IsEqualTo(3);

        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        scaler.ReportPipelineUtilization(3, 3);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(1); // Still 1 — at max
    }

    [Test]
    public async Task MaybeScale_RespectsMinConnectionCount()
    {
        var scaleDownCount = 0;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: ct => { return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { scaleDownCount++; return ValueTask.CompletedTask; },
            pipelineDepth: 3);

        scaler.ReportPipelineUtilization(0, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(121));
        scaler.ReportPipelineUtilization(0, 3);
        scaler.MaybeScale();
        await Assert.That(scaleDownCount).IsEqualTo(0); // At initial — can't go lower
    }

    [Test]
    public async Task MaybeScale_CooldownPreventsRapidScaling()
    {
        var scaleUpCount = 0;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: ct => { scaleUpCount++; return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { return ValueTask.CompletedTask; },
            pipelineDepth: 3);

        // First scale-up
        scaler.ReportPipelineUtilization(3, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        scaler.ReportPipelineUtilization(3, 3);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(1);

        // Within cooldown
        scaler.ReportPipelineUtilization(3, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(3));
        scaler.ReportPipelineUtilization(3, 3);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(1); // Blocked by cooldown

        // After cooldown
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        scaler.ReportPipelineUtilization(3, 3);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(2);
    }

    [Test]
    public async Task MaybeScale_SaturationReset_WhenPipelineNotFull()
    {
        var scaleUpCount = 0;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: ct => { scaleUpCount++; return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { return ValueTask.CompletedTask; },
            pipelineDepth: 3);

        scaler.ReportPipelineUtilization(3, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(3));
        scaler.ReportPipelineUtilization(2, 3); // Drop below full — resets timer

        scaler.TestAdvanceTime(TimeSpan.FromSeconds(3));
        scaler.ReportPipelineUtilization(3, 3);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(0); // Interrupted saturation
    }

    [Test]
    public async Task Scaler_DisabledWhenMaxEqualsInitial()
    {
        var scaleUpCount = 0;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 2, // Same as initial = effectively disabled
            scaleUpAsync: ct => { scaleUpCount++; return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { return ValueTask.CompletedTask; },
            pipelineDepth: 3);

        scaler.ReportPipelineUtilization(3, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        scaler.ReportPipelineUtilization(3, 3);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(0); // Cannot scale
    }
}
