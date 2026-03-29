using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerConnectionScalerTests
{
    [Test]
    public async Task MaybeScale_PipelineSaturated_ScalesUpAfterSustainedPeriod()
    {
        var scaleUpCount = 0;
        var scaler = new ConsumerConnectionScaler(
            initialConnectionCount: 2,
            maxConnectionCount: 4,
            scaleUpAsync: ct => { scaleUpCount++; return ValueTask.CompletedTask; },
            scaleDownAsync: ct => { return ValueTask.CompletedTask; });

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
            scaleDownAsync: ct => { scaleDownCount++; return ValueTask.CompletedTask; });

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
            scaleDownAsync: ct => { return ValueTask.CompletedTask; });

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
            scaleDownAsync: ct => { scaleDownCount++; return ValueTask.CompletedTask; });

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
            scaleDownAsync: ct => { return ValueTask.CompletedTask; });

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
            scaleDownAsync: ct => { return ValueTask.CompletedTask; });

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
            scaleDownAsync: ct => { return ValueTask.CompletedTask; });

        scaler.ReportPipelineUtilization(3, 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        scaler.ReportPipelineUtilization(3, 3);
        scaler.MaybeScale();
        await Assert.That(scaleUpCount).IsEqualTo(0); // Cannot scale
    }

    [Test]
    public async Task SplitPartitions_SingleConnection_ReturnsSingleGroup()
    {
        var groups = new (int StartIndex, int Count)[1];
        var count = ConsumerConnectionScaler.SplitPartitionsAcrossConnections(6, 1, groups);

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(groups[0].StartIndex).IsEqualTo(0);
        await Assert.That(groups[0].Count).IsEqualTo(6);
    }

    [Test]
    public async Task SplitPartitions_EvenSplit_DistributesEqually()
    {
        var groups = new (int StartIndex, int Count)[3];
        var count = ConsumerConnectionScaler.SplitPartitionsAcrossConnections(6, 3, groups);

        await Assert.That(count).IsEqualTo(3);
        await Assert.That(groups[0]).IsEqualTo((0, 2));
        await Assert.That(groups[1]).IsEqualTo((2, 2));
        await Assert.That(groups[2]).IsEqualTo((4, 2));
    }

    [Test]
    public async Task SplitPartitions_UnevenSplit_DistributesRemainderToFirstGroups()
    {
        var groups = new (int StartIndex, int Count)[3];
        var count = ConsumerConnectionScaler.SplitPartitionsAcrossConnections(7, 3, groups);

        await Assert.That(count).IsEqualTo(3);
        // 7 / 3 = 2 base + 1 remainder => first group gets 3, others get 2
        await Assert.That(groups[0]).IsEqualTo((0, 3));
        await Assert.That(groups[1]).IsEqualTo((3, 2));
        await Assert.That(groups[2]).IsEqualTo((5, 2));
    }

    [Test]
    public async Task SplitPartitions_MoreConnectionsThanPartitions_LimitsToPartitionCount()
    {
        var groups = new (int StartIndex, int Count)[5];
        var count = ConsumerConnectionScaler.SplitPartitionsAcrossConnections(2, 5, groups);

        await Assert.That(count).IsEqualTo(2);
        await Assert.That(groups[0]).IsEqualTo((0, 1));
        await Assert.That(groups[1]).IsEqualTo((1, 1));
    }

    [Test]
    public async Task SplitPartitions_SinglePartition_ReturnsSingleGroup()
    {
        var groups = new (int StartIndex, int Count)[3];
        var count = ConsumerConnectionScaler.SplitPartitionsAcrossConnections(1, 3, groups);

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(groups[0]).IsEqualTo((0, 1));
    }

    [Test]
    public async Task SplitPartitions_CoversAllPartitions()
    {
        // Verify that all partitions are accounted for with various splits
        var groups = new (int StartIndex, int Count)[8];
        for (var partitions = 1; partitions <= 20; partitions++)
        {
            for (var connections = 1; connections <= 5; connections++)
            {
                var groupCount = ConsumerConnectionScaler.SplitPartitionsAcrossConnections(
                    partitions, connections, groups);

                var totalPartitions = 0;
                for (var i = 0; i < groupCount; i++)
                    totalPartitions += groups[i].Count;

                await Assert.That(totalPartitions).IsEqualTo(partitions);
            }
        }
    }
}
