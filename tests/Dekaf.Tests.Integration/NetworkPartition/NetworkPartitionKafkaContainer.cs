using Docker.DotNet;

namespace Dekaf.Tests.Integration.NetworkPartition;

/// <summary>
/// Kafka container wrapper with pause/unpause capabilities for network partition simulation.
/// Uses Docker's pause/unpause to freeze container processes, simulating a network partition
/// where TCP connections go silent (no RST, no FIN).
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
        // Always unpause before disposing to avoid Docker cleanup issues
        try
        {
            var container = ContainerInstance;
            if (container is not null)
            {
                var client = GetDockerClient();
                try
                {
                    await client.Containers.UnpauseContainerAsync(container.Id).ConfigureAwait(false);
                }
                catch
                {
                    // Container may not be paused, ignore
                }
            }
        }
        finally
        {
            _dockerClient?.Dispose();
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}
