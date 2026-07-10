namespace Dekaf.Tests.Integration;

[Category("Retry")]
public sealed class ContainerImageBootstrapCoordinatorTests
{
    [Test]
    public async Task RunAsync_ColdStart_BlocksFollowersUntilBootstrapCompletes()
    {
        var coordinator = new ContainerImageBootstrapCoordinator();
        var bootstrapStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBootstrap = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startupCount = 0;

        Task StartAsync()
        {
            if (Interlocked.Increment(ref startupCount) == 1)
                bootstrapStarted.SetResult();

            return releaseBootstrap.Task;
        }

        var startupTasks = Enumerable.Range(0, 3)
            .Select(_ => coordinator.RunAsync(StartAsync))
            .ToArray();

        await bootstrapStarted.Task;
        await Assert.That(startupCount).IsEqualTo(1);

        releaseBootstrap.SetResult();
        await Task.WhenAll(startupTasks);

        await Assert.That(startupCount).IsEqualTo(3);
    }

    [Test]
    public async Task RunAsync_AfterBootstrap_RunsStartupsConcurrently()
    {
        var coordinator = new ContainerImageBootstrapCoordinator();
        await coordinator.RunAsync(() => Task.CompletedTask);

        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStartups = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startupCount = 0;

        Task StartAsync()
        {
            if (Interlocked.Increment(ref startupCount) == 2)
                bothStarted.SetResult();

            return releaseStartups.Task;
        }

        var first = coordinator.RunAsync(StartAsync);
        var second = coordinator.RunAsync(StartAsync);

        await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        releaseStartups.SetResult();
        await Task.WhenAll(first, second);

        await Assert.That(startupCount).IsEqualTo(2);
    }

    [Test]
    public async Task RunAsync_FailedBootstrap_AllowsNextStartupToRetry()
    {
        var coordinator = new ContainerImageBootstrapCoordinator();
        var startupCount = 0;

        var failedBootstrap = async () => await coordinator.RunAsync(() =>
        {
            startupCount++;
            return Task.FromException(new InvalidOperationException("bootstrap failed"));
        });

        await Assert.That(failedBootstrap).Throws<InvalidOperationException>();

        await coordinator.RunAsync(() =>
        {
            startupCount++;
            return Task.CompletedTask;
        });

        await Assert.That(startupCount).IsEqualTo(2);
    }
}
