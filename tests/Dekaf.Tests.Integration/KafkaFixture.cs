using Testcontainers.Kafka;
using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Shared Kafka container fixture for integration tests.
/// </summary>
public class KafkaFixture : IAsyncInitializer, IAsyncDisposable
{
    private KafkaContainer? _container;

    public string BootstrapServers => _container?.GetBootstrapAddress()
        ?? throw new InvalidOperationException("Container not started");

    public async Task InitializeAsync()
    {
        _container = new KafkaBuilder("confluentinc/cp-kafka:7.5.0")
            .WithPortBinding(9092, true)
            .Build();

        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}
