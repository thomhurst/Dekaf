using System.Reflection;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public class RecordAccumulatorDequeCacheTests
{
    private static readonly MethodInfo GetOrCreateDequeMethod = typeof(RecordAccumulator).GetMethod(
        "GetOrCreateDeque",
        BindingFlags.NonPublic | BindingFlags.Instance,
        [typeof(string), typeof(int)])!;
    private static readonly FieldInfo ThreadCacheField = typeof(RecordAccumulator).GetField(
        "t_partitionDequeCache",
        BindingFlags.NonPublic | BindingFlags.Static)!;

    [Test]
    [Timeout(30_000)]
    public async Task GetOrCreateDeque_ConcurrentCollidingPartitions_ReturnCorrectDeques(
        CancellationToken cancellationToken)
    {
        await using var accumulator = CreateAccumulator();
        using var start = new ManualResetEventSlim();
        object? partition0Deque = null;
        object? partition16Deque = null;

        var partition0Thread = StartLookupThread(
            accumulator,
            partition: 0,
            start,
            deque => partition0Deque = deque);
        var partition16Thread = StartLookupThread(
            accumulator,
            partition: 16,
            start,
            deque => partition16Deque = deque);

        start.Set();
        JoinThread(partition0Thread, cancellationToken);
        JoinThread(partition16Thread, cancellationToken);

        await Assert.That(partition0Deque).IsNotNull();
        await Assert.That(partition16Deque).IsNotNull();
        await Assert.That(partition0Deque).IsNotSameReferenceAs(partition16Deque);
        await Assert.That(GetPartition(partition0Deque!)).IsEqualTo(0);
        await Assert.That(GetPartition(partition16Deque!)).IsEqualTo(16);
    }

    [Test]
    [Timeout(30_000)]
    public async Task GetOrCreateDeque_ReusedWorkerCachesNewLiveOwner(CancellationToken cancellationToken)
    {
        await using var firstAccumulator = CreateAccumulator();
        await using var secondAccumulator = CreateAccumulator();
        var firstCached = false;
        var secondCached = false;

        var thread = new Thread(() =>
        {
            var first = GetOrCreateDeque(firstAccumulator, "orders", partition: 0);
            var second = GetOrCreateDeque(secondAccumulator, "orders", partition: 16);
            firstCached = IsCachedForCurrentThread(first);
            secondCached = IsCachedForCurrentThread(second);
        })
        {
            IsBackground = true
        };
        thread.Start();
        JoinThread(thread, cancellationToken);

        await Assert.That(firstCached).IsTrue();
        await Assert.That(secondCached).IsTrue();
    }

    [Test]
    public async Task GetOrCreateDeque_SamePartitionAcrossTopics_ReturnsTopicSpecificDeque()
    {
        await using var accumulator = CreateAccumulator();

        var orders = GetOrCreateDeque(accumulator, "orders", partition: 3);
        var payments = GetOrCreateDeque(accumulator, "payments", partition: 3);
        var ordersAgain = GetOrCreateDeque(accumulator, "orders", partition: 3);

        await Assert.That(ordersAgain).IsSameReferenceAs(orders);
        await Assert.That(payments).IsNotSameReferenceAs(orders);
        await Assert.That(GetTopic(orders)).IsEqualTo("orders");
        await Assert.That(GetTopic(payments)).IsEqualTo("payments");
    }

    [Test]
    public async Task GetOrCreateDeque_AlternatingAccumulators_PreservesInstanceOwnership()
    {
        await using var firstAccumulator = CreateAccumulator();
        await using var secondAccumulator = CreateAccumulator();

        var first = GetOrCreateDeque(firstAccumulator, "orders", partition: 5);
        var second = GetOrCreateDeque(secondAccumulator, "orders", partition: 5);
        var firstAgain = GetOrCreateDeque(firstAccumulator, "orders", partition: 5);
        var secondAgain = GetOrCreateDeque(secondAccumulator, "orders", partition: 5);

        await Assert.That(firstAgain).IsSameReferenceAs(first);
        await Assert.That(secondAgain).IsSameReferenceAs(second);
        await Assert.That(first).IsNotSameReferenceAs(second);
    }

    private static RecordAccumulator CreateAccumulator()
        => new(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BufferMemory = ulong.MaxValue,
            BatchSize = 1_048_576,
            LingerMs = 0
        });

    private static Thread StartLookupThread(
        RecordAccumulator accumulator,
        int partition,
        ManualResetEventSlim start,
        Action<object> setResult)
    {
        var thread = new Thread(() =>
        {
            start.Wait();
            object? deque = null;
            for (var i = 0; i < 1_000; i++)
                deque = GetOrCreateDeque(accumulator, "orders", partition);

            setResult(deque!);
        })
        {
            IsBackground = true
        };
        thread.Start();
        return thread;
    }

    private static object GetOrCreateDeque(RecordAccumulator accumulator, string topic, int partition)
        => GetOrCreateDequeMethod.Invoke(accumulator, [topic, partition])!;

    private static void JoinThread(Thread thread, CancellationToken cancellationToken)
    {
        while (!thread.Join(millisecondsTimeout: 50))
            cancellationToken.ThrowIfCancellationRequested();
    }

    private static int GetPartition(object deque)
        => (int)deque.GetType().GetField("Partition")!.GetValue(deque)!;

    private static string GetTopic(object deque)
        => (string)deque.GetType().GetField("Topic")!.GetValue(deque)!;

    private static bool IsCachedForCurrentThread(object deque)
    {
        var cache = (Array?)ThreadCacheField.GetValue(null);
        if (cache is null)
            return false;

        for (var i = 0; i < cache.Length; i++)
        {
            var weakEntry = cache.GetValue(i);
            if (weakEntry is null)
                continue;

            object?[] arguments = [null];
            var found = (bool)weakEntry.GetType().GetMethod("TryGetTarget")!.Invoke(weakEntry, arguments)!;
            if (found && ReferenceEquals(arguments[0], deque))
                return true;
        }

        return false;
    }
}
