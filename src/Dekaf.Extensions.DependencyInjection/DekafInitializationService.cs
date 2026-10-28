using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dekaf.Extensions.DependencyInjection;

/// <summary>
/// Hosted service that initializes all registered Dekaf Kafka clients at host startup.
/// Clients owned by a hosted service (see <see cref="HostedServiceOwnedClient"/>) are skipped,
/// because that service initializes them with its own retry.
/// </summary>
internal sealed partial class DekafInitializationService : IHostedService
{
    private readonly IEnumerable<IInitializableKafkaClient> _clients;
    private readonly IEnumerable<HostedServiceOwnedClient> _hostedServiceOwnedClients;
    private readonly ILogger _logger;

    public DekafInitializationService(
        IEnumerable<IInitializableKafkaClient> clients,
        IEnumerable<HostedServiceOwnedClient> hostedServiceOwnedClients,
        ILogger<DekafInitializationService>? logger = null)
    {
        _clients = clients;
        _hostedServiceOwnedClients = hostedServiceOwnedClients;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DekafInitializationService>.Instance;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var hostedServiceOwned = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var owned in _hostedServiceOwnedClients)
            hostedServiceOwned.Add(owned.Client);

        foreach (var client in _clients)
        {
            if (hostedServiceOwned.Contains(client))
            {
                LogSkippingHostedServiceClient(client.GetType().Name);
                continue;
            }

            LogInitializingClient(client.GetType().Name);
            await client.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    #region Logging

    [LoggerMessage(Level = LogLevel.Debug, Message = "Initializing Dekaf client {ClientType}")]
    private partial void LogInitializingClient(string clientType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping Dekaf client {ClientType}: its hosted service initializes it")]
    private partial void LogSkippingHostedServiceClient(string clientType);

    #endregion
}
