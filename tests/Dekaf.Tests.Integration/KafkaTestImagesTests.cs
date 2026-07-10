namespace Dekaf.Tests.Integration;

[Category("Admin")]
public sealed class KafkaTestImagesTests
{
    [Test]
    public async Task Resolve_WithoutRequestedVersion_UsesCurrentStableImage()
    {
        var resolved = KafkaTestImages.Resolve(null);
        var expected = KafkaTestImages.Parse(KafkaTestImages.CurrentImage);

        await Assert.That(resolved).IsEqualTo(expected);
    }

    [Test]
    public async Task Resolve_FloorLane_UsesPinnedFloorImage()
    {
        var resolved = KafkaTestImages.Resolve(KafkaTestImages.FloorLane);
        var expected = KafkaTestImages.Parse(KafkaTestImages.FloorImage);

        await Assert.That(resolved).IsEqualTo(expected);
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
    public async Task Parse_MultiDigitVersionComponent_IsAccepted(string release)
    {
        var image = $"apache/kafka:{release}@sha256:test";
        var parsed = KafkaTestImages.Parse(image);

        await Assert.That(parsed.Release).IsEqualTo(release);
        await Assert.That(parsed.Version).IsEqualTo(Version.Parse(release));
    }

    [Test]
    public async Task Resolve_CurrentLane_UsesCurrentStableImage()
    {
        var resolved = KafkaTestImages.Resolve(KafkaTestImages.CurrentLane);
        var expected = KafkaTestImages.Parse(KafkaTestImages.CurrentImage);

        await Assert.That(resolved).IsEqualTo(expected);
    }

    [Test]
    [Arguments("4.1.9", false)]
    [Arguments("4.2.0", true)]
    [Arguments("5.0.0", true)]
    public async Task SupportsShareGroups_UsesKafkaFeatureVersion(string release, bool expected)
    {
        var supported = KafkaTestImages.SupportsShareGroups(Version.Parse(release));

        await Assert.That(supported).IsEqualTo(expected);
    }

    [Test]
    public async Task TransactionFaultContainer_UsesSelectedKafkaLane()
    {
        var selected = KafkaTestImages.Selected;
        await using var container = new TransactionFaultKafkaContainer();

        await Assert.That(container.ContainerName).IsEqualTo(selected.Image);
        await Assert.That(container.Version).IsEqualTo(selected.Version);
    }
}
