using System.Reflection;
using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Protocol.Messages;
using Dekaf.Telemetry;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientTelemetryTests
{
    private static readonly MethodInfo EnsureInitializedMethod = typeof(AdminClient)
        .GetMethod("EnsureInitializedAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo MetadataManagerField = typeof(AdminClient)
        .GetField("_metadataManager", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo TelemetryManagerField = typeof(AdminClient)
        .GetField("_telemetryManager", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo TelemetryDisabledField = typeof(ClientTelemetryManager)
        .GetField("_disabled", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Test]
    public async Task EnsureInitializedAsync_AttemptsTelemetryStartOnlyOnceAfterFailure()
    {
        await using var client = new AdminClient(new AdminClientOptions
        {
            BootstrapServers = ["localhost:9092"],
            RequestTimeoutMs = 100
        });

        var metadataManager = (MetadataManager)MetadataManagerField.GetValue(client)!;
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers = [],
            ClusterId = "test-cluster",
            ControllerId = -1,
            Topics = []
        });

        var telemetryManager = (ClientTelemetryManager)TelemetryManagerField.GetValue(client)!;

        await InvokeEnsureInitializedAsync(client);
        await Assert.That(telemetryManager.IsDisabled).IsTrue();

        TelemetryDisabledField.SetValue(telemetryManager, 0);

        await InvokeEnsureInitializedAsync(client);
        await Assert.That(telemetryManager.IsDisabled).IsFalse();
    }

    private static async ValueTask InvokeEnsureInitializedAsync(AdminClient client)
    {
        var result = (ValueTask)EnsureInitializedMethod.Invoke(client, [CancellationToken.None])!;
        await result.ConfigureAwait(false);
    }
}
