using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dekaf.Extensions.DependencyInjection;

/// <summary>
/// Hosted service that initializes all registered Dekaf Kafka clients at host startup.
/// </summary>
internal sealed class DekafInitializationService : IHostedService
{
    private readonly IEnumerable<IInitializableKafkaClient> _clients;
    private readonly ILogger<DekafInitializationService>? _logger;

    public DekafInitializationService(
        IEnumerable<IInitializableKafkaClient> clients,
        ILogger<DekafInitializationService>? logger = null)
    {
        _clients = clients;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var client in _clients)
        {
            _logger?.LogDebug("Initializing Dekaf client {ClientType}", client.GetType().Name);
            await client.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
