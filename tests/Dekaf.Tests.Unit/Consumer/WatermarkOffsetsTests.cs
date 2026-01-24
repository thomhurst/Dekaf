using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Consumer;

public class WatermarkOffsetsTests
{
    [Test]
    public async Task WatermarkOffsets_Constructor_SetsProperties()
    {
        var watermarks = new WatermarkOffsets(10, 100);

        await Assert.That(watermarks.Low).IsEqualTo(10);
        await Assert.That(watermarks.High).IsEqualTo(100);
    }

    [Test]
    public async Task WatermarkOffsets_Equality_WorksCorrectly()
    {
        var watermarks1 = new WatermarkOffsets(10, 100);
        var watermarks2 = new WatermarkOffsets(10, 100);
        var watermarks3 = new WatermarkOffsets(20, 200);

        await Assert.That(watermarks1).IsEqualTo(watermarks2);
        await Assert.That(watermarks1).IsNotEqualTo(watermarks3);
    }

    [Test]
    public async Task WatermarkOffsets_HashCode_ConsistentForEqualValues()
    {
        var watermarks1 = new WatermarkOffsets(10, 100);
        var watermarks2 = new WatermarkOffsets(10, 100);

        await Assert.That(watermarks1.GetHashCode()).IsEqualTo(watermarks2.GetHashCode());
    }

    [Test]
    public async Task WatermarkOffsets_WithZeroValues_WorksCorrectly()
    {
        var watermarks = new WatermarkOffsets(0, 0);

        await Assert.That(watermarks.Low).IsEqualTo(0);
        await Assert.That(watermarks.High).IsEqualTo(0);
    }

    [Test]
    public async Task WatermarkOffsets_WithLargeValues_WorksCorrectly()
    {
        var watermarks = new WatermarkOffsets(long.MaxValue - 1, long.MaxValue);

        await Assert.That(watermarks.Low).IsEqualTo(long.MaxValue - 1);
        await Assert.That(watermarks.High).IsEqualTo(long.MaxValue);
    }

    [Test]
    public async Task WatermarkOffsets_Deconstruction_WorksCorrectly()
    {
        var watermarks = new WatermarkOffsets(10, 100);
        var (low, high) = watermarks;

        await Assert.That(low).IsEqualTo(10);
        await Assert.That(high).IsEqualTo(100);
    }

    [Test]
    public async Task WatermarkOffsets_ToString_ContainsValues()
    {
        var watermarks = new WatermarkOffsets(10, 100);
        var str = watermarks.ToString();

        await Assert.That(str).Contains("10");
        await Assert.That(str).Contains("100");
    }

    [Test]
    public async Task WatermarkOffsets_CalculateLag_CanComputeFromValues()
    {
        var watermarks = new WatermarkOffsets(10, 100);
        var currentPosition = 50L;

        // Consumer lag = high watermark - current position
        var lag = watermarks.High - currentPosition;

        await Assert.That(lag).IsEqualTo(50);
    }

    [Test]
    public async Task WatermarkOffsets_CalculateAvailable_CanComputeFromValues()
    {
        var watermarks = new WatermarkOffsets(10, 100);

        // Available messages = high - low
        var available = watermarks.High - watermarks.Low;

        await Assert.That(available).IsEqualTo(90);
    }
}
