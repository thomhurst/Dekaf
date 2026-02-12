using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dekaf.Extensions.DependencyInjection;

/// <summary>
/// Hosted service that initializes all registered Dekaf Kafka clients at host startup.
/// </summary>
internal sealed partial class DekafInitializationService : IHostedService
{
    private readonly IEnumerable<IInitializableKafkaClient> _clients;
    private readonly ILogger _logger;

    public DekafInitializationService(
        IEnumerable<IInitializableKafkaClient> clients,
        ILogger<DekafInitializationService>? logger = null)
    {
        _clients = clients;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DekafInitializationService>.Instance;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var client in _clients)
        {
            LogInitializingClient(client.GetType().Name);
            await client.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    #region Logging

    [LoggerMessage(Level = LogLevel.Debug, Message = "Initializing Dekaf client {ClientType}")]
    private partial void LogInitializingClient(string clientType);

    #endregion
}
