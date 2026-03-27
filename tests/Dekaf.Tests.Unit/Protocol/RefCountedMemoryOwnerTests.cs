using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Protocol;

public class RefCountedMemoryOwnerTests
{
    /// <summary>
    /// Simple test implementation of IPooledMemory that tracks dispose calls.
    /// </summary>
    private sealed class TrackingPooledMemory : IPooledMemory
    {
        public int DisposeCount { get; private set; }
        public ReadOnlyMemory<byte> Memory { get; } = new byte[] { 1, 2, 3 };

        public void Dispose() => DisposeCount++;
    }

    [Test]
    public async Task Dispose_WithInitialRefCountOne_DisposesInnerImmediately()
    {
        // Arrange
        var inner = new TrackingPooledMemory();
        var owner = new RefCountedMemoryOwner(inner, initialRefCount: 1);

        // Act
        owner.Dispose();

        // Assert
        await Assert.That(inner.DisposeCount).IsEqualTo(1);
    }

    [Test]
    public async Task Dispose_WithRefCountTwo_DoesNotDisposeOnFirstDispose()
    {
        // Arrange
        var inner = new TrackingPooledMemory();
        var owner = new RefCountedMemoryOwner(inner, initialRefCount: 2);

        // Act - first dispose decrements from 2 to 1
        owner.Dispose();

        // Assert - inner should NOT be disposed yet
        await Assert.That(inner.DisposeCount).IsEqualTo(0);
    }

    [Test]
    public async Task Dispose_WithRefCountTwo_DisposesOnSecondDispose()
    {
        // Arrange
        var inner = new TrackingPooledMemory();
        var owner = new RefCountedMemoryOwner(inner, initialRefCount: 2);

        // Act - two disposes: 2 -> 1 -> 0
        owner.Dispose();
        owner.Dispose();

        // Assert - inner disposed exactly once
        await Assert.That(inner.DisposeCount).IsEqualTo(1);
    }

    [Test]
    public async Task Dispose_WithRefCountThree_OnlyDisposesAfterLastRelease()
    {
        // Arrange
        var inner = new TrackingPooledMemory();
        var owner = new RefCountedMemoryOwner(inner, initialRefCount: 3);

        // Act & Assert - first two disposes should not release
        owner.Dispose();
        await Assert.That(inner.DisposeCount).IsEqualTo(0);

        owner.Dispose();
        await Assert.That(inner.DisposeCount).IsEqualTo(0);

        // Third dispose should release
        owner.Dispose();
        await Assert.That(inner.DisposeCount).IsEqualTo(1);
    }

    [Test]
    public async Task Dispose_DoubleDisposeAfterFullyReleased_DoesNotDoubleFree()
    {
        // Arrange
        var inner = new TrackingPooledMemory();
        var owner = new RefCountedMemoryOwner(inner, initialRefCount: 1);

        // Act - first dispose releases, second should be a no-op
        owner.Dispose();
        owner.Dispose();

        // Assert - inner disposed exactly once, not twice
        await Assert.That(inner.DisposeCount).IsEqualTo(1);
    }

    [Test]
    public async Task Dispose_MultipleExtraDisposesAfterFullyReleased_DoesNotDoubleFree()
    {
        // Arrange
        var inner = new TrackingPooledMemory();
        var owner = new RefCountedMemoryOwner(inner, initialRefCount: 2);

        // Fully release: 2 -> 1 -> 0
        owner.Dispose();
        owner.Dispose();
        await Assert.That(inner.DisposeCount).IsEqualTo(1);

        // Extra disposes should be no-ops
        owner.Dispose();
        owner.Dispose();
        owner.Dispose();

        await Assert.That(inner.DisposeCount).IsEqualTo(1);
    }

    [Test]
    public void Constructor_RejectsInitialRefCountLessThanOne()
    {
        var inner = new TrackingPooledMemory();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            using var owner = new RefCountedMemoryOwner(inner, initialRefCount: 0);
        });
    }

    [Test]
    public void Constructor_RejectsNegativeInitialRefCount()
    {
        var inner = new TrackingPooledMemory();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            using var owner = new RefCountedMemoryOwner(inner, initialRefCount: -1);
        });
    }

    [Test]
    public async Task Memory_DelegatesInnerMemory()
    {
        // Arrange
        var inner = new TrackingPooledMemory();
        var owner = new RefCountedMemoryOwner(inner, initialRefCount: 1);

        // Act & Assert
        await Assert.That(owner.Memory.ToArray()).IsEquivalentTo(inner.Memory.ToArray());
    }
}
