namespace Dekaf.Tests.Integration;

[Category("Retry")]
public sealed class SchemaRegistryStartupDeadlineTests
{
    [Test]
    public async Task ResolveStartupDeadline_OnCi_StaysBelowTheHangDumpWindow()
    {
        // MTP's hang dump fires after 5 minutes without progress; the fixture must fail first.
        var deadline = KafkaWithSchemaRegistryContainer.ResolveStartupDeadline(null, "true");

        await Assert.That(deadline).IsEqualTo(TimeSpan.FromMinutes(3));
    }

    [Test]
    public async Task ResolveStartupDeadline_Locally_AllowsColdImagePulls()
    {
        var deadline = KafkaWithSchemaRegistryContainer.ResolveStartupDeadline(null, null);

        await Assert.That(deadline).IsEqualTo(TimeSpan.FromMinutes(15));
    }

    [Test]
    [Arguments("45", "true", 45)]
    [Arguments("900", null, 900)]
    public async Task ResolveStartupDeadline_Override_Wins(string configured, string? ci, int expectedSeconds)
    {
        var deadline = KafkaWithSchemaRegistryContainer.ResolveStartupDeadline(configured, ci);

        await Assert.That(deadline).IsEqualTo(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Test]
    [Arguments("0")]
    [Arguments("-5")]
    [Arguments("soon")]
    public async Task ResolveStartupDeadline_InvalidOverride_UsesDefault(string configured)
    {
        var deadline = KafkaWithSchemaRegistryContainer.ResolveStartupDeadline(configured, "true");

        await Assert.That(deadline).IsEqualTo(TimeSpan.FromMinutes(3));
    }
}
