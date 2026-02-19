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
    private bool _startupScriptPatched;

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
        // Patch the startup script (once) to resolve the container IP dynamically.
        // Must be done while the container is running (before kill).
        // Testcontainers generates /testcontainers.sh with a hardcoded container IP in the
        // BROKER listener. After docker kill + start, the container may get a different IP,
        // causing KRaft controller quorum failure. This patched version resolves the IP at boot.
        if (!_startupScriptPatched)
        {
            await PatchStartupScriptAsync().ConfigureAwait(false);
            _startupScriptPatched = true;
        }

        var client = GetDockerClient();
        var containerId = GetContainerId();
        Console.WriteLine("[NetworkPartitionKafkaContainer] Stopping broker (SIGKILL)...");
        await client.Containers.KillContainerAsync(containerId, new ContainerKillParameters
        {
            Signal = "SIGKILL"
        }).ConfigureAwait(false);
        // Wait for the container to fully stop
        await client.Containers.WaitContainerAsync(containerId).ConfigureAwait(false);
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
        // Broker cold-start after SIGKILL requires log recovery, which takes longer than initial startup
        // especially on CI runners with constrained resources
        await WaitForKafkaAsync(maxAttempts: 90).ConfigureAwait(false);
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

    /// <summary>
    /// Rewrites /testcontainers.sh with dynamic IP resolution so it survives container restarts.
    /// Must be called while the container is running (uses docker exec).
    /// </summary>
    private async Task PatchStartupScriptAsync()
    {
        var container = ContainerInstance
            ?? throw new InvalidOperationException("Container has not been initialized.");

        var colonIndex = BootstrapServers.LastIndexOf(':');
        var hostPort = BootstrapServers[(colonIndex + 1)..];

        // Use a heredoc with a single-quoted delimiter to write the script literally
        // (no shell expansion within the heredoc body).
        // At runtime, bash will expand $(hostname -i ...) and ${CONTAINER_IP}.
        var shellCommand = string.Join("\n",
            "cat > /testcontainers.sh << 'ENDOFSCRIPT'",
            "#!/bin/bash",
            "CONTAINER_IP=$(hostname -i | awk '{print $1}')",
            $"export KAFKA_ADVERTISED_LISTENERS=\"PLAINTEXT://127.0.0.1:{hostPort},BROKER://${{CONTAINER_IP}}:9093\"",
            "/etc/kafka/docker/run",
            "ENDOFSCRIPT",
            "chmod 755 /testcontainers.sh");

        var result = await container.ExecAsync(["sh", "-c", shellCommand]).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            Console.WriteLine($"[NetworkPartitionKafkaContainer] Warning: Failed to patch startup script (exit {result.ExitCode}): {result.Stderr}");
        }
        else
        {
            Console.WriteLine("[NetworkPartitionKafkaContainer] Patched startup script for restart support.");
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
