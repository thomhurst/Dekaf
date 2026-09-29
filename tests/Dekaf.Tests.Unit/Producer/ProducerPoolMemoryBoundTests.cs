using System.Runtime.CompilerServices;
using Dekaf.Producer;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Idle producer batch storage must stay bounded by the memory the process can spend on it.
/// Every pooled <see cref="PartitionBatch"/> keeps a pinned arena of BatchSize + 12.5%
/// (~1.1MB by default) that is allocated without zeroing, so an unbounded pool shows up as
/// <see cref="OutOfMemoryException"/> from the GC heap limit while container RSS stays low.
/// </summary>
public class ProducerPoolMemoryBoundTests
{
    private const long MiB = 1024 * 1024;
    private const long GiB = 1024 * MiB;
    private const int DefaultBatchSize = 1_048_576;
    private static readonly int DefaultArenaCapacity =
        ProducerOptions.GetEffectiveArenaCapacity(DefaultBatchSize, arenaCapacity: 0);

    #region Pool sizing

    [Test]
    public async Task ComputePoolSize_384MiContainer_KeepsIdleArenasWithinTenthOfHeapLimit()
    {
        // The reported deployment: 384Mi memory limit, so the GC heap limit is 75% = 288MiB,
        // and the auto-tuned BufferMemory is the 32MiB producer floor.
        const long heapLimit = 288 * MiB;
        var options = CreateOptions(bufferMemory: 32 * MiB);

        var poolSize = RecordAccumulator.ComputePoolSize(options, heapLimit);

        await Assert.That(poolSize).IsEqualTo(25);
        await Assert.That((long)poolSize * DefaultArenaCapacity)
            .IsLessThanOrEqualTo(heapLimit / RecordAccumulator.RetainedMemoryDivisor);
    }

    [Test]
    public async Task ComputePoolSize_384MiContainer_WithExplicitLargeBufferMemory_IsStillBoundedByHeapLimit()
    {
        // WithBufferMemory(256MB) in a small pod must not restore the 128 × 1.1MB pool.
        const long heapLimit = 288 * MiB;
        var options = CreateOptions(bufferMemory: 256 * MiB);

        var poolSize = RecordAccumulator.ComputePoolSize(options, heapLimit);

        await Assert.That(poolSize).IsEqualTo(25);
    }

    [Test]
    public async Task ComputePoolSize_AmpleMemory_IsBoundedByBufferMemory()
    {
        var options = CreateOptions(bufferMemory: 32 * MiB);

        var poolSize = RecordAccumulator.ComputePoolSize(options, availableMemoryBytes: 64 * GiB);

        // 32MiB / ~1.1MiB arenas.
        await Assert.That(poolSize).IsEqualTo(28);
        await Assert.That((long)poolSize * DefaultArenaCapacity).IsLessThanOrEqualTo(32 * MiB);
    }

    [Test]
    [Arguments(32L * 1024 * 1024, 28)]
    [Arguments(256L * 1024 * 1024, 128)]
    [Arguments(1024L * 1024 * 1024, 256)]
    public async Task ComputePoolSize_UnknownAvailableMemory_AppliesBufferMemoryBoundOnly(
        long bufferMemory,
        int expectedPoolSize)
    {
        var options = CreateOptions(bufferMemory);

        var poolSize = RecordAccumulator.ComputePoolSize(options, availableMemoryBytes: 0);

        await Assert.That(poolSize).IsEqualTo(expectedPoolSize);
    }

    [Test]
    [Arguments(40L * 1024 * 1024, 3)]
    [Arguments(20L * 1024 * 1024, 1)]
    [Arguments(1L, 1)]
    public async Task ComputePoolSize_TinyMemory_KeepsAtLeastOneArena(long availableMemoryBytes, int expectedPoolSize)
    {
        var options = CreateOptions(bufferMemory: 256 * MiB);

        var poolSize = RecordAccumulator.ComputePoolSize(options, availableMemoryBytes);

        await Assert.That(poolSize).IsEqualTo(expectedPoolSize);
    }

