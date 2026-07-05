#if NETSTANDARD2_0
namespace System.Threading
{
    internal sealed class Lock
    {
        private readonly object _gate = new();

        public Scope EnterScope()
        {
            Monitor.Enter(_gate);
            return new Scope(_gate);
        }

        public readonly ref struct Scope
        {
            private readonly object _gate;

            internal Scope(object gate)
            {
                _gate = gate;
            }

            public void Dispose()
            {
                Monitor.Exit(_gate);
            }
        }
    }

    internal sealed class PeriodicTimer : IDisposable
    {
        private readonly TimeSpan _period;
        private readonly CancellationTokenSource _disposed = new();

        public PeriodicTimer(TimeSpan period)
        {
            _period = period;
        }

        public async ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken = default)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposed.Token);
            try
            {
                await Task.Delay(_period, linked.Token).ConfigureAwait(false);
                return !_disposed.IsCancellationRequested;
            }
            catch (OperationCanceledException) when (_disposed.IsCancellationRequested)
            {
                return false;
            }
        }

        public void Dispose()
        {
            _disposed.Cancel();
            _disposed.Dispose();
        }
    }

    internal static class CancellationTokenSourceCompatibilityExtensions
    {
        public static Task CancelAsync(this CancellationTokenSource source)
        {
            source.Cancel();
            return Task.CompletedTask;
        }
    }
}

namespace System.Threading.Tasks
{
    internal sealed class TaskCompletionSource
    {
        private readonly TaskCompletionSource<bool> _inner;

        public TaskCompletionSource()
            : this(TaskCreationOptions.None)
        {
        }

        public TaskCompletionSource(TaskCreationOptions creationOptions)
        {
            _inner = new TaskCompletionSource<bool>(creationOptions);
        }

        public Task Task => _inner.Task;

        public void SetResult()
        {
            _inner.SetResult(true);
        }

        public bool TrySetResult()
        {
            return _inner.TrySetResult(true);
        }

        public bool TrySetCanceled()
        {
            return _inner.TrySetCanceled();
        }

        public bool TrySetCanceled(CancellationToken cancellationToken)
        {
            return _inner.TrySetCanceled(cancellationToken);
        }

        public bool TrySetException(Exception exception)
        {
            return _inner.TrySetException(exception);
        }

        public bool TrySetException(IEnumerable<Exception> exceptions)
        {
            return _inner.TrySetException(exceptions);
        }
    }

    internal static class TaskWaitAsyncCompatibilityExtensions
    {
        public static Task WaitAsync(this Task task, CancellationToken cancellationToken)
            => WaitAsyncCore(task, Timeout.InfiniteTimeSpan, hasTimeout: false, cancellationToken);

        public static Task WaitAsync(this Task task, TimeSpan timeout)
            => WaitAsyncCore(task, timeout, hasTimeout: true, CancellationToken.None);

        public static Task WaitAsync(this Task task, TimeSpan timeout, CancellationToken cancellationToken)
            => WaitAsyncCore(task, timeout, hasTimeout: true, cancellationToken);

        public static async Task<TResult> WaitAsync<TResult>(this Task<TResult> task, CancellationToken cancellationToken)
        {
            await WaitAsync((Task)task, cancellationToken).ConfigureAwait(false);
            return await task.ConfigureAwait(false);
        }

        public static async Task<TResult> WaitAsync<TResult>(this Task<TResult> task, TimeSpan timeout)
        {
            await WaitAsync((Task)task, timeout).ConfigureAwait(false);
            return await task.ConfigureAwait(false);
        }

        public static async Task<TResult> WaitAsync<TResult>(
            this Task<TResult> task,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            await WaitAsync((Task)task, timeout, cancellationToken).ConfigureAwait(false);
            return await task.ConfigureAwait(false);
        }

        private static async Task WaitAsyncCore(
            Task task,
            TimeSpan timeout,
            bool hasTimeout,
            CancellationToken cancellationToken)
        {
            if (task.IsCompleted)
            {
                await task.ConfigureAwait(false);
                return;
            }

            using var timeoutCts = hasTimeout ? new CancellationTokenSource() : null;
            using var linkedCts = CreateLinkedCancellationTokenSource(timeoutCts, hasTimeout, cancellationToken);
            var delay = Task.Delay(hasTimeout ? timeout : Timeout.InfiniteTimeSpan, linkedCts.Token);

            var completed = await Task.WhenAny(task, delay).ConfigureAwait(false);
            if (ReferenceEquals(completed, task))
            {
                timeoutCts?.Cancel();
                await task.ConfigureAwait(false);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException();
        }

        private static CancellationTokenSource CreateLinkedCancellationTokenSource(
            CancellationTokenSource? timeoutCts,
            bool hasTimeout,
            CancellationToken cancellationToken)
        {
            if (hasTimeout && cancellationToken.CanBeCanceled)
            {
                return CancellationTokenSource.CreateLinkedTokenSource(timeoutCts!.Token, cancellationToken);
            }

            if (hasTimeout)
            {
                return CancellationTokenSource.CreateLinkedTokenSource(timeoutCts!.Token);
            }

            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }
    }
}
#endif
