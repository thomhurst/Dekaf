using System.Net;
using Docker.DotNet;
using DotNet.Testcontainers.Containers;

namespace Dekaf.Tests.Integration;

[Category("Retry")]
public sealed class ContainerStartupRetryTests
{
    [Test]
    [Arguments("Bind for 0.0.0.0:32769 failed: port is already allocated")]
    [Arguments("failed to bind host port 0.0.0.0:58162/tcp: address already in use")]
    [Arguments("ports are not available: listen tcp 0.0.0.0:51600: bind: Only one usage of each socket address is normally permitted")]
    public async Task RunAsync_PortBindingCollision_UsesFreshAttempt(string message)
    {
        var attempts = 0;
        var cleanups = 0;

        await ContainerStartupRetry.RunAsync(
            () => ++attempts == 1
                ? Task.FromException(new DockerApiException(
                    HttpStatusCode.InternalServerError,
                    message))
                : Task.CompletedTask,
            () =>
            {
                cleanups++;
                return ValueTask.CompletedTask;
            },
            ContainerStartupRetry.IsPortBindingCollision,
            NoDelayAsync);

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
            ContainerStartupRetry.IsKafkaStartupScriptBusy,
            NoDelayAsync);

        await Assert.That(attempts).IsEqualTo(2);
        await Assert.That(cleanups).IsEqualTo(1);
    }

    [Test]
    public async Task RunAsync_RegistryUnavailable_RetriesWithBackoff()
    {
        var attempts = 0;
        var delays = new List<TimeSpan>();

        await ContainerStartupRetry.RunAsync(
            () => ++attempts < 3
                ? Task.FromException(new DockerApiException(
                    HttpStatusCode.BadGateway,
                    "Head \"https://registry-1.docker.io/v2/apache/kafka/manifests/4.3.1\": received unexpected HTTP status: 502 Bad Gateway"))
                : Task.CompletedTask,
            () => ValueTask.CompletedTask,
            ContainerStartupRetry.IsRegistryUnavailable,
            delay =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        await Assert.That(attempts).IsEqualTo(3);
        await Assert.That(delays).IsEquivalentTo([
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15)
        ]);
    }

    [Test]
    [Arguments(HttpStatusCode.NotFound)]
    [Arguments(HttpStatusCode.InternalServerError)]
    public async Task IsRegistryUnavailable_NonTransientFailure_ReturnsFalse(
        HttpStatusCode statusCode)
    {
        var message = statusCode == HttpStatusCode.NotFound
            ? "Head \"https://registry-1.docker.io/v2/apache/kafka/manifests/missing\": received unexpected HTTP status: 404 Not Found"
            : "Docker daemon returned an unrelated 500 error";

        var exception = new DockerApiException(statusCode, message);

        await Assert.That(ContainerStartupRetry.IsRegistryUnavailable(exception)).IsFalse();
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
            ContainerStartupRetry.IsPortBindingCollision,
            NoDelayAsync);

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
            ContainerStartupRetry.IsPortBindingCollision,
            NoDelayAsync);

        await Assert.That(action).Throws<DockerApiException>();
        await Assert.That(attempts).IsEqualTo(3);
        await Assert.That(cleanups).IsEqualTo(3);
    }

    [Test]
    public async Task KafkaStartupCommand_OverwritesTestcontainersDefault()
    {
        var effectiveCommand = KafkaTestContainer.StartupCommandOverride
            .Compose(["while [ ! -f /testcontainers.sh ]; do sleep 0.1; done; /testcontainers.sh"])
            .ToArray();

        await Assert.That(effectiveCommand).IsEquivalentTo([KafkaTestContainer.StartupCommand]);
        await Assert.That(KafkaTestContainer.StartupCommand).Contains("wc -c");
        await Assert.That(KafkaTestContainer.StartupCommand).Contains("exec /bin/sh /testcontainers.sh");
    }

    private static Task NoDelayAsync(TimeSpan _) => Task.CompletedTask;
}
