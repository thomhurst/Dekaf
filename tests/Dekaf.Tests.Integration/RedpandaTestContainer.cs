using Testcontainers.Redpanda;
using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Single-node Redpanda broker. Redpanda speaks the Kafka protocol but negotiates older
/// versions of some APIs, so it exercises compatibility paths a Kafka 4.x broker never takes.
/// </summary>
public sealed class RedpandaTestContainer : IAsyncInitializer, IAsyncDisposable
{
    internal const string RedpandaImage =
        "docker.redpanda.com/redpandadata/redpanda:v26.2.4@sha256:c98c2f04a751e6646012cc701bac5183252cee44c88ba1dfa412e4c7124f2e89";

    private RedpandaContainer? _container;

    public string BootstrapServers { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await ContainerStartupRetry.RunAsync(
            async () =>
            {
                _container = new RedpandaBuilder(RedpandaImage).Build();
                await _container.StartAsync().ConfigureAwait(false);
            },
            DisposeContainerAsync,
            ContainerStartupRetry.IsKnownTransient).ConfigureAwait(false);

        BootstrapServers = _container!.GetBootstrapAddress();
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeContainerAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private async ValueTask DisposeContainerAsync()
    {
        var container = _container;
        _container = null;
        BootstrapServers = string.Empty;
        if (container is not null)
            await container.DisposeAsync().ConfigureAwait(false);
    }
}
