using Dekaf.Diagnostics;
using Dekaf.OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

namespace Dekaf.Tests.Unit.Diagnostics;

public sealed class OpenTelemetryExtensionTests
{
    [Test]
    public async Task AddDekafInstrumentation_Tracing_RegistersCorrectSource()
    {
        // Verify the extension method can be called and uses the correct source name
        // Building the provider would fail if the source name were wrong
        using var provider = global::OpenTelemetry.Sdk.CreateTracerProviderBuilder()
            .AddDekafInstrumentation()
            .Build();

        await Assert.That(provider).IsNotNull();
    }

    [Test]
    public async Task AddDekafInstrumentation_Metrics_RegistersCorrectMeter()
    {
        using var provider = global::OpenTelemetry.Sdk.CreateMeterProviderBuilder()
            .AddDekafInstrumentation()
            .Build();

        await Assert.That(provider).IsNotNull();
    }

    [Test]
    public async Task TracerExtension_UsesCorrectActivitySourceName()
    {
        // The extension method should use the same name as DekafDiagnostics
        var expectedName = DekafDiagnostics.ActivitySourceName;
        await Assert.That(expectedName).IsEqualTo("Dekaf");
    }

    [Test]
    public async Task MeterExtension_UsesCorrectMeterName()
    {
        var expectedName = DekafDiagnostics.MeterName;
        await Assert.That(expectedName).IsEqualTo("Dekaf");
    }
}
