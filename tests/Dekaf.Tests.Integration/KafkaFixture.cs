using System.Net.Sockets;
using Testcontainers.Kafka;
using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Shared Kafka container fixture for integration tests.
/// Uses KAFKA_BOOTSTRAP_SERVERS env var if set (for CI), otherwise starts Testcontainers.
/// </summary>
public class KafkaFixture : IAsyncInitializer, IAsyncDisposable
{
    private KafkaContainer? _container;
    private bool _externalKafka;
    private string _bootstrapServers = string.Empty;

    public string BootstrapServers => _bootstrapServers;

    public async Task InitializeAsync()
    {
        // Check for external Kafka (CI environment)
        var externalBootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS");
        if (!string.IsNullOrEmpty(externalBootstrap))
        {
            _bootstrapServers = externalBootstrap;
            _externalKafka = true;
            Console.WriteLine($"Using external Kafka at {_bootstrapServers}");
            await WaitForKafkaAsync();
            return;
        }

        Console.WriteLine("Starting Kafka container via Testcontainers...");
        _container = new KafkaBuilder("confluentinc/cp-kafka:7.5.0")
            .WithPortBinding(9092, true)
            .Build();

        await _container.StartAsync();
        _bootstrapServers = _container.GetBootstrapAddress();
        Console.WriteLine($"Kafka started at {_bootstrapServers}");
        await WaitForKafkaAsync();
    }

    private async Task WaitForKafkaAsync()
    {
        Console.WriteLine("Waiting for Kafka to be ready...");
        var maxAttempts = 30;

        // Parse host and port
        var parts = _bootstrapServers.Split(':');
        var host = parts[0];
        var port = int.Parse(parts[1]);

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(host, port);
                if (client.Connected)
                {
                    Console.WriteLine("Kafka is accepting connections");
                    // Give it a moment to fully initialize
                    await Task.Delay(2000);
                    return;
                }
            }
            catch
            {
                // Ignore and retry
            }

            await Task.Delay(1000);
        }

        throw new InvalidOperationException($"Kafka not ready after {maxAttempts} attempts");
    }

    public async ValueTask DisposeAsync()
    {
        if (_externalKafka)
        {
            GC.SuppressFinalize(this);
            return;
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}
