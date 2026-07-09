namespace Dekaf.Tests.Integration;

internal sealed class CleanupFailureCollector
{
    private readonly List<Exception> _failures = [];

    public async Task CaptureTaskAsync(string operation, Func<Task> cleanup)
    {
        try
        {
            await cleanup().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AddFailure(operation, ex);
        }
    }

    public async ValueTask CaptureValueTaskAsync(string operation, Func<ValueTask> cleanup)
    {
        try
        {
            await cleanup().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AddFailure(operation, ex);
        }
    }

    public void Capture(string operation, Action cleanup)
    {
        try
        {
            cleanup();
        }
        catch (Exception ex)
        {
            AddFailure(operation, ex);
        }
    }

    public void WriteFailuresToConsole()
    {
        foreach (var failure in _failures)
        {
            Console.WriteLine($"[Cleanup] {failure}");
        }
    }

    public void ThrowIfAny()
    {
        if (_failures.Count > 0)
        {
            throw new AggregateException("One or more cleanup operations failed.", _failures);
        }
    }

    private void AddFailure(string operation, Exception exception) =>
        _failures.Add(new InvalidOperationException($"{operation} failed.", exception));
}
