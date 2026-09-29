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
        var available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

        // Arenas: seven fit 8MiB. A batch-pool slot keeps at most two 256KB completion arrays, so 16 fit.
        await Assert.That(accumulator.ArenaPoolRegistrationForTest!.Limit.PoolSize).IsEqualTo(7);
        await Assert.That(accumulator.ArenaPoolRegistrationForTest!.Limit.PoolSize)
            .IsEqualTo(RecordAccumulator.ComputePoolSize(options));
        await Assert.That(accumulator.BatchPoolMaxSizeForTest).IsEqualTo(16);
        await Assert.That(accumulator.BatchPoolMaxSizeForTest)
            .IsEqualTo(RecordAccumulator.ComputeBatchPoolSize(options, options.BufferMemory, available));
        await Assert.That(BatchArena.PoolCapacity).IsGreaterThanOrEqualTo(7);
        await Assert.That(BatchArena.MissRatchetLimit).IsGreaterThanOrEqualTo(7);
        await Assert.That(BatchArena.RetainedByteLimit)
            .IsGreaterThanOrEqualTo(RecordAccumulator.ComputeRetainedArenaBytes(options, availableMemoryBytes: 0));
    }

    [Test]
    public async Task RecordAccumulator_Incremental_SizesBatchPoolByCompletionArrays()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = DefaultBatchSize,
            BufferMemory = 8 * MiB,
            BufferMemoryAllocationStrategy = BufferMemoryAllocationStrategy.Incremental,
        };

        await using var accumulator = new RecordAccumulator(options);

        // 8MiB / (2 × 256KB) completion arrays; no arena bound applies to Incremental.
        await Assert.That(accumulator.BatchPoolMaxSizeForTest).IsEqualTo(16);
        await Assert.That(accumulator.ArenaPoolRegistrationForTest).IsNull();
    }

    [Test]
    [Arguments(288L * 1024 * 1024, 32L * 1024 * 1024, DefaultBatchSize, 57)]                       // 384Mi pod: arenas 25; 28.8MB / (2 × 256KB) arrays
    [Arguments(64L * 1024 * 1024 * 1024, 8L * 1024 * 1024, DefaultBatchSize, 16)]                  // 8MiB / (2 × 256KB) arrays
    [Arguments(64L * 1024 * 1024 * 1024, 256L * 1024 * 1024, 16_384, BatchArena.MaxPoolSizeCap)]   // small batches: 256MB / (2 × 256KB) reaches the churn cap
    [Arguments(20L * 1024 * 1024, 256L * 1024 * 1024, DefaultBatchSize, 4)]                        // 2MiB budget / (2 × 256KB) arrays
    public async Task ComputeBatchPoolSize_IsBoundedByCompletionArraysNotArenas(
        long availableMemoryBytes,
        long bufferMemory,
        int batchSize,
        int expected)
    {
        var options = CreateOptions(bufferMemory, batchSize);

        var batchPoolSize = RecordAccumulator.ComputeBatchPoolSize(options, (ulong)bufferMemory, availableMemoryBytes);
        var arrayBytes = (long)ProducerContainerPools.MaxRecordArrayLength * IntPtr.Size;

        await Assert.That(batchPoolSize).IsEqualTo(expected);
        await Assert.That(batchPoolSize).IsGreaterThanOrEqualTo(RecordAccumulator.ComputePoolSize(options, availableMemoryBytes));
        // Each slot can hold a pooled batch's array and one queued for reuse.
        await Assert.That(RecordAccumulator.CompletionArraysPerBatchPoolSlot * batchPoolSize * arrayBytes)
            .IsLessThanOrEqualTo(RecordAccumulator.ComputeRetainedBudgetBytes((ulong)bufferMemory, availableMemoryBytes));
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

        var beforeLarge = limits.Current;
        limits.Register(large);
        var afterLarge = limits.Current;
        limits.Register(small);

        await Assert.That(beforeLarge).IsEqualTo(IdleLimit);
        await Assert.That(afterLarge).IsEqualTo(large);
        await Assert.That(limits.Current).IsEqualTo(large);
    }

    [Test]
    public async Task ArenaPoolLimits_DisposingTheLargestProducer_FallsBackToTheRemainingRequest()
    {
        // The review scenario: a default producer, then an 8MiB one; disposing the default
        // producer must let the 8MiB bound take effect.
        var limits = CreateLimits();
        var large = limits.Register(new ArenaPoolLimit(128, 512, 256 * MiB));
        var small = new ArenaPoolLimit(7, 7, 8 * MiB);
        limits.Register(small);
        var previous = limits.Current;

        var removed = limits.Unregister(large);
        var current = limits.Current;

        await Assert.That(removed).IsTrue();
        await Assert.That(previous.RetainedBytes).IsEqualTo(256 * MiB);
        await Assert.That(current).IsEqualTo(small);
        await Assert.That(BatchArena.ShouldReleasePooledArenas(previous.RetainedBytes, current.RetainedBytes)).IsTrue();
    }

    [Test]
    public async Task ArenaPoolLimits_EachFieldTakesItsOwnMaximum()
    {
        var limits = CreateLimits();
        limits.Register(new ArenaPoolLimit(128, 128, 8 * MiB));
        limits.Register(new ArenaPoolLimit(16, 512, 64 * MiB));

        await Assert.That(limits.Current).IsEqualTo(new ArenaPoolLimit(128, 512, 64 * MiB));
    }

    [Test]
    public async Task ArenaPoolLimits_LastUnregister_KeepsNoArenas()
    {
        var limits = CreateLimits();
        var registration = limits.Register(new ArenaPoolLimit(25, 25, 29 * MiB));
        var previous = limits.Current;

        var removed = limits.Unregister(registration);
        var current = limits.Current;
        var removedAgain = limits.Unregister(registration);
        var afterSecond = limits.Current;

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
                var registration = limits.Register(new ArenaPoolLimit(i, i, i * MiB));
                if (limits.Current.RetainedBytes < i * MiB)
                    throw new InvalidOperationException("Effective limit fell below a live request.");
                limits.Unregister(registration);
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
    public async Task RetainedByteBudget_ReserveThenCreate_ReleasesTheReservationWhenCreationFails()
    {
        // Pre-warming reserves an arena's bytes, then allocates it; an allocation failure must
        // not leave an orphaned reservation that shrinks the pool's allowance forever.
        var retained = new RetainedByteBudget();
        retained.SetLimit(4L * DefaultArenaCapacity);

        await Assert.That(() => retained.ReserveThenCreate<byte[]>(
                DefaultArenaCapacity,
                static _ => throw new InvalidOperationException("allocation failed")))
            .Throws<InvalidOperationException>();

        await Assert.That(retained.RetainedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task RetainedByteBudget_ReserveThenCreate_CreatesWithinTheLimitOnly()
    {
        var retained = new RetainedByteBudget();
        retained.SetLimit(DefaultArenaCapacity);
        var created = 0;

        var first = retained.ReserveThenCreate(DefaultArenaCapacity, bytes => { created++; return new object(); });
        var second = retained.ReserveThenCreate(DefaultArenaCapacity, bytes => { created++; return new object(); });

        await Assert.That(first).IsNotNull();
        await Assert.That(second).IsNull();
        await Assert.That(created).IsEqualTo(1);
        await Assert.That(retained.RetainedBytes).IsEqualTo(DefaultArenaCapacity);
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
        var limit = accumulator.ArenaPoolRegistrationForTest!.Limit;
        var arrayBytes = (ulong)ProducerContainerPools.MaxRecordArrayLength * (ulong)IntPtr.Size;

        await Assert.That(accumulator.MaxBufferMemory).IsEqualTo(32UL * 1024 * 1024);
        await Assert.That((ulong)limit.RetainedBytes).IsLessThanOrEqualTo(accumulator.MaxBufferMemory);
        await Assert.That((ulong)limit.PoolSize * (ulong)DefaultArenaCapacity).IsLessThanOrEqualTo(accumulator.MaxBufferMemory);
        await Assert.That((ulong)RecordAccumulator.CompletionArraysPerBatchPoolSlot * (ulong)accumulator.BatchPoolMaxSizeForTest * arrayBytes)
            .IsLessThanOrEqualTo(accumulator.MaxBufferMemory);
    }

    [Test]
    public async Task DefaultProducerOnLargeHost_KeepsDefaultPoolSize()
    {
        var options = new ProducerOptions { BootstrapServers = ["localhost:9092"] };

        var poolSize = RecordAccumulator.ComputePoolSize(options, availableMemoryBytes: 16 * GiB);

        await Assert.That(poolSize).IsEqualTo(BatchArena.DefaultPoolSize);
    }

    #endregion

    #region BufferMemory rebalance

    [Test]
    public async Task SetMaxBufferMemory_Downward_ShrinksBatchPoolAndReplacesArenaRegistration()
    {
        // An auto-tuned producer's BufferMemory falls when another client joins the budget.
        var options = CreateOptions(bufferMemory: 64 * MiB);
        await using var accumulator = new RecordAccumulator(options);
        var original = accumulator.ArenaPoolRegistrationForTest!;

        accumulator.SetMaxBufferMemory(8 * 1024 * 1024);

        var replacement = accumulator.ArenaPoolRegistrationForTest!;
        await Assert.That(accumulator.BatchPoolRetentionLimitForTest).IsEqualTo(16);
        await Assert.That(original.IsDisposed).IsTrue();
        await Assert.That(ReferenceEquals(replacement, original)).IsFalse();
        await Assert.That(replacement.IsDisposed).IsFalse();
        await Assert.That(replacement.Limit.RetainedBytes).IsEqualTo(8 * MiB);
        await Assert.That(replacement.Limit.PoolSize).IsEqualTo(7);
    }

    [Test]
    public async Task AutoTunedProducer_WhenAnotherProducerJoinsTheBudget_ShrinksItsBatchStorage()
    {
        // 768MiB client budget: one producer gets 128MiB of BufferMemory, two get 64MiB each.
        await using var client = Kafka.Connect("localhost:9092", builder =>
            builder.WithMemoryBudget(768UL * 1024 * 1024));
        await using var first = (KafkaProducer<string, string>)client.CreateProducer<string, string>().Build();
        var accumulator = first.RecordAccumulator;
        var available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var alone = accumulator.ArenaPoolRegistrationForTest!.Limit;

        await using var second = client.CreateProducer<string, string>().Build();

        var shared = accumulator.ArenaPoolRegistrationForTest!.Limit;
        await Assert.That(accumulator.MaxBufferMemory).IsEqualTo(64UL * 1024 * 1024);
        await Assert.That(alone.PoolSize).IsEqualTo(
            RecordAccumulator.ComputePoolSize(accumulator.OptionsForTest, 128UL * 1024 * 1024, available));
        await Assert.That(shared.PoolSize).IsEqualTo(
            RecordAccumulator.ComputePoolSize(accumulator.OptionsForTest, 64UL * 1024 * 1024, available));
        await Assert.That(shared.PoolSize).IsLessThan(alone.PoolSize);
        await Assert.That(shared.RetainedBytes).IsLessThanOrEqualTo(64 * MiB);
        await Assert.That(accumulator.BatchPoolRetentionLimitForTest).IsEqualTo(
            RecordAccumulator.ComputeBatchPoolSize(accumulator.OptionsForTest, 64UL * 1024 * 1024, available));
    }

    [Test]
    public async Task SetMaxBufferMemory_Upward_GrowsBatchPool()
    {
        var options = CreateOptions(bufferMemory: 8 * MiB);
        await using var accumulator = new RecordAccumulator(options);

        accumulator.SetMaxBufferMemory(256UL * 1024 * 1024);

        var available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var expectedArenas = RecordAccumulator.ComputePoolSize(options, 256UL * 1024 * 1024, available);
        var expectedBatches = RecordAccumulator.ComputeBatchPoolSize(options, 256UL * 1024 * 1024, available);
        await Assert.That(expectedArenas).IsGreaterThan(7);
        await Assert.That(expectedBatches).IsGreaterThan(16);
        await Assert.That(accumulator.BatchPoolRetentionLimitForTest).IsEqualTo(expectedBatches);
        await Assert.That(accumulator.BatchPoolMaxSizeForTest).IsGreaterThanOrEqualTo(expectedBatches);
        await Assert.That(accumulator.ArenaPoolRegistrationForTest!.Limit.PoolSize).IsEqualTo(expectedArenas);
        // The ReadyBatch pool keeps its 2x ratio to the batch pool after growing.
        await Assert.That(accumulator.ReadyBatchPoolMaxSizeForTest)
            .IsGreaterThanOrEqualTo(expectedBatches * RecordAccumulator.ReadyBatchPoolSizeRatioForTest);
    }

    [Test]
    public async Task SetMaxBufferMemory_Downward_KeepsTheReadyBatchPool()
    {
        // ReadyBatch objects hold no batch storage, so a reduction leaves their pool alone.
        await using var accumulator = new RecordAccumulator(CreateOptions(bufferMemory: 256 * MiB));
        var before = accumulator.ReadyBatchPoolMaxSizeForTest;

        accumulator.SetMaxBufferMemory(8 * 1024 * 1024);

        await Assert.That(accumulator.ReadyBatchPoolMaxSizeForTest).IsEqualTo(before);
    }

    [Test]
    public async Task SetMaxBufferMemory_UnchangedLimit_KeepsTheRegistration()
    {
        var accumulator = new RecordAccumulator(CreateOptions(bufferMemory: 8 * MiB));
        await using var _ = accumulator;
        var original = accumulator.ArenaPoolRegistrationForTest;

        accumulator.SetMaxBufferMemory(8 * 1024 * 1024);

        await Assert.That(ReferenceEquals(accumulator.ArenaPoolRegistrationForTest, original)).IsTrue();
        await Assert.That(original!.IsDisposed).IsFalse();
    }

    [Test]
    public async Task SetMaxBufferMemory_AfterDispose_DoesNotRegisterAgain()
    {
        var accumulator = new RecordAccumulator(CreateOptions(bufferMemory: 8 * MiB));
        await accumulator.DisposeAsync();

        accumulator.SetMaxBufferMemory(64 * 1024 * 1024);

        await Assert.That(accumulator.ArenaPoolRegistrationForTest!.IsDisposed).IsTrue();
    }

    [Test]
    public async Task SetMaxBufferMemory_Incremental_ResizesPoolWithoutRegistering()
    {
        await using var accumulator = new RecordAccumulator(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = DefaultBatchSize,
            BufferMemory = 1024 * MiB,
            BufferMemoryAllocationStrategy = BufferMemoryAllocationStrategy.Incremental,
        });

        accumulator.SetMaxBufferMemory(8 * 1024 * 1024);

        await Assert.That(accumulator.BatchPoolRetentionLimitForTest).IsEqualTo(16);
        await Assert.That(accumulator.ArenaPoolRegistrationForTest).IsNull();
    }

    [Test]
    [Timeout(60_000)]
    public async Task SetMaxBufferMemory_ConcurrentWithDispose_LeavesNoLiveRegistration(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var accumulator = new RecordAccumulator(CreateOptions(bufferMemory: 8 * MiB));
            using var start = new ManualResetEventSlim();
            var rebalance = Task.Run(() =>
            {
                start.Wait(cancellationToken);
                for (var i = 0; i < 20; i++)
                    accumulator.SetMaxBufferMemory((ulong)(i % 2 == 0 ? 64 : 8) * 1024 * 1024);
            }, cancellationToken);
            var dispose = Task.Run(async () =>
            {
                start.Wait(cancellationToken);
                await accumulator.DisposeAsync();
            }, cancellationToken);

            start.Set();
            await Task.WhenAll(rebalance, dispose);

            await Assert.That(accumulator.ArenaPoolRegistrationForTest!.IsDisposed).IsTrue();
        }
    }

    [Test]
    public async Task PartitionBatchPool_LoweredRetentionLimit_ReleasesPooledBatchesAndCapsReturns()
    {
        var (pool, readyPool) = CreatePools(maxPoolSize: 8);
        var batches = Enumerable.Range(0, 8)
            .Select(_ => pool.Rent(new TopicPartition("pool-memory-bound", 0), partitionCount: 1))
            .ToList();
        foreach (var batch in batches)
            FillCompleteAndReturn(pool, readyPool, batch);
        var pooledBefore = pool.ApproximateCount;

        pool.SetRetentionLimit(2);
        var pooledAfterLowering = pool.ApproximateCount;
        var rented = Enumerable.Range(0, 5)
            .Select(_ => pool.Rent(new TopicPartition("pool-memory-bound", 0), partitionCount: 1))
            .ToList();
        foreach (var batch in rented)
            FillCompleteAndReturn(pool, readyPool, batch);

        await Assert.That(pooledBefore).IsEqualTo(8);
        await Assert.That(pooledAfterLowering).IsEqualTo(0);
        await Assert.That(pool.ApproximateCount).IsEqualTo(2);
        await Assert.That(pool.RetentionLimit).IsEqualTo(2);
    }

    [Test]
    public async Task PartitionBatchPool_RaisedRetentionLimit_GrowsCapacity()
    {
        var (pool, readyPool) = CreatePools(maxPoolSize: 2);

        pool.SetRetentionLimit(6);
        var batches = Enumerable.Range(0, 6)
            .Select(_ => pool.Rent(new TopicPartition("pool-memory-bound", 0), partitionCount: 1))
            .ToList();
        foreach (var batch in batches)
            FillCompleteAndReturn(pool, readyPool, batch);

        await Assert.That(pool.MaxPoolSize).IsGreaterThanOrEqualTo(6);
        await Assert.That(pool.ApproximateCount).IsEqualTo(6);
    }

    [Test]
    public async Task PartitionBatchPool_PooledBatch_KeepsNoArenaUntilRentedAgain()
    {
        // Idle arenas must live only in the byte-bounded arena pool; a pooled batch that kept
        // its own would let both pools retain a full budget each.
        var (pool, readyPool) = CreatePools(maxPoolSize: 2);
        var batch = pool.Rent(new TopicPartition("pool-memory-bound", 0), partitionCount: 1);
        var rentedWithBuffer = batch.HasAppendBufferForTest;

        FillCompleteAndReturn(pool, readyPool, batch);
        var pooledWithBuffer = batch.HasAppendBufferForTest;
        var again = pool.Rent(new TopicPartition("pool-memory-bound", 1), partitionCount: 1);

        await Assert.That(rentedWithBuffer).IsTrue();
        await Assert.That(pooledWithBuffer).IsFalse();
        await Assert.That(ReferenceEquals(again, batch)).IsTrue();
        await Assert.That(again.HasAppendBufferForTest).IsTrue();
    }

    [Test]
    [Arguments(BufferMemoryAllocationStrategy.Full)]
    [Arguments(BufferMemoryAllocationStrategy.Incremental)]
    public async Task PartitionBatchPool_PreWarmedBatches_RentStorageOnFirstUse(BufferMemoryAllocationStrategy strategy)
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = DefaultBatchSize,
            BufferMemoryAllocationStrategy = strategy,
        };
        var readyPool = new ReadyBatchPool(4);
        var pool = new PartitionBatchPool(options, maxPoolSize: 4);
        pool.SetReadyBatchPool(readyPool);
        pool.PreWarm(4);

        var batch = pool.Rent(new TopicPartition("pool-memory-bound", 0), partitionCount: 1);
        FillCompleteAndReturn(pool, readyPool, pool.Rent(new TopicPartition("pool-memory-bound", 1), partitionCount: 1));

        await Assert.That(pool.Misses).IsEqualTo(0);
        await Assert.That(batch.HasAppendBufferForTest).IsTrue();
    }

    [Test]
    public async Task DirectlyConstructedBatch_StillAllocatesItsStorage()
    {
        var batch = new PartitionBatch(new TopicPartition("pool-memory-bound", 0), CreateOptions(bufferMemory: 8 * MiB));

        await Assert.That(batch.HasAppendBufferForTest).IsTrue();
    }

    [Test]
    [NotInParallel(nameof(BatchArena))]
    public async Task BatchArena_DisposingTheLargestRegistration_AdvancesTheReleaseEpoch()
    {
        // A registration larger than any real producer's is the effective limit while it lives,
        // so disposing it always lowers the limit and must release the pooled arenas.
        var epochBefore = BatchArena.ReleaseEpoch;
        var registration = BatchArena.Register(new ArenaPoolLimit(1, 1, long.MaxValue / 2));
        var limitWhileRegistered = BatchArena.RetainedByteLimit;

        registration.Dispose();

        await Assert.That(limitWhileRegistered).IsEqualTo(long.MaxValue / 2);
        await Assert.That(BatchArena.RetainedByteLimit).IsLessThan(long.MaxValue / 2);
        await Assert.That(BatchArena.ReleaseEpoch).IsGreaterThan(epochBefore);
    }

    [Test]
    [Timeout(60_000)]
    public async Task PartitionBatchPool_ConcurrentReturnsAfterLoweredLimit_NeverExceedIt(CancellationToken cancellationToken)
    {
        // A pool that once held 128 batches is lowered to 1; a burst of concurrent returns must
        // still retain at most one, not fill the historical capacity.
        const int returners = 64;
        var (pool, readyPool) = CreatePools(maxPoolSize: 128);
        var batches = Enumerable.Range(0, returners)
            .Select(i =>
            {
                var batch = pool.Rent(new TopicPartition("pool-memory-bound", i), partitionCount: 1);
                Append(batch);
                var ready = batch.Complete()!;
                ready.CompleteSend(0, DateTimeOffset.UnixEpoch);
                readyPool.Return(ready);
                return batch;
            })
            .ToArray();
        pool.SetRetentionLimit(1);

        using var barrier = new Barrier(returners);
        await Task.WhenAll(batches.Select(batch => Task.Factory.StartNew(
            () =>
            {
                barrier.SignalAndWait(cancellationToken);
                pool.Return(batch);
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default)));

        await Assert.That(pool.RetainedCount).IsEqualTo(1);
        await Assert.That(pool.ApproximateCount).IsEqualTo(1);
    }

    [Test]
    public async Task PartitionBatchPool_RetainedCount_BalancesAcrossRentReturnAndClear()
    {
        var (pool, readyPool) = CreatePools(maxPoolSize: 4);
        var batches = Enumerable.Range(0, 6)
            .Select(i => pool.Rent(new TopicPartition("pool-memory-bound", i), partitionCount: 1))
            .ToList();
        foreach (var batch in batches)
            FillCompleteAndReturn(pool, readyPool, batch);
        var afterReturns = pool.RetainedCount;

        var rented = pool.Rent(new TopicPartition("pool-memory-bound", 0), partitionCount: 1);
        var afterRent = pool.RetainedCount;
        FillCompleteAndReturn(pool, readyPool, rented);
        pool.SetRetentionLimit(2);
        var afterLowering = pool.RetainedCount;

        await Assert.That(afterReturns).IsEqualTo(4);
        await Assert.That(afterRent).IsEqualTo(3);
        await Assert.That(afterLowering).IsEqualTo(0);
        await Assert.That(pool.ApproximateCount).IsEqualTo(0);
    }

    [Test]
    public async Task ResizeBatchStorage_StaleNotification_UsesTheLatestBufferMemory()
    {
        // A 64MiB rebalance notification that finishes after a later 8MiB one must not
        // re-register the 64MiB budget.
        var options = CreateOptions(bufferMemory: 64 * MiB);
        await using var accumulator = new RecordAccumulator(options);
        accumulator.SetMaxBufferMemory(8 * 1024 * 1024);

        accumulator.ResizeBatchStorageForTest();

        await Assert.That(accumulator.ArenaPoolRegistrationForTest!.Limit.RetainedBytes).IsEqualTo(8 * MiB);
        await Assert.That(accumulator.ArenaPoolRegistrationForTest!.Limit.PoolSize).IsEqualTo(7);
        await Assert.That(accumulator.BatchPoolRetentionLimitForTest).IsEqualTo(16);
    }

    [Test]
    [Timeout(60_000)]
    public async Task SetMaxBufferMemory_ConcurrentRebalances_EndAtTheFinalBufferMemory(CancellationToken cancellationToken)
    {
        var options = CreateOptions(bufferMemory: 64 * MiB);
        await using var accumulator = new RecordAccumulator(options);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(seed => Task.Run(() =>
        {
            var random = new Random(seed);
            for (var i = 0; i < 200; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                accumulator.SetMaxBufferMemory((ulong)random.Next(4, 129) * 1024 * 1024);
            }
        }, cancellationToken)));

        var final = accumulator.MaxBufferMemory;
        var available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var registration = accumulator.ArenaPoolRegistrationForTest!;

        await Assert.That(registration.IsDisposed).IsFalse();
        await Assert.That(registration.Limit.PoolSize)
            .IsEqualTo(RecordAccumulator.ComputePoolSize(options, final, available));
        await Assert.That(accumulator.BatchPoolRetentionLimitForTest)
            .IsEqualTo(RecordAccumulator.ComputeBatchPoolSize(options, final, available));
    }

    [Test]
    public async Task PartitionBatchPool_OnlyLoweringTheLimit_AdvancesTheReleaseEpoch()
    {
        // Returns compare the epoch before admission and after pooling; only a reduction may
        // bump it, or every raise would force a needless release.
        var (pool, _) = CreatePools(maxPoolSize: 8);
        var initial = pool.ReleaseEpoch;

        pool.SetRetentionLimit(16);
        var afterRaise = pool.ReleaseEpoch;
        pool.SetRetentionLimit(16);
        var afterSame = pool.ReleaseEpoch;
        pool.SetRetentionLimit(4);
        var afterLower = pool.ReleaseEpoch;

        await Assert.That(afterRaise).IsEqualTo(initial);
        await Assert.That(afterSame).IsEqualTo(initial);
        await Assert.That(afterLower).IsEqualTo(initial + 1);
    }

    [Test]
    [Timeout(60_000)]
    public async Task PartitionBatchPool_ReturnsRacingRepeatedReductions_EndWithinTheFinalLimit(CancellationToken cancellationToken)
    {
        // Returns interleave with limit reductions; after both stop, the pool must hold no more
        // than the final limit, however the returns and releases were ordered.
        const int returners = 16;
        const int roundsPerReturner = 200;
        var (pool, readyPool) = CreatePools(maxPoolSize: 256);
        var stop = 0;

        var reducer = Task.Run(() =>
        {
            var random = new Random(7);
            while (Volatile.Read(ref stop) == 0 && !cancellationToken.IsCancellationRequested)
            {
                pool.SetRetentionLimit(random.Next(64, 257));
                pool.SetRetentionLimit(random.Next(1, 64));
            }
        }, CancellationToken.None);
        try
        {
            await Task.WhenAll(Enumerable.Range(0, returners).Select(i => Task.Run(() =>
            {
                for (var round = 0; round < roundsPerReturner; round++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var batch = pool.Rent(new TopicPartition("pool-memory-bound", i), partitionCount: 1);
                    FillCompleteAndReturn(pool, readyPool, batch);
                }
            }, cancellationToken)));
        }
        finally
        {
            // Stop the reducer even when a returner fails, so it cannot outlive the test.
            Volatile.Write(ref stop, 1);
            await reducer;
        }

        pool.SetRetentionLimit(2);

        await Assert.That(pool.RetainedCount).IsLessThanOrEqualTo(2);
        await Assert.That(pool.ApproximateCount).IsLessThanOrEqualTo(2);
        await Assert.That(pool.RetainedCount).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task BatchArena_Register_RejectsANonPositivePoolSize()
    {
        // Validated before registering, so a bad request never enters the effective limits.
        await Assert.That(() => BatchArena.Register(new ArenaPoolLimit(0, 1, MiB)))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task PartitionBatchPool_DiscardedBatch_ReleasesItsAdmissionLease()
    {
        // A batch returned while the pool is full is not pooled, so PrepareForPooling never runs;
        // the discard must still release the broker admission bytes it holds.
        var (pool, readyPool) = CreatePools(maxPoolSize: 1);
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 100, initialCapBytes: 1);
        var pooled = pool.Rent(new TopicPartition("pool-memory-bound", 0), partitionCount: 1);
        var discarded = pool.Rent(new TopicPartition("pool-memory-bound", 1), partitionCount: 1);
        FillCompleteAndReturn(pool, readyPool, pooled);

        budget.Charge(500);
        discarded.AddAdmissionLease(budget, generation: 1, bytes: 500);
        var chargedBeforeDiscard = budget.UnackedBytes;
        pool.Return(discarded);

        await Assert.That(pool.RetainedCount).IsEqualTo(1);
        await Assert.That(chargedBeforeDiscard).IsEqualTo(500);
        await Assert.That(budget.UnackedBytes).IsEqualTo(0);
        await Assert.That(discarded.HasAppendBufferForTest).IsFalse();
    }

    [Test]
    public async Task BatchArrayReuseQueue_HonorsItsRetentionLimit()
    {
        var queue = new BatchArrayReuseQueue(maxSize: 8);
        for (var i = 0; i < 8; i++)
            queue.EnqueueOrReturn(ProducerContainerPools.CompletionSources.Rent(16));

        queue.SetRetentionLimit(2);
        var afterLowering = queue.RetainedCount;
        for (var i = 0; i < 5; i++)
            queue.EnqueueOrReturn(ProducerContainerPools.CompletionSources.Rent(16));
        var afterRefill = queue.RetainedCount;
        var dequeued = queue.TryDequeue(out _);

        await Assert.That(afterLowering).IsEqualTo(0);
        await Assert.That(afterRefill).IsEqualTo(2);
        await Assert.That(dequeued).IsTrue();
        await Assert.That(queue.RetainedCount).IsEqualTo(1);
    }

    [Test]
    public async Task BatchArrayReuseQueue_RetentionLimitNeverExceedsItsCapacity()
    {
        var queue = new BatchArrayReuseQueue(maxSize: 2);
        queue.SetRetentionLimit(64);

        for (var i = 0; i < 6; i++)
            queue.EnqueueOrReturn(ProducerContainerPools.CompletionSources.Rent(16));

        await Assert.That(queue.RetainedCount).IsEqualTo(2);
    }

    [Test]
    [Timeout(60_000)]
    public async Task BatchArrayReuseQueue_ConcurrentEnqueueDequeue_KeepsAccountsBalanced(CancellationToken cancellationToken)
    {
        var queue = new BatchArrayReuseQueue(maxSize: 16);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(seed => Task.Run(() =>
        {
            var random = new Random(seed);
            for (var i = 0; i < 5_000; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (random.Next(2) == 0)
                    queue.EnqueueOrReturn(ProducerContainerPools.CompletionSources.Rent(16));
                else if (queue.TryDequeue(out var array))
                    ProducerContainerPools.CompletionSources.Return(array, clearArray: false);
                if (random.Next(64) == 0)
                    queue.SetRetentionLimit(random.Next(1, 17));
            }
        }, cancellationToken)));
        var limitAtEnd = 1;
        queue.SetRetentionLimit(limitAtEnd);
        var drained = 0;
        while (queue.TryDequeue(out _))
            drained++;

        await Assert.That(drained).IsLessThanOrEqualTo(limitAtEnd);
        await Assert.That(queue.RetainedCount).IsEqualTo(0);
    }

    [Test]
    [Arguments(BufferMemoryAllocationStrategy.Full)]
    [Arguments(BufferMemoryAllocationStrategy.Incremental)]
    public async Task ComputeBatchPoolSize_IgnoresArenaCapacity(BufferMemoryAllocationStrategy strategy)
    {
        // Batch pools hold no arenas, so a large (and for Incremental, unused) ArenaCapacity
        // must not raise how many completion arrays they may keep.
        var plain = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = DefaultBatchSize,
            BufferMemoryAllocationStrategy = strategy,
        };
        var largeArena = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = DefaultBatchSize,
            ArenaCapacity = 64 * 1024 * 1024,
            BufferMemoryAllocationStrategy = strategy,
        };

        var plainSize = RecordAccumulator.ComputeBatchPoolSize(plain, 8UL * 1024 * 1024, 64 * GiB);
        var largeArenaSize = RecordAccumulator.ComputeBatchPoolSize(largeArena, 8UL * 1024 * 1024, 64 * GiB);

        await Assert.That(plainSize).IsEqualTo(16);
        await Assert.That(largeArenaSize).IsEqualTo(plainSize);
    }

    [Test]
    public async Task BatchArrayReuseQueue_DoesNotKeepArraysLongerThanTheBudgetedLength()
    {
        // Batches of tiny records grow their completion arrays; the pools budget each array at
        // MaxRecordArrayLength, so a longer one must not be queued.
        var queue = new BatchArrayReuseQueue(maxSize: 8);

        queue.EnqueueOrReturn(new PooledValueTaskSource<RecordMetadata>[ProducerContainerPools.MaxRecordArrayLength + 1]);
        queue.EnqueueOrReturn(ProducerContainerPools.CompletionSources.Rent(ProducerContainerPools.MaxRecordArrayLength));

        await Assert.That(queue.RetainedCount).IsEqualTo(1);
        await Assert.That(queue.TryDequeue(out var kept)).IsTrue();
        await Assert.That(kept!.Length).IsLessThanOrEqualTo(ProducerContainerPools.MaxRecordArrayLength);
    }

    [Test]
    public async Task PartitionBatchPool_ReuseQueue_FollowsARaisedRetentionLimit()
    {
        // A pool created small (a small auto-tuned share) and raised later must queue up to the
        // new limit, not its constructor-time size.
        var (pool, _) = CreatePools(maxPoolSize: 2);
        pool.SetRetentionLimit(6);
        var queue = pool.ArrayReuseQueueForTest;

        for (var i = 0; i < 8; i++)
            queue.EnqueueOrReturn(ProducerContainerPools.CompletionSources.Rent(16));

        await Assert.That(queue.RetainedCount).IsEqualTo(6);
    }

    [Test]
    public async Task PrepareForPooling_DropsAnOversizedFireOnlyArray()
    {
        var options = CreateOptions(bufferMemory: 8 * MiB);
        var batch = new PartitionBatch(new TopicPartition("pool-memory-bound", 0), options);
        var storage = typeof(PartitionBatch).GetField(
            "_completionSources",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        storage.SetValue(batch, new PooledValueTaskSource<RecordMetadata>[ProducerContainerPools.MaxRecordArrayLength * 2]);

        batch.PrepareForPooling(options);

        var retained = (PooledValueTaskSource<RecordMetadata>[])storage.GetValue(batch)!;
        await Assert.That(retained.Length).IsLessThanOrEqualTo(ProducerContainerPools.MaxRecordArrayLength);
    }

    [Test]
    [NotInParallel(nameof(BatchArena))]
    public async Task PartitionBatchPool_UnusedBatch_ReturnsItsArenaToTheArenaPool()
    {
        // Rotation races and rejected first appends return a rented batch without completing it.
        // Its arena was never written or shared, so it must go back to the arena pool (buffer
        // kept) rather than be dropped and reallocated on the next rent.
        using var room = BatchArena.Register(new ArenaPoolLimit(BatchArena.MaxPoolSizeCap, BatchArena.MaxPoolSizeCap, long.MaxValue / 2));
        var (pool, _) = CreatePools(maxPoolSize: 2);
        var arenaField = typeof(PartitionBatch).GetField(
            "_arena",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var unused = pool.Rent(new TopicPartition("pool-memory-bound", 0), partitionCount: 1);
        var arena = (BatchArena)arenaField.GetValue(unused)!;

        pool.Return(unused);

        await Assert.That(arenaField.GetValue(unused)).IsNull();
        await Assert.That(arena.Buffer).IsNotNull();
        await Assert.That(arena.Position).IsEqualTo(0);
    }

    [Test]
    [NotInParallel(nameof(BatchArena))]
    public async Task PartitionBatchPool_DiscardedUnusedBatch_ReturnsItsArenaToTheArenaPool()
    {
        using var room = BatchArena.Register(new ArenaPoolLimit(BatchArena.MaxPoolSizeCap, BatchArena.MaxPoolSizeCap, long.MaxValue / 2));
        var (pool, readyPool) = CreatePools(maxPoolSize: 1);
        var arenaField = typeof(PartitionBatch).GetField(
            "_arena",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var pooled = pool.Rent(new TopicPartition("pool-memory-bound", 0), partitionCount: 1);
        var discarded = pool.Rent(new TopicPartition("pool-memory-bound", 1), partitionCount: 1);
        FillCompleteAndReturn(pool, readyPool, pooled);
        var arena = (BatchArena)arenaField.GetValue(discarded)!;

        pool.Return(discarded);

        await Assert.That(pool.RetainedCount).IsEqualTo(1);
        await Assert.That(arena.Buffer).IsNotNull();
    }

    [Test]
    public async Task ReleaseArenaPoolRegistration_WithoutDispose_ReleasesAndBlocksReregistration()
    {
        // A KafkaProducer whose constructor fails after creating its accumulator never disposes
        // it; releasing the registration must stick even if a budget rebalance arrives later.
        var accumulator = new RecordAccumulator(CreateOptions(bufferMemory: 8 * MiB));
        try
        {
            var registration = accumulator.ArenaPoolRegistrationForTest!;

            accumulator.ReleaseArenaPoolRegistration();
            accumulator.ReleaseArenaPoolRegistration();
            accumulator.SetMaxBufferMemory(64 * 1024 * 1024);

            await Assert.That(registration.IsDisposed).IsTrue();
            await Assert.That(ReferenceEquals(accumulator.ArenaPoolRegistrationForTest, registration)).IsTrue();
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task PartitionBatchPool_SetRetentionLimit_RejectsNonPositive()
    {
        var (pool, _) = CreatePools(maxPoolSize: 2);

        await Assert.That(() => pool.SetRetentionLimit(0)).Throws<ArgumentOutOfRangeException>();
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

    private static void Append(PartitionBatch batch)
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
