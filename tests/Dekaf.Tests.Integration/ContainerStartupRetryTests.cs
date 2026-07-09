using System.Net;
using Docker.DotNet;
using DotNet.Testcontainers.Containers;

namespace Dekaf.Tests.Integration;

[Category("Consumer")]
[Category("MessagingOrdering")]
public sealed class ContainerStartupRetryTests
{
    [Test]
    public async Task RunAsync_PortBindingCollision_UsesFreshAttempt()
    {
        var attempts = 0;
        var cleanups = 0;

        await ContainerStartupRetry.RunAsync(
            () => ++attempts == 1
                ? Task.FromException(new DockerApiException(
                    HttpStatusCode.InternalServerError,
                    "Bind for 0.0.0.0:32769 failed: port is already allocated"))
                : Task.CompletedTask,
            () =>
            {
                cleanups++;
                return ValueTask.CompletedTask;
            },
            ContainerStartupRetry.IsPortBindingCollision);

        await Assert.That(attempts).IsEqualTo(2);
        await Assert.That(cleanups).IsEqualTo(1);
    }

    [Test]
    public async Task RunAsync_KafkaStartupScriptBusy_UsesFreshAttempt()
    {
        var attempts = 0;
        var cleanups = 0;

        await ContainerStartupRetry.RunAsync(
            () => ++attempts == 1
                ? Task.FromException(new ContainerNotRunningException(
                    "container-id",
                    string.Empty,
                    "/bin/sh: /testcontainers.sh: Text file busy",
                    126,
                    new InvalidOperationException("container exited")))
                : Task.CompletedTask,
            () =>
            {
                cleanups++;
                return ValueTask.CompletedTask;
            },
            ContainerStartupRetry.IsKafkaStartupScriptBusy);

        await Assert.That(attempts).IsEqualTo(2);
        await Assert.That(cleanups).IsEqualTo(1);
    }

    [Test]
    public async Task RunAsync_NonTransientDockerFailure_DoesNotRetry()
    {
        var attempts = 0;
        var cleanups = 0;

        var action = async () => await ContainerStartupRetry.RunAsync(
            () =>
            {
                attempts++;
                return Task.FromException(new DockerApiException(
                    HttpStatusCode.InternalServerError,
                    "broker configuration is invalid"));
            },
            () =>
            {
                cleanups++;
                return ValueTask.CompletedTask;
            },
            ContainerStartupRetry.IsPortBindingCollision);

        await Assert.That(action).Throws<DockerApiException>();
        await Assert.That(attempts).IsEqualTo(1);
        await Assert.That(cleanups).IsEqualTo(1);
    }

    [Test]
    public async Task RunAsync_RepeatedTransientFailure_StopsAfterThreeAttempts()
    {
        var attempts = 0;
        var cleanups = 0;

        var action = async () => await ContainerStartupRetry.RunAsync(
            () =>
            {
                attempts++;
                return Task.FromException(new DockerApiException(
                    HttpStatusCode.InternalServerError,
                    "Bind failed: port is already allocated"));
            },
            () =>
            {
                cleanups++;
                return ValueTask.CompletedTask;
            },
            ContainerStartupRetry.IsPortBindingCollision);

        await Assert.That(action).Throws<DockerApiException>();
        await Assert.That(attempts).IsEqualTo(3);
        await Assert.That(cleanups).IsEqualTo(3);
    }

    [Test]
    public async Task KafkaStartupCommand_WaitsForStableCopy_ThenUsesShell()
    {
        await Assert.That(KafkaTestContainer.StartupCommand).Contains("wc -c");
        await Assert.That(KafkaTestContainer.StartupCommand).Contains("exec /bin/sh /testcontainers.sh");
    }
}
