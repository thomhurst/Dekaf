namespace Dekaf.Tests.Integration;

public sealed class KafkaVersionComparisonTests
{
    [Test]
    [Arguments("4.3.10", 440, false)]
    [Arguments("4.4.0", 440, true)]
    [Arguments("4.4.1", 440, true)]
    [Arguments("5.0.0", 440, true)]
    public async Task SupportsVersionUsesSemanticComponentOrdering(
        string imageTag,
        int supportedKafkaVersion,
        bool expected)
    {
        await Assert.That(KafkaContainerDefault.SupportsVersion(imageTag, supportedKafkaVersion))
            .IsEqualTo(expected);
    }
}