    [Test]
    public async Task ComputePoolSize_LargeArenaInSmallContainer_RetainsNoMoreThanTheBudget()
    {
        // A 16MB batch in a 384Mi pod: the ~29MB budget holds one 18MB arena, not a floor of four.
        const long heapLimit = 288 * MiB;
        var options = CreateOptions(bufferMemory: 256 * MiB, batchSize: 16 * 1024 * 1024);
        var arena = ProducerOptions.GetEffectiveArenaCapacity(options.BatchSize, 0);

        var poolSize = RecordAccumulator.ComputePoolSize(options, heapLimit);

        await Assert.That(poolSize).IsEqualTo(1);
        await Assert.That((long)poolSize * arena)
            .IsLessThanOrEqualTo(heapLimit / RecordAccumulator.RetainedMemoryDivisor);
    }

    [Test]
    public async Task ComputePoolSize_ExplicitArenaCapacity_CountsTheLargerArena()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = DefaultBatchSize,
            ArenaCapacity = 4 * 1024 * 1024,
            BufferMemory = 64 * MiB,
        };

        var poolSize = RecordAccumulator.ComputePoolSize(options, availableMemoryBytes: 64 * GiB);

        await Assert.That(poolSize).IsEqualTo(16);
    }

    [Test]
    [Arguments(BufferMemoryAllocationStrategy.Full, 0, 25)]
    [Arguments(BufferMemoryAllocationStrategy.Incremental, 0, BatchArena.DefaultPoolSize)]
    // Incremental never allocates an arena, so an explicit ArenaCapacity must not shrink its pool.
    [Arguments(BufferMemoryAllocationStrategy.Incremental, 64 * 1024 * 1024, BatchArena.DefaultPoolSize)]
    public async Task ComputePoolSize_OnlyFullArenasCountAgainstTheMemoryBound(
        BufferMemoryAllocationStrategy strategy,
        int arenaCapacity,
        int expectedPoolSize)
    {
        // Incremental batches return their chunks before pooling, so a pooled batch keeps no
        // batch storage for the arena bound to limit.
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = DefaultBatchSize,
            ArenaCapacity = arenaCapacity,
            BufferMemory = 32 * MiB,
            BufferMemoryAllocationStrategy = strategy,
        };

        var poolSize = RecordAccumulator.ComputePoolSize(options, availableMemoryBytes: 288 * MiB);

        await Assert.That(poolSize).IsEqualTo(expectedPoolSize);
    }

    [Test]
    public async Task ComputePoolSize_SmallBatches_KeepChurnSizing()
    {
        // 16KB batches need the large churn pool; the memory bound (256MB / 18KB) is far above it.
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 16_384,
            BufferMemory = 256 * MiB,
        };

        var poolSize = RecordAccumulator.ComputePoolSize(options, availableMemoryBytes: 4 * GiB);

        await Assert.That(poolSize).IsEqualTo(BatchArena.MaxPoolSizeCap);
    }

    [Test]
    public async Task ComputePoolSize_AcrossConfigurations_HonorsEveryBound()
    {
        long[] availableMemory = [0, 1, 20 * MiB, 96 * MiB, 288 * MiB, 768 * MiB, 2 * GiB, 16 * GiB, 256 * GiB];
        long[] bufferMemory = [1, 1 * MiB, 32 * MiB, 64 * MiB, 256 * MiB, 1 * GiB, 8 * GiB];
        int[] batchSizes = [1, 1024, 16_384, 262_144, DefaultBatchSize, 2_097_152, 16 * 1024 * 1024, 1 << 30];
        var violations = new List<string>();

        foreach (var available in availableMemory)
        foreach (var buffer in bufferMemory)
        foreach (var batchSize in batchSizes)
        {
            var options = new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                BatchSize = batchSize,
                BufferMemory = (ulong)buffer,
            };
            var arena = ProducerOptions.GetEffectiveArenaCapacity(batchSize, 0);
            var poolSize = RecordAccumulator.ComputePoolSize(options, available);
            var memoryBound = RecordAccumulator.ComputeMemoryBoundedPoolSize(options, available);
            var retainedBudget = Math.Max(
                available > 0 ? Math.Min(buffer, available / RecordAccumulator.RetainedMemoryDivisor) : buffer,
                arena);
            var label = $"available={available} buffer={buffer} batch={batchSize} pool={poolSize}";

            if (poolSize < RecordAccumulator.MinimumPoolSize || poolSize > BatchArena.MaxPoolSizeCap)
                violations.Add($"{label}: outside [{RecordAccumulator.MinimumPoolSize}, {BatchArena.MaxPoolSizeCap}]");
            if (poolSize > memoryBound)
                violations.Add($"{label}: above memory bound {memoryBound}");
            if ((long)poolSize * arena > retainedBudget)
                violations.Add($"{label}: retains {(long)poolSize * arena} bytes over budget {retainedBudget}");
            if (RecordAccumulator.ComputeRetainedArenaBytes(options, available) != retainedBudget)
                violations.Add($"{label}: byte budget {RecordAccumulator.ComputeRetainedArenaBytes(options, available)} != {retainedBudget}");
        }

        await Assert.That(violations).IsEmpty();
    }

    [Test]
    public async Task ComputePoolSize_MoreMemoryNeverShrinksThePool()
    {
        long[] availableMemory = [1, 20 * MiB, 96 * MiB, 288 * MiB, 768 * MiB, 2 * GiB, 16 * GiB, 256 * GiB];
        long[] bufferMemory = [1 * MiB, 32 * MiB, 256 * MiB, 1 * GiB, 8 * GiB];
        int[] batchSizes = [16_384, 262_144, DefaultBatchSize, 16 * 1024 * 1024];
        var violations = new List<string>();

        foreach (var buffer in bufferMemory)
        foreach (var batchSize in batchSizes)
        {
            var previous = 0;
            foreach (var available in availableMemory)
            {
                var poolSize = RecordAccumulator.ComputePoolSize(
                    CreateOptions(buffer, batchSize),
                    available);
                if (poolSize < previous)
                    violations.Add($"buffer={buffer} batch={batchSize} available={available}: {poolSize} < {previous}");
                previous = poolSize;
            }
        }

        await Assert.That(violations).IsEmpty();
    }

    [Test]
    public async Task RecordAccumulator_SizesItsBatchPoolWithTheMemoryBound()
    {
        // 8MiB of BufferMemory holds seven ~1.1MiB arenas; every test host has far more
        // than 80MiB available, so BufferMemory is the binding bound.
        var options = CreateOptions(bufferMemory: 8 * MiB);

        await using var accumulator = new RecordAccumulator(options);

        await Assert.That(accumulator.BatchPoolMaxSizeForTest).IsEqualTo(7);
        await Assert.That(accumulator.BatchPoolMaxSizeForTest)
            .IsEqualTo(RecordAccumulator.ComputePoolSize(options));
        await Assert.That(BatchArena.PoolCapacity).IsGreaterThanOrEqualTo(7);
        await Assert.That(BatchArena.MissRatchetLimit).IsGreaterThanOrEqualTo(7);
        await Assert.That(BatchArena.RetainedByteLimit)
            .IsGreaterThanOrEqualTo(RecordAccumulator.ComputeRetainedArenaBytes(options, availableMemoryBytes: 0));
    }

    [Test]
    public async Task RecordAccumulator_Incremental_KeepsChurnSizedBatchPool()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = DefaultBatchSize,
            BufferMemory = 8 * MiB,
            BufferMemoryAllocationStrategy = BufferMemoryAllocationStrategy.Incremental,
        };

        await using var accumulator = new RecordAccumulator(options);

        await Assert.That(accumulator.BatchPoolMaxSizeForTest).IsEqualTo(BatchArena.DefaultPoolSize);
    }

    #endregion

    #region Retained byte budget

    [Test]
    public async Task RetainedByteBudget_MixedArenaSizes_StayWithinTheByteLimit()
    {
        // A 16KB-batch producer can raise the shared arena pool to 512 slots. A 1MB-batch
        // producer returning its arenas into those slots must still stop at the byte budget.
        const long budget = 29 * MiB;
        var retained = new RetainedByteBudget();
        retained.SetLimit(budget);
        var accepted = 0;

        for (var i = 0; i < 512; i++)
        {
            if (retained.TryReserve(DefaultArenaCapacity))
                accepted++;
        }

        for (var i = 0; i < 512; i++)
            retained.TryReserve(18_432);

        await Assert.That(accepted).IsEqualTo((int)(budget / DefaultArenaCapacity));
        await Assert.That(retained.RetainedBytes).IsLessThanOrEqualTo(budget);
    }

    [Test]
    public async Task RetainedByteBudget_WithoutLimit_TracksBytesAndAcceptsEverything()
    {
        var retained = new RetainedByteBudget();

        var accepted = retained.TryReserve(DefaultArenaCapacity) && retained.TryReserve(DefaultArenaCapacity);

        await Assert.That(accepted).IsTrue();
        await Assert.That(retained.Limit).IsEqualTo(0);
        await Assert.That(retained.RetainedBytes).IsEqualTo(2L * DefaultArenaCapacity);
    }

    [Test]
    public async Task RetainedByteBudget_Release_MakesRoomForTheNextItem()
    {
        var retained = new RetainedByteBudget();
        retained.SetLimit(DefaultArenaCapacity);

        var first = retained.TryReserve(DefaultArenaCapacity);
        var whileFull = retained.TryReserve(DefaultArenaCapacity);
        retained.Release(DefaultArenaCapacity);
        var afterRelease = retained.TryReserve(DefaultArenaCapacity);

        await Assert.That(first).IsTrue();
        await Assert.That(whileFull).IsFalse();
        await Assert.That(afterRelease).IsTrue();
        await Assert.That(retained.RetainedBytes).IsEqualTo(DefaultArenaCapacity);
    }

    [Test]
    public async Task RetainedByteBudget_LoweredLimit_RejectsReservationsUntilReleasesFit()
    {
        // A disposed producer with a large budget leaves a smaller one: later returns must
        // honor the smaller budget even though more bytes are still reserved.
        var retained = new RetainedByteBudget();
        retained.SetLimit(4L * DefaultArenaCapacity);
        for (var i = 0; i < 4; i++)
            retained.TryReserve(DefaultArenaCapacity);

        retained.SetLimit(2L * DefaultArenaCapacity);
        var whileOver = retained.TryReserve(DefaultArenaCapacity);
        retained.Release(DefaultArenaCapacity);
        retained.Release(DefaultArenaCapacity);
        retained.Release(DefaultArenaCapacity);
        var afterReleases = retained.TryReserve(DefaultArenaCapacity);

        await Assert.That(retained.Limit).IsEqualTo(2L * DefaultArenaCapacity);
        await Assert.That(whileOver).IsFalse();
        await Assert.That(afterReleases).IsTrue();
        await Assert.That(retained.RetainedBytes).IsEqualTo(2L * DefaultArenaCapacity);
    }

    #endregion

    #region Live producer limits

    private static readonly ArenaPoolLimit IdleLimit = new(1, 1, RetainedBytes: 0);
    private static readonly ArenaPoolLimit DrainedLimit = new(1, 1, RetainedByteBudget.RetainNone);

    private static ArenaPoolLimits CreateLimits() => new(IdleLimit, DrainedLimit);

    [Test]
    public async Task ArenaPoolLimits_EffectiveLimit_IsTheLargestLiveRequest()
    {
        var limits = CreateLimits();
        var large = new ArenaPoolLimit(128, 512, 256 * MiB);
        var small = new ArenaPoolLimit(7, 7, 8 * MiB);

        limits.Register(large, out var beforeLarge, out var afterLarge);
        limits.Register(small, out var beforeSmall, out var afterSmall);

        await Assert.That(beforeLarge).IsEqualTo(IdleLimit);
        await Assert.That(afterLarge).IsEqualTo(large);
        await Assert.That(beforeSmall).IsEqualTo(large);
        await Assert.That(afterSmall).IsEqualTo(large);
        await Assert.That(limits.Current).IsEqualTo(large);
    }

    [Test]
    public async Task ArenaPoolLimits_DisposingTheLargestProducer_FallsBackToTheRemainingRequest()
    {
        // The review scenario: a default producer, then an 8MiB one; disposing the default
        // producer must let the 8MiB bound take effect.
        var limits = CreateLimits();
        var large = limits.Register(new ArenaPoolLimit(128, 512, 256 * MiB), out _, out _);
        var small = new ArenaPoolLimit(7, 7, 8 * MiB);
        limits.Register(small, out _, out _);

        var removed = limits.Unregister(large, out var previous, out var current);

        await Assert.That(removed).IsTrue();
        await Assert.That(previous.RetainedBytes).IsEqualTo(256 * MiB);
        await Assert.That(current).IsEqualTo(small);
        await Assert.That(BatchArena.ShouldReleasePooledArenas(previous.RetainedBytes, current.RetainedBytes)).IsTrue();
    }

    [Test]
    public async Task ArenaPoolLimits_EachFieldTakesItsOwnMaximum()
    {
        var limits = CreateLimits();
        limits.Register(new ArenaPoolLimit(128, 128, 8 * MiB), out _, out _);
        limits.Register(new ArenaPoolLimit(16, 512, 64 * MiB), out _, out _);

        await Assert.That(limits.Current).IsEqualTo(new ArenaPoolLimit(128, 512, 64 * MiB));
    }

    [Test]
    public async Task ArenaPoolLimits_LastUnregister_KeepsNoArenas()
    {
        var limits = CreateLimits();
        var registration = limits.Register(new ArenaPoolLimit(25, 25, 29 * MiB), out _, out _);

        var removed = limits.Unregister(registration, out var previous, out var current);
        var removedAgain = limits.Unregister(registration, out _, out var afterSecond);

        await Assert.That(removed).IsTrue();
        await Assert.That(previous.RetainedBytes).IsEqualTo(29 * MiB);
        // Not the unbounded idle limit: an arena returned after the last disposal must not be pooled.
        await Assert.That(current).IsEqualTo(DrainedLimit);
        await Assert.That(removedAgain).IsFalse();
        await Assert.That(afterSecond).IsEqualTo(DrainedLimit);
        await Assert.That(BatchArena.ShouldReleasePooledArenas(previous.RetainedBytes, current.RetainedBytes)).IsTrue();
    }

    [Test]
    public async Task ArenaPoolLimits_ConcurrentRegistrations_EndDrained(CancellationToken cancellationToken)
    {
        var limits = CreateLimits();

        await Task.WhenAll(Enumerable.Range(1, 32).Select(i => Task.Run(() =>
        {
            for (var j = 0; j < 500; j++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var registration = limits.Register(new ArenaPoolLimit(i, i, i * MiB), out _, out var current);
                if (current.RetainedBytes < i * MiB)
                    throw new InvalidOperationException("Effective limit fell below a live request.");
                limits.Unregister(registration, out _, out _);
            }
        }, cancellationToken)));

        await Assert.That(limits.Current).IsEqualTo(DrainedLimit);
    }

    [Test]
    public async Task ArenaPoolLimits_BeforeAnyRegistration_IsIdle()
    {
        await Assert.That(CreateLimits().Current).IsEqualTo(IdleLimit);
    }

    [Test]
    public async Task RetainedByteBudget_RetainNone_RejectsEveryNonEmptyItem()
    {
        var retained = new RetainedByteBudget();
        retained.SetLimit(RetainedByteBudget.RetainNone);

        var arena = retained.TryReserve(DefaultArenaCapacity);
        var oneByte = retained.TryReserve(1);
        var empty = retained.TryReserve(0);

        await Assert.That(arena).IsFalse();
        await Assert.That(oneByte).IsFalse();
        await Assert.That(empty).IsTrue();
        await Assert.That(retained.RetainedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task RegisterThen_FailureAfterRegistration_DisposesTheRegistration()
    {
        // A producer whose construction fails after registering (for example, pre-warming runs
        // out of memory) must not leave its allowance in the process-wide pool.
        var registration = new TrackingDisposable();

        var thrown = await Assert.That(() => RecordAccumulator.RegisterThen(
                () => registration,
                () => throw new InvalidOperationException("pre-warm")))
            .Throws<InvalidOperationException>();

        await Assert.That(thrown!.Message).IsEqualTo("pre-warm");
        await Assert.That(registration.DisposeCount).IsEqualTo(1);
    }

    [Test]
    public async Task RegisterThen_Success_KeepsTheRegistration()
    {
        var registration = new TrackingDisposable();
        var ran = false;

        var returned = RecordAccumulator.RegisterThen(() => registration, () => ran = true);

        await Assert.That(ran).IsTrue();
        await Assert.That(ReferenceEquals(returned, registration)).IsTrue();
        await Assert.That(registration.DisposeCount).IsEqualTo(0);
    }

    [Test]
    public async Task RecordAccumulator_InvalidOptions_LeavesNoRegistration()
    {
        // Validation throws before registration; nothing to release.
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = DefaultBatchSize,
            ArenaCapacity = 1,
        };

        await Assert.That(() => new RecordAccumulator(options)).Throws<InvalidOperationException>();
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }

    [Test]
    [Arguments(0L, 29L * 1024 * 1024, true)]                  // first producer bounds a previously unbounded pool
    [Arguments(29L * 1024 * 1024, 29L * 1024 * 1024, false)]  // same effective budget keeps pooled arenas
    [Arguments(8L * 1024 * 1024, 256L * 1024 * 1024, false)]  // a larger budget keeps pooled arenas
    [Arguments(256L * 1024 * 1024, 8L * 1024 * 1024, true)]   // the largest producer was disposed
    [Arguments(29L * 1024 * 1024, RetainedByteBudget.RetainNone, true)] // the last producer was disposed
    [Arguments(RetainedByteBudget.RetainNone, 29L * 1024 * 1024, false)] // a new producer after drain
    [Arguments(0L, 0L, true)]
    public async Task ShouldReleasePooledArenas_OnlyKeepsArenasWhenTheBudgetDidNotFall(
        long previousLimit,
        long currentLimit,
        bool expected)
    {
        await Assert.That(BatchArena.ShouldReleasePooledArenas(previousLimit, currentLimit)).IsEqualTo(expected);
    }

    [Test]
    public async Task RecordAccumulator_Dispose_ReleasesItsArenaPoolRegistration()
    {
        var accumulator = new RecordAccumulator(CreateOptions(bufferMemory: 8 * MiB));
        var registration = accumulator.ArenaPoolRegistrationForTest;

        await Assert.That(registration).IsNotNull();
        await Assert.That(registration!.IsDisposed).IsFalse();

        await accumulator.DisposeAsync();

        await Assert.That(registration.IsDisposed).IsTrue();
    }

    [Test]
    public async Task RecordAccumulator_Incremental_DoesNotRegisterArenaLimits()
    {
        await using var accumulator = new RecordAccumulator(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = DefaultBatchSize,
            BufferMemory = 8 * MiB,
            BufferMemoryAllocationStrategy = BufferMemoryAllocationStrategy.Incremental,
        });

        await Assert.That(accumulator.ArenaPoolRegistrationForTest).IsNull();
    }

    [Test]
    public async Task RetainedByteBudget_ZeroByteItems_AreAlwaysAccepted()
    {
        var retained = new RetainedByteBudget();
        retained.SetLimit(1);
        retained.TryReserve(1);

        var accepted = retained.TryReserve(0);
        retained.Release(0);

        await Assert.That(accepted).IsTrue();
        await Assert.That(retained.RetainedBytes).IsEqualTo(1);
    }

    [Test]
    [Timeout(60_000)]
    public async Task RetainedByteBudget_ConcurrentReservations_KeepAccountsBalanced(CancellationToken cancellationToken)
    {
        const long budget = 32 * MiB;
        const int threadCount = 16;
        const int operationsPerThread = 20_000;
        var retained = new RetainedByteBudget();
        retained.SetLimit(budget);
        long heldPeak = 0;
        long held = 0;

        var workers = Enumerable.Range(0, threadCount).Select(seed => Task.Run(() =>
        {
            var random = new Random(seed);
            var mine = new Stack<int>();
            for (var i = 0; i < operationsPerThread; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (mine.Count > 0 && random.Next(2) == 0)
                {
                    var bytes = mine.Pop();
                    Interlocked.Add(ref held, -bytes);
                    retained.Release(bytes);
                    continue;
                }

                var size = random.Next(1, 4) * DefaultArenaCapacity / 3;
                if (!retained.TryReserve(size))
                    continue;

                mine.Push(size);
                RatchetUp(ref heldPeak, Interlocked.Add(ref held, size));
            }

            while (mine.Count > 0)
            {
                var bytes = mine.Pop();
                Interlocked.Add(ref held, -bytes);
                retained.Release(bytes);
            }
        }, cancellationToken)).ToArray();

        await Task.WhenAll(workers);

        // Accepted reservations never sum past the limit, and every release balances.
        await Assert.That(heldPeak).IsLessThanOrEqualTo(budget);
        await Assert.That(retained.RetainedBytes).IsEqualTo(0);
    }

    private static void RatchetUp(ref long location, long value)
    {
        long current;
        do
        {
            current = Volatile.Read(ref location);
            if (value <= current)
                return;
        }
        while (Interlocked.CompareExchange(ref location, value, current) != current);
    }

    [Test]
    public async Task AutoTunedProducer_IdleArenaRetentionFitsItsBufferMemory()
    {
        // A client budget small enough that the producer lands on the 32MiB BufferMemory
        // floor, like a producer in a small container. Its idle pool must fit that buffer.
        await using var client = Kafka.Connect("localhost:9092", builder =>
            builder.WithMemoryBudget(96UL * 1024 * 1024));
        await using var producer = (KafkaProducer<string, string>)client.CreateProducer<string, string>().Build();

        var accumulator = producer.RecordAccumulator;
        var retainedArenaBytes = (ulong)accumulator.BatchPoolMaxSizeForTest * (ulong)DefaultArenaCapacity;

        await Assert.That(accumulator.MaxBufferMemory).IsEqualTo(32UL * 1024 * 1024);
        await Assert.That(retainedArenaBytes).IsLessThanOrEqualTo(accumulator.MaxBufferMemory);
    }

    [Test]
    public async Task DefaultProducerOnLargeHost_KeepsDefaultPoolSize()
    {
        var options = new ProducerOptions { BootstrapServers = ["localhost:9092"] };

        var poolSize = RecordAccumulator.ComputePoolSize(options, availableMemoryBytes: 16 * GiB);

        await Assert.That(poolSize).IsEqualTo(BatchArena.DefaultPoolSize);
    }

    #endregion

    #region Thread-local retention

    [Test]
    [Timeout(60_000)]
    public async Task PartitionBatchPool_ReturnsFromManyThreads_RetainAtMostMaxPoolSize(
        CancellationToken cancellationToken)
    {
        const int maxPoolSize = 2;
        const int threadCount = 32;
        var (pool, readyPool) = CreatePools(maxPoolSize);

        using var threads = ReturningThreads.Start(threadCount, () => RentFillAndReturn(pool, readyPool));
        threads.WaitUntilAllReturned(cancellationToken);

        await Assert.That(pool.ApproximateCount).IsLessThanOrEqualTo(maxPoolSize);
    }

    [Test]
    [Timeout(60_000)]
    public async Task PartitionBatchPool_BatchesBeyondCapacity_AreCollectibleWhileReturningThreadsLive(
        CancellationToken cancellationToken)
    {
        // Returning threads stay alive (a thread-pool thread never exits), so any per-thread
        // slot holding a batch would keep its ~1.1MB arena reachable.
        const int maxPoolSize = 2;
        const int threadCount = 32;
        var (pool, readyPool) = CreatePools(maxPoolSize);

        using var threads = ReturningThreads.Start(threadCount, () => RentFillAndReturn(pool, readyPool));
        threads.WaitUntilAllReturned(cancellationToken);
        ForceFullCollection();

        var alive = CountDistinctSurvivors(threads.Results);

        await Assert.That(alive).IsLessThanOrEqualTo(maxPoolSize);
        GC.KeepAlive(pool);
        GC.KeepAlive(readyPool);
    }

    [Test]
    [Timeout(60_000)]
    public async Task PartitionBatchPool_BatchReturnedOnAnotherThread_IsReusedByTheNextRent(
        CancellationToken cancellationToken)
    {
        // Batches are rented on append threads and returned on whichever thread sealed them.
        // A returned batch must be visible to every renter, not parked with the returning thread.
        var (pool, readyPool) = CreatePools(maxPoolSize: 1);
        var batch = pool.Rent(new TopicPartition("pool-memory-bound", 0), partitionCount: 1);
        var first = new WeakReference(batch);

        using (var threads = ReturningThreads.Start(1, () =>
               {
                   FillCompleteAndReturn(pool, readyPool, batch);
                   return first;
               }))
        {
            threads.WaitUntilAllReturned(cancellationToken);

            var rented = pool.Rent(new TopicPartition("pool-memory-bound", 0), partitionCount: 1);

            await Assert.That(ReferenceEquals(rented, first.Target)).IsTrue();
            await Assert.That(pool.Misses).IsEqualTo(1);
        }
    }

    [Test]
    [Timeout(60_000)]
    public async Task PartitionBatchPool_CrossThreadChurn_RetainsBoundedArenas(CancellationToken cancellationToken)
    {
        // The reported workload: request threads rent and fill batches, while many other
        // threads seal and return them. Count every batch ever created and require the
        // survivors (pooled batches, each with an arena) to stay within the pool bound.
        const int maxPoolSize = 4;
        const int workerCount = 48;
        const int cyclesPerWorker = 20;
        var (pool, readyPool) = CreatePools(maxPoolSize);
        var created = new System.Collections.Concurrent.ConcurrentBag<WeakReference>();

        using (var threads = ReturningThreads.Start(workerCount, () =>
               {
                   for (var i = 0; i < cyclesPerWorker; i++)
                       created.Add(RentFillAndReturn(pool, readyPool));
                   return new WeakReference(null);
               }))
        {
            threads.WaitUntilAllReturned(cancellationToken);
            ForceFullCollection();

            var alive = CountDistinctSurvivors(created);

            await Assert.That(created.Count).IsEqualTo(workerCount * cyclesPerWorker);
            await Assert.That(alive).IsLessThanOrEqualTo(maxPoolSize);
            await Assert.That(pool.ApproximateCount).IsLessThanOrEqualTo(maxPoolSize);
        }

        GC.KeepAlive(pool);
        GC.KeepAlive(readyPool);
    }

    #endregion

    private static ProducerOptions CreateOptions(long bufferMemory, int batchSize = DefaultBatchSize) => new()
    {
        BootstrapServers = ["localhost:9092"],
        BatchSize = batchSize,
        BufferMemory = (ulong)bufferMemory,
    };

    private static (PartitionBatchPool Pool, ReadyBatchPool ReadyPool) CreatePools(int maxPoolSize)
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = DefaultBatchSize,
            LingerMs = 0,
        };
        var readyPool = new ReadyBatchPool(maxPoolSize);
        var pool = new PartitionBatchPool(options, maxPoolSize: maxPoolSize);
        pool.SetReadyBatchPool(readyPool);
        return (pool, readyPool);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RentFillAndReturn(PartitionBatchPool pool, ReadyBatchPool readyPool)
    {
        var batch = pool.Rent(new TopicPartition("pool-memory-bound", 0), partitionCount: 1);
        var reference = new WeakReference(batch);
        FillCompleteAndReturn(pool, readyPool, batch);
        return reference;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FillCompleteAndReturn(PartitionBatchPool pool, ReadyBatchPool readyPool, PartitionBatch batch)
    {
        ReadOnlySpan<byte> value = [1, 2, 3, 4, 5, 6, 7, 8];
        var result = batch.TryAppendFromSpans(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ReadOnlySpan<byte>.Empty,
            keyIsNull: true,
            value,
            valueIsNull: false,
            headers: null,
            headerCount: 0,
            completionSource: null,
            callback: null,
            PartitionBatch.EstimateRecordSize(0, value.Length, null, 0));
        if (!result.Success)
            throw new InvalidOperationException("Record did not fit in an empty batch.");

        var ready = batch.Complete() ?? throw new InvalidOperationException("Batch did not complete.");
        ready.CompleteSend(0, DateTimeOffset.UnixEpoch);
        readyPool.Return(ready);
        pool.Return(batch);
    }

    /// <summary>
    /// A thread can rent a batch another thread already returned, so several references may
    /// share one pooled batch. Count the distinct batches that survived collection.
    /// </summary>
    private static int CountDistinctSurvivors(IEnumerable<WeakReference> references) => references
        .Select(static reference => reference.Target)
        .Where(static target => target is not null)
        .Distinct(ReferenceEqualityComparer.Instance)
        .Count();

    private static void ForceFullCollection()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    /// <summary>
    /// Dedicated threads that each run one action and then stay alive until disposal,
    /// like thread-pool threads that returned a batch and moved on to other work.
    /// </summary>
    private sealed class ReturningThreads : IDisposable
    {
        private readonly Thread[] _threads;
        private readonly CountdownEvent _returned;
        private readonly ManualResetEventSlim _release = new();
        private readonly WeakReference[] _results;
        private Exception? _failure;

        private ReturningThreads(int count, Func<WeakReference> action)
        {
            _returned = new CountdownEvent(count);
            _results = new WeakReference[count];
            _threads = new Thread[count];
            for (var i = 0; i < count; i++)
            {
                var index = i;
                _threads[i] = new Thread(() =>
                {
                    try
                    {
                        _results[index] = action();
                    }
                    catch (Exception ex)
                    {
                        Interlocked.CompareExchange(ref _failure, ex, null);
                    }
                    finally
                    {
                        _returned.Signal();
                    }

                    _release.Wait();
                })
                {
                    IsBackground = true,
                    Name = $"pool-memory-bound-{index}",
                };
            }
        }

        public IReadOnlyList<WeakReference> Results => _results;

        public static ReturningThreads Start(int count, Func<WeakReference> action)
        {
            var threads = new ReturningThreads(count, action);
            foreach (var thread in threads._threads)
                thread.Start();
            return threads;
        }

        public void WaitUntilAllReturned(CancellationToken cancellationToken)
        {
            _returned.Wait(cancellationToken);
            if (_failure is not null)
                throw new InvalidOperationException("A returning thread failed.", _failure);
        }

        public void Dispose()
        {
            _release.Set();
            foreach (var thread in _threads)
                thread.Join();
            _returned.Dispose();
            _release.Dispose();
        }
    }
}
