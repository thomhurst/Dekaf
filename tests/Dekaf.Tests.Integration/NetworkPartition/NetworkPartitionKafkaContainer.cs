using Docker.DotNet;
using Docker.DotNet.Models;

namespace Dekaf.Tests.Integration.NetworkPartition;

/// <summary>
/// Kafka container wrapper with pause/unpause and stop/start capabilities for network partition
/// and broker failure simulation.
/// Uses Docker's pause/unpause to freeze container processes, simulating a network partition
/// where TCP connections go silent (no RST, no FIN).
/// Uses Docker's stop/start to kill and restart the broker process, simulating a full broker crash.
/// Not shared across test classes to avoid interference between partition tests.
/// </summary>
public class NetworkPartitionKafkaContainer : KafkaTestContainer
{
    private DockerClient? _dockerClient;

    public override string ContainerName => "apache/kafka:3.9.1";
    public override int Version => 391;

    /// <summary>
    /// Pauses the Kafka container, simulating a network partition.
    /// All processes in the container are frozen; TCP connections go silent.
    /// </summary>
    public async Task PauseAsync()
    {
        var client = GetDockerClient();
        var containerId = GetContainerId();
        await client.Containers.PauseContainerAsync(containerId).ConfigureAwait(false);
    }

    /// <summary>
    /// Unpauses the Kafka container, restoring network connectivity.
    /// </summary>
    public async Task UnpauseAsync()
    {
        var client = GetDockerClient();
        var containerId = GetContainerId();
        await client.Containers.UnpauseContainerAsync(containerId).ConfigureAwait(false);
    }

    /// <summary>
    /// Attempts to unpause the container, logging any unexpected failures.
    /// Intended for use in finally blocks to ensure container cleanup.
    /// </summary>
    public async Task TryUnpauseAsync()
    {
        try
        {
            await UnpauseAsync().ConfigureAwait(false);
        }
        catch (Docker.DotNet.DockerApiException)
        {
            // Container is not paused - expected when unpause already succeeded in the test
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NetworkPartitionKafkaContainer] Unexpected error during cleanup unpause: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops the Kafka container with immediate SIGKILL, simulating a broker crash.
    /// The broker process is killed and the container exits.
    /// </summary>
    public async Task StopBrokerAsync()
    {
        var client = GetDockerClient();
        var containerId = GetContainerId();
        Console.WriteLine("[NetworkPartitionKafkaContainer] Stopping broker (SIGKILL)...");
        await client.Containers.StopContainerAsync(containerId, new ContainerStopParameters
        {
            WaitBeforeKillSeconds = 0
        }).ConfigureAwait(false);
        Console.WriteLine("[NetworkPartitionKafkaContainer] Broker stopped.");
    }

    /// <summary>
    /// Starts the Kafka container after a stop, then waits for it to be ready.
    /// Simulates broker recovery after a crash.
    /// </summary>
    public async Task StartBrokerAsync()
    {
        var client = GetDockerClient();
        var containerId = GetContainerId();
        Console.WriteLine("[NetworkPartitionKafkaContainer] Starting broker...");
        await client.Containers.StartContainerAsync(containerId, new ContainerStartParameters()).ConfigureAwait(false);
        await WaitForKafkaAsync().ConfigureAwait(false);
        Console.WriteLine("[NetworkPartitionKafkaContainer] Broker started and ready.");
    }

    /// <summary>
    /// Attempts to start the broker, swallowing exceptions if it's already running.
    /// Intended for use in finally blocks to ensure container is running before disposal.
    /// </summary>
    public async Task TryStartBrokerAsync()
    {
        try
        {
            await StartBrokerAsync().ConfigureAwait(false);
        }
        catch (DockerApiException)
        {
            // Container is already running - expected when start already succeeded in the test
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NetworkPartitionKafkaContainer] Unexpected error during cleanup start: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private string GetContainerId()
    {
        var container = ContainerInstance
            ?? throw new InvalidOperationException("Container has not been initialized. Call InitializeAsync() first.");

        return container.Id;
    }

    private DockerClient GetDockerClient()
    {
        _dockerClient ??= new DockerClientConfiguration().CreateClient();
        return _dockerClient;
    }

    public new async ValueTask DisposeAsync()
    {
        // Always attempt to unpause and start before disposing to avoid Docker cleanup issues
        try
        {
            if (ContainerInstance is not null)
            {
                await TryUnpauseAsync().ConfigureAwait(false);
                await TryStartBrokerAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _dockerClient?.Dispose();
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}
