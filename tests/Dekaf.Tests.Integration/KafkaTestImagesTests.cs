namespace Dekaf.Tests.Integration;

public sealed class KafkaTestImagesTests
{
    [Test]
    public async Task Resolve_WithoutRequestedVersion_UsesCurrentStableImage()
    {
        var resolved = KafkaTestImages.Resolve(null);

        await Assert.That(resolved.Release).IsEqualTo("4.3.1");
        await Assert.That(resolved.VersionNumber).IsEqualTo(431);
        await Assert.That(resolved.Image).IsEqualTo(KafkaTestImages.CurrentImage);
    }

    [Test]
    public async Task Resolve_FloorLane_UsesPinnedFloorImage()
    {
        var resolved = KafkaTestImages.Resolve(KafkaTestImages.FloorLane);

        await Assert.That(resolved.Release).IsEqualTo("4.0.2");
        await Assert.That(resolved.VersionNumber).IsEqualTo(402);
        await Assert.That(resolved.Image).IsEqualTo(KafkaTestImages.FloorImage);
    }

    [Test]
    public async Task Resolve_UnknownVersion_Throws()
    {
        await Assert.That(() => KafkaTestImages.Resolve("3.9.0"))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("Supported lanes");
    }
}
