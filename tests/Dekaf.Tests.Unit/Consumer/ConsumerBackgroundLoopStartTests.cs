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
