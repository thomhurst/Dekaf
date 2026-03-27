using Dekaf.Internal;

namespace Dekaf.Tests.Unit.Internal;

public class SemaphoreHelperTests
{
    [Test]
    public async Task AcquireOrThrowDisposedAsync_AlreadyDisposed_ThrowsObjectDisposedException()
    {
        var semaphore = new SemaphoreSlim(1, 1);
        semaphore.Dispose();

        await Assert.That(async () =>
                await SemaphoreHelper.AcquireOrThrowDisposedAsync(semaphore, "TestObject", CancellationToken.None))
            .ThrowsExactly<ObjectDisposedException>();
    }

    [Test]
    public async Task AcquireOrThrowDisposed_AlreadyDisposed_ThrowsObjectDisposedException()
    {
        var semaphore = new SemaphoreSlim(1, 1);
        semaphore.Dispose();

        await Assert.That(() => SemaphoreHelper.AcquireOrThrowDisposed(semaphore, "TestObject"))
            .ThrowsExactly<ObjectDisposedException>();
    }

    [Test]
    public async Task ReleaseSafely_WhenSemaphoreDisposed_DoesNotThrow()
    {
        var semaphore = new SemaphoreSlim(1, 1);
        semaphore.Dispose();

        await Assert.That(() => SemaphoreHelper.ReleaseSafely(semaphore)).ThrowsNothing();
    }

    [Test]
    public async Task AcquireOrThrowDisposedAsync_NormalAcquisition_Succeeds()
    {
        var semaphore = new SemaphoreSlim(1, 1);

        await SemaphoreHelper.AcquireOrThrowDisposedAsync(semaphore, "TestObject", CancellationToken.None);

        await Assert.That(semaphore.CurrentCount).IsEqualTo(0);

        semaphore.Release();
        semaphore.Dispose();
    }

    [Test]
    public async Task ReleaseSafely_NormalRelease_IncrementsSemaphoreCount()
    {
        var semaphore = new SemaphoreSlim(0, 1);

        SemaphoreHelper.ReleaseSafely(semaphore);

        await Assert.That(semaphore.CurrentCount).IsEqualTo(1);

        semaphore.Dispose();
    }
}
