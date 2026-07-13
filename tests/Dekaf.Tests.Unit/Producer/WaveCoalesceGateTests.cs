using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public class WaveCoalesceGateTests
{
    [Test]
    public async Task NewGate_AllowsSpinning()
    {
        var gate = new WaveCoalesceGate();

        await Assert.That(gate.ShouldSpin(nowTicks: 0)).IsTrue();
    }

    [Test]
    public async Task FruitlessSpins_BelowThreshold_KeepGateOpen()
    {
        var gate = new WaveCoalesceGate();

        for (var i = 0; i < WaveCoalesceGate.FruitlessSpinSuppressThreshold - 1; i++)
            gate.OnSpinCompleted(coalescedAdditionalBatch: false, nowTicks: 0);

        await Assert.That(gate.ShouldSpin(nowTicks: 0)).IsTrue();
    }

    [Test]
    public async Task FruitlessSpins_AtThreshold_SuppressSpinning()
    {
        var gate = new WaveCoalesceGate();

        for (var i = 0; i < WaveCoalesceGate.FruitlessSpinSuppressThreshold; i++)
            gate.OnSpinCompleted(coalescedAdditionalBatch: false, nowTicks: 0);

        await Assert.That(gate.ShouldSpin(nowTicks: 0)).IsFalse();
    }

    [Test]
    public async Task SuccessfulSpin_ResetsFruitlessCount()
    {
        var gate = new WaveCoalesceGate();
        for (var i = 0; i < WaveCoalesceGate.FruitlessSpinSuppressThreshold - 1; i++)
            gate.OnSpinCompleted(coalescedAdditionalBatch: false, nowTicks: 0);

        gate.OnSpinCompleted(coalescedAdditionalBatch: true, nowTicks: 0);
        gate.OnSpinCompleted(coalescedAdditionalBatch: false, nowTicks: 0);

        await Assert.That(gate.ShouldSpin(nowTicks: 0)).IsTrue();
    }

    [Test]
    public async Task SuppressedGate_ReopensForProbe_AfterReprobeTimeInterval()
    {
        var gate = new WaveCoalesceGate();
        for (var i = 0; i < WaveCoalesceGate.FruitlessSpinSuppressThreshold; i++)
            gate.OnSpinCompleted(coalescedAdditionalBatch: false, nowTicks: 0);

        await Assert.That(gate.ShouldSpin(WaveCoalesceGate.SuppressedReprobeIntervalTicks - 1)).IsFalse();

        await Assert.That(gate.ShouldSpin(WaveCoalesceGate.SuppressedReprobeIntervalTicks)).IsTrue();
    }

    [Test]
    public async Task FruitlessProbe_ResuppressesUntilAnotherTimeIntervalPasses()
    {
        var gate = new WaveCoalesceGate();
        for (var i = 0; i < WaveCoalesceGate.FruitlessSpinSuppressThreshold; i++)
            gate.OnSpinCompleted(coalescedAdditionalBatch: false, nowTicks: 0);
        var firstProbeAt = WaveCoalesceGate.SuppressedReprobeIntervalTicks;

        gate.OnSpinCompleted(coalescedAdditionalBatch: false, nowTicks: firstProbeAt);

        await Assert.That(gate.ShouldSpin(firstProbeAt)).IsFalse();
        await Assert.That(gate.ShouldSpin(firstProbeAt + WaveCoalesceGate.SuppressedReprobeIntervalTicks - 1))
            .IsFalse();
        await Assert.That(gate.ShouldSpin(firstProbeAt + WaveCoalesceGate.SuppressedReprobeIntervalTicks))
            .IsTrue();
    }

    [Test]
    public async Task SuccessfulProbe_ReopensGateFully()
    {
        var gate = new WaveCoalesceGate();
        for (var i = 0; i < WaveCoalesceGate.FruitlessSpinSuppressThreshold; i++)
            gate.OnSpinCompleted(coalescedAdditionalBatch: false, nowTicks: 0);

        gate.OnSpinCompleted(
            coalescedAdditionalBatch: true,
            nowTicks: WaveCoalesceGate.SuppressedReprobeIntervalTicks);

        await Assert.That(gate.ShouldSpin(WaveCoalesceGate.SuppressedReprobeIntervalTicks)).IsTrue();
        await Assert.That(gate.ShouldSpin(long.MaxValue)).IsTrue();
    }
}
