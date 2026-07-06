using System.Collections.Concurrent;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerBackgroundLoopStartTests
{
    [Test]
    public async Task StartAutoCommitAsync_ConcurrentCalls_InstallSingleLoop()
    {
        await using var consumer = CreateConsumer();
        var consumerType = typeof(KafkaConsumer<string, string>);
        var startAutoCommit = consumerType.GetMethod(
            "StartAutoCommitAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var taskField = consumerType.GetField(
            "_autoCommitTask",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var ctsField = consumerType.GetField(
            "_autoCommitCts",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        var seenTasks = new ConcurrentDictionary<Task, byte>();
        var seenCts = new ConcurrentDictionary<CancellationTokenSource, byte>();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callers = Enumerable.Range(0, 128)
            .Select(_ => Task.Run(async () =>
            {
                await start.Task.ConfigureAwait(false);
                await ((Task)startAutoCommit.Invoke(consumer, [CancellationToken.None])!).ConfigureAwait(false);

                if (taskField.GetValue(consumer) is Task task)
                    seenTasks.TryAdd(task, 0);
                if (ctsField.GetValue(consumer) is CancellationTokenSource cts)
                    seenCts.TryAdd(cts, 0);
            }))
            .ToArray();

        start.SetResult();
        await Task.WhenAll(callers).WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(seenTasks.Count).IsEqualTo(1);
        await Assert.That(seenCts.Count).IsEqualTo(1);
    }

    [Test]
    public async Task StartPrefetch_ConcurrentCalls_InstallSingleLoop()
    {
        await using var consumer = CreateConsumer();
        var consumerType = typeof(KafkaConsumer<string, string>);
        var startPrefetch = consumerType.GetMethod(
            "StartPrefetch",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var taskField = consumerType.GetField(
            "_prefetchTask",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var ctsField = consumerType.GetField(
            "_prefetchCts",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        var seenTasks = new ConcurrentDictionary<Task, byte>();
        var seenCts = new ConcurrentDictionary<CancellationTokenSource, byte>();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callers = Enumerable.Range(0, 128)
            .Select(_ => Task.Run(async () =>
            {
                await start.Task.ConfigureAwait(false);
                startPrefetch.Invoke(consumer, []);

                if (taskField.GetValue(consumer) is Task task)
                    seenTasks.TryAdd(task, 0);
                if (ctsField.GetValue(consumer) is CancellationTokenSource cts)
                    seenCts.TryAdd(cts, 0);
            }))
            .ToArray();

        start.SetResult();
        await Task.WhenAll(callers).WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(seenTasks.Count).IsEqualTo(1);
        await Assert.That(seenCts.Count).IsEqualTo(1);
    }

    [Test]
    public async Task CloseAsync_SnapshotsLoopStartedAfterCloseFlag()
    {
        await using var consumer = CreateConsumer();

        await VerifyTeardownSnapshotsLoopStartedAfterFlagAsync(
            consumer,
            static c => c.CloseAsync().AsTask(),
            "_closed");
    }

    [Test]
    public async Task DisposeAsync_SnapshotsLoopStartedAfterDisposeFlag()
    {
        var consumer = CreateConsumer();

        try
        {
            await VerifyTeardownSnapshotsLoopStartedAfterFlagAsync(
                consumer,
                static c => c.DisposeAsync().AsTask(),
                "_consumerDisposed");
        }
        finally
        {
            await consumer.DisposeAsync();
        }
    }

    private static async Task VerifyTeardownSnapshotsLoopStartedAfterFlagAsync(
        KafkaConsumer<string, string> consumer,
        Func<KafkaConsumer<string, string>, Task> teardown,
        string flagFieldName)
    {
        var consumerType = typeof(KafkaConsumer<string, string>);
        var flagField = consumerType.GetField(flagFieldName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        var autoCommitLock = consumerType.GetField(
            "_autoCommitStartLock",
            BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(consumer)!;
        var prefetchLock = consumerType.GetField(
            "_prefetchStartLock",
            BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(consumer)!;
        var autoCommitLoop = new InstalledLoop(
            consumerType.GetField("_autoCommitTask", BindingFlags.NonPublic | BindingFlags.Instance)!,
            consumerType.GetField("_autoCommitCts", BindingFlags.NonPublic | BindingFlags.Instance)!);
        var prefetchLoop = new InstalledLoop(
            consumerType.GetField("_prefetchTask", BindingFlags.NonPublic | BindingFlags.Instance)!,
            consumerType.GetField("_prefetchCts", BindingFlags.NonPublic | BindingFlags.Instance)!);

        Task teardownTask;
        lock (autoCommitLock)
        {
            lock (prefetchLock)
            {
                teardownTask = Task.Run(() => teardown(consumer));

                if (!SpinWait.SpinUntil(
                    () => (int)flagField.GetValue(consumer)! != 0,
                    TimeSpan.FromSeconds(5)))
                {
                    Assert.Fail($"{flagFieldName} was not set before teardown tried to snapshot background loops.");
                }

                autoCommitLoop.Install(consumer);
                prefetchLoop.Install(consumer);
            }
        }

        await teardownTask.WaitAsync(TimeSpan.FromSeconds(10));
        await autoCommitLoop.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await prefetchLoop.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(autoCommitLoop.Task.IsCompleted).IsTrue();
        await Assert.That(prefetchLoop.Task.IsCompleted).IsTrue();
    }

    private sealed class InstalledLoop
    {
        private readonly FieldInfo _taskField;
        private readonly FieldInfo _ctsField;
        private readonly CancellationTokenSource _cts = new();

        internal InstalledLoop(FieldInfo taskField, FieldInfo ctsField)
        {
            _taskField = taskField;
            _ctsField = ctsField;
            Task = RunUntilCancelledAsync(CancellationObserved, _cts.Token);
        }

        internal Task Task { get; }

        internal TaskCompletionSource CancellationObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Install(KafkaConsumer<string, string> consumer)
        {
            _ctsField.SetValue(consumer, _cts);
            _taskField.SetValue(consumer, Task);
        }

        private static async Task RunUntilCancelledAsync(
            TaskCompletionSource cancellationObserved,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancellationObserved.SetResult();
            }
        }
    }

    private static KafkaConsumer<string, string> CreateConsumer()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "background-loop-start-test",
            AutoCommitIntervalMs = 60_000
        };

        return new KafkaConsumer<string, string>(
            options,
            Serializers.String,
            Serializers.String);
    }
}
