using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public class WaveCoalesceGateTests
{
    [Test]
    public async Task NewGate_AllowsSpinning()
    {
        var gate = new WaveCoalesceGate();

        await Assert.That(gate.ShouldSpin()).IsTrue();
    }

    [Test]
    public async Task FruitlessSpins_BelowThreshold_KeepGateOpen()
    {
        var gate = new WaveCoalesceGate();

        for (var i = 0; i < WaveCoalesceGate.FruitlessSpinSuppressThreshold - 1; i++)
            gate.OnSpinCompleted(coalescedAdditionalBatch: false);

        await Assert.That(gate.ShouldSpin()).IsTrue();
    }

    [Test]
    public async Task FruitlessSpins_AtThreshold_SuppressSpinning()
    {
        var gate = new WaveCoalesceGate();

        for (var i = 0; i < WaveCoalesceGate.FruitlessSpinSuppressThreshold; i++)
            gate.OnSpinCompleted(coalescedAdditionalBatch: false);

        await Assert.That(gate.ShouldSpin()).IsFalse();
    }

    [Test]
    public async Task SuccessfulSpin_ResetsFruitlessCount()
    {
        var gate = new WaveCoalesceGate();
        for (var i = 0; i < WaveCoalesceGate.FruitlessSpinSuppressThreshold - 1; i++)
            gate.OnSpinCompleted(coalescedAdditionalBatch: false);

        gate.OnSpinCompleted(coalescedAdditionalBatch: true);
        gate.OnSpinCompleted(coalescedAdditionalBatch: false);

        await Assert.That(gate.ShouldSpin()).IsTrue();
    }

    [Test]
    public async Task SuppressedGate_ReopensForProbe_AfterReprobeSendInterval()
    {
        var gate = new WaveCoalesceGate();
        for (var i = 0; i < WaveCoalesceGate.FruitlessSpinSuppressThreshold; i++)
            gate.OnSpinCompleted(coalescedAdditionalBatch: false);

        for (var i = 0; i < WaveCoalesceGate.SuppressedReprobeSendInterval - 1; i++)
            gate.OnSent();
        await Assert.That(gate.ShouldSpin()).IsFalse();

        gate.OnSent();

        await Assert.That(gate.ShouldSpin()).IsTrue();
    }

    [Test]
    public async Task FruitlessProbe_ResuppressesForAnotherFullInterval()
    {
        var gate = new WaveCoalesceGate();
        for (var i = 0; i < WaveCoalesceGate.FruitlessSpinSuppressThreshold; i++)
            gate.OnSpinCompleted(coalescedAdditionalBatch: false);
        for (var i = 0; i < WaveCoalesceGate.SuppressedReprobeSendInterval; i++)
            gate.OnSent();

        gate.OnSpinCompleted(coalescedAdditionalBatch: false);

        await Assert.That(gate.ShouldSpin()).IsFalse();
        for (var i = 0; i < WaveCoalesceGate.SuppressedReprobeSendInterval - 1; i++)
            gate.OnSent();
        await Assert.That(gate.ShouldSpin()).IsFalse();
        gate.OnSent();
        await Assert.That(gate.ShouldSpin()).IsTrue();
    }

    [Test]
    public async Task SuccessfulProbe_ReopensGateFully()
    {
        var gate = new WaveCoalesceGate();
        for (var i = 0; i < WaveCoalesceGate.FruitlessSpinSuppressThreshold; i++)
            gate.OnSpinCompleted(coalescedAdditionalBatch: false);
        for (var i = 0; i < WaveCoalesceGate.SuppressedReprobeSendInterval; i++)
            gate.OnSent();

        gate.OnSpinCompleted(coalescedAdditionalBatch: true);

        await Assert.That(gate.ShouldSpin()).IsTrue();
        gate.OnSent();
        await Assert.That(gate.ShouldSpin()).IsTrue();
    }
}
