namespace Dekaf.Tests.Integration;

internal sealed class ContainerImageBootstrapCoordinator
{
    private readonly SemaphoreSlim _bootstrapGate = new(1, 1);
    private bool _bootstrapCompleted;

    internal async Task RunAsync(Func<Task> startupAsync)
    {
        ArgumentNullException.ThrowIfNull(startupAsync);

        if (Volatile.Read(ref _bootstrapCompleted))
        {
            await startupAsync().ConfigureAwait(false);
            return;
        }

        await _bootstrapGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!Volatile.Read(ref _bootstrapCompleted))
            {
                await startupAsync().ConfigureAwait(false);
                Volatile.Write(ref _bootstrapCompleted, true);
                return;
            }
        }
        finally
        {
            _bootstrapGate.Release();
        }

        await startupAsync().ConfigureAwait(false);
    }
}
