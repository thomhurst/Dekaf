using Dekaf.Consumer;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class FetchBufferMemoryPoolTests
{
    [Test]
    public async Task TryReserve_BoundsAggregateAndAllowsOneOversizedResponse()
    {
        using var pool = new FetchBufferMemoryPool(100);

        await Assert.That(pool.TryReserve(60)).IsTrue();
        await Assert.That(pool.TryReserve(50)).IsFalse();
        await Assert.That(pool.UsedBytes).IsEqualTo(60);
        await Assert.That(pool.FreeBytes).IsEqualTo(40);

        pool.Release(60);

        await Assert.That(pool.TryReserve(150)).IsTrue();
        await Assert.That(pool.TryReserve(1)).IsFalse();
        await Assert.That(pool.UsedBytes).IsEqualTo(150);
        await Assert.That(pool.FreeBytes).IsEqualTo(0);

        pool.Release(150);
        await Assert.That(pool.UsedBytes).IsEqualTo(0);
        await Assert.That(pool.FreeBytes).IsEqualTo(100);
    }

    [Test]
    public async Task ReserveAsync_WaitsUntilExistingReservationReleases()
    {
        using var pool = new FetchBufferMemoryPool(100);
        var first = await pool.ReserveAsync(70, CancellationToken.None);

        var waiting = pool.ReserveAsync(40, CancellationToken.None).AsTask();
        await Assert.That(waiting.IsCompleted).IsFalse();

        first.Dispose();
        using var second = await waiting.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(pool.UsedBytes).IsEqualTo(40);
    }

    [Test]
    public async Task ReserveSlowAsync_RechecksCapacityBeforeWaiting()
    {
        using var pool = new FetchBufferMemoryPool(100);
        var first = await pool.ReserveAsync(100, CancellationToken.None);

        await Assert.That(pool.TryReserve(1)).IsFalse();
        first.Dispose();

        using var reservation = await pool
            .ReserveSlowAsync(1, CancellationToken.None)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(pool.UsedBytes).IsEqualTo(1);
    }

    [Test]
    public async Task Reservation_DisposeReleasesMemory()
    {
        using var pool = new FetchBufferMemoryPool(100);
        var reservation = await pool.ReserveAsync(60, CancellationToken.None);

        await Assert.That(pool.UsedBytes).IsEqualTo(60);

        reservation.Dispose();

        await Assert.That(pool.UsedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task FailedReservation_TracksDepletionUntilMemoryIsReleased()
    {
        using var pool = new FetchBufferMemoryPool(100);
        var first = await pool.ReserveAsync(70, CancellationToken.None);

        var waiting = pool.ReserveAsync(40, CancellationToken.None).AsTask();
        await Task.Delay(20);
        var depletedBeforeRelease = pool.DepletedDurationSeconds;

        first.Dispose();
        using var second = await waiting.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(depletedBeforeRelease).IsGreaterThan(0);
        await Assert.That(pool.DepletedDurationSeconds).IsGreaterThanOrEqualTo(depletedBeforeRelease);
        await Assert.That(pool.DepletedPercent).IsGreaterThan(0);
    }

    [Test]
    public async Task ReserveAsync_CancelledWaiterDoesNotLeakCapacity()
    {
        using var pool = new FetchBufferMemoryPool(100);
        var first = await pool.ReserveAsync(100, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var waiting = pool.ReserveAsync(1, cancellation.Token).AsTask();

        cancellation.Cancel();

        await Assert.That(async () => await waiting).Throws<OperationCanceledException>();
        await Assert.That(pool.UsedBytes).IsEqualTo(100);

        first.Dispose();
        await Assert.That(pool.UsedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task ReserveAsync_WakesSmallerWaiterWhenFirstWaiterDoesNotFit()
    {
        using var pool = new FetchBufferMemoryPool(100);
        using var first = await pool.ReserveAsync(60, CancellationToken.None);
        var released = await pool.ReserveAsync(20, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var large = pool.ReserveAsync(70, cancellation.Token).AsTask();
        var small = pool.ReserveAsync(20, CancellationToken.None).AsTask();

        released.Dispose();
        using var smallReservation = await small.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(large.IsCompleted).IsFalse();

        cancellation.Cancel();
        await Assert.That(async () => await large).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task ReserveAsync_StopsWakingWhenReleasedCapacityFitsNoWaiter()
    {
        using var pool = new FetchBufferMemoryPool(100);
        using var retained = await pool.ReserveAsync(80, CancellationToken.None);
        var released = await pool.ReserveAsync(10, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var first = pool.ReserveAsync(70, cancellation.Token).AsTask();
        var second = pool.ReserveAsync(80, cancellation.Token).AsTask();

        released.Dispose();

        await Assert.That(() => pool.PendingWakeCount).IsEqualTo(0)
            .Within(5000);
        await Assert.That(first.IsCompleted).IsFalse();
        await Assert.That(second.IsCompleted).IsFalse();

        cancellation.Cancel();
        await Assert.That(async () => await first).Throws<OperationCanceledException>();
        await Assert.That(async () => await second).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task Dispose_WakesAllWaitingReservations()
    {
        var pool = new FetchBufferMemoryPool(100);
        using var retained = await pool.ReserveAsync(100, CancellationToken.None);
        var first = pool.ReserveAsync(1, CancellationToken.None).AsTask();
        var second = pool.ReserveAsync(1, CancellationToken.None).AsTask();

        pool.Dispose();

        await Assert.That(async () => await first.WaitAsync(TimeSpan.FromSeconds(5)))
            .Throws<ObjectDisposedException>();
        await Assert.That(async () => await second.WaitAsync(TimeSpan.FromSeconds(5)))
            .Throws<ObjectDisposedException>();
    }
}
