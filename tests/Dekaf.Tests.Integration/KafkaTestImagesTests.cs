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

    [Test]
    [Arguments("4.0.10")]
    [Arguments("4.10.0")]
    public async Task Parse_MultiDigitCompactVersionComponent_Throws(string release)
    {
        var image = $"apache/kafka:{release}@sha256:test";

        await Assert.That(() => KafkaTestImages.Parse(image))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("single-digit minor and patch");
    }

    [Test]
    public async Task Resolve_CurrentLane_UsesCurrentStableImage()
    {
        var resolved = KafkaTestImages.Resolve(KafkaTestImages.CurrentLane);

        await Assert.That(resolved.Release).IsEqualTo("4.3.1");
        await Assert.That(resolved.VersionNumber).IsEqualTo(431);
        await Assert.That(resolved.Image).IsEqualTo(KafkaTestImages.CurrentImage);
    }

    [Test]
    public async Task TransactionFaultContainer_UsesSelectedKafkaLane()
    {
        var selected = KafkaTestImages.Selected;
        await using var container = new TransactionFaultKafkaContainer();

        await Assert.That(container.ContainerName).IsEqualTo(selected.Image);
        await Assert.That(container.Version).IsEqualTo(selected.VersionNumber);
    }
}
