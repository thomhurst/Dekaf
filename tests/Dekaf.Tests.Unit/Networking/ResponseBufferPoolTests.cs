using Dekaf.Networking;

namespace Dekaf.Tests.Unit.Networking;

public class ResponseBufferPoolTests
{
    [Test]
    public async Task Default_HasExpectedMaxArrayLength()
    {
        await Assert.That(ResponseBufferPool.Default.MaxArrayLength)
            .IsEqualTo(ResponseBufferPool.DefaultMaxArrayLength);
    }

    [Test]
    public async Task Create_WithSmallFetchMaxBytes_UsesDefaultMinimum()
    {
        // FetchMaxBytes of 1 MB + 1 MB overhead = 2 MB, which is below the 16 MB default
        var pool = ResponseBufferPool.Create(1 * 1024 * 1024);

        await Assert.That(pool.MaxArrayLength)
            .IsEqualTo(ResponseBufferPool.DefaultMaxArrayLength);
    }

    [Test]
    public async Task Create_WithDefaultFetchMaxBytes_ScalesAboveDefault()
    {
        // Default FetchMaxBytes is 52428800 (50 MB) + 1 MB overhead = ~51 MB
        const int defaultFetchMaxBytes = 52428800;
        var pool = ResponseBufferPool.Create(defaultFetchMaxBytes);

        var expected = defaultFetchMaxBytes + ResponseBufferPool.ProtocolOverheadBytes;

        await Assert.That(pool.MaxArrayLength).IsEqualTo(expected);
    }

    [Test]
    public async Task Create_WithLargeFetchMaxBytes_ScalesAccordingly()
    {
        // 6 partitions x 4 MB = 24 MB fetch response; FetchMaxBytes would be set to 24 MB
        const int fetchMaxBytes = 24 * 1024 * 1024;
        var pool = ResponseBufferPool.Create(fetchMaxBytes);

        var expected = fetchMaxBytes + ResponseBufferPool.ProtocolOverheadBytes;

        await Assert.That(pool.MaxArrayLength).IsEqualTo(expected);
    }

    [Test]
    public async Task Create_WithExactDefaultThreshold_UsesDefault()
    {
        // FetchMaxBytes such that fetchMaxBytes + overhead == DefaultMaxArrayLength
        var fetchMaxBytes = ResponseBufferPool.DefaultMaxArrayLength - ResponseBufferPool.ProtocolOverheadBytes;
        var pool = ResponseBufferPool.Create(fetchMaxBytes);

        await Assert.That(pool.MaxArrayLength)
            .IsEqualTo(ResponseBufferPool.DefaultMaxArrayLength);
    }

    [Test]
    public async Task Pool_CanRentAndReturnArrays()
    {
        var pool = ResponseBufferPool.Create(20 * 1024 * 1024);
        var array = pool.Pool.Rent(1024);

        await Assert.That(array).IsNotNull();
        await Assert.That(array.Length).IsGreaterThanOrEqualTo(1024);

        // Should not throw
        pool.Pool.Return(array);
    }

    [Test]
    public async Task Pool_CanRentArraysUpToMaxLength()
    {
        const int fetchMaxBytes = 32 * 1024 * 1024;
        var pool = ResponseBufferPool.Create(fetchMaxBytes);

        // Should be able to rent an array at the max size without falling back to new byte[]
        var array = pool.Pool.Rent(pool.MaxArrayLength);

        await Assert.That(array).IsNotNull();
        await Assert.That(array.Length).IsGreaterThanOrEqualTo(pool.MaxArrayLength);

        pool.Pool.Return(array);
    }

    [Test]
    public async Task PooledResponseBuffer_ReturnsToCorrectPool()
    {
        var pool = ResponseBufferPool.Create(20 * 1024 * 1024);
        var array = pool.Pool.Rent(1024);

        var buffer = new PooledResponseBuffer(array, 1024, isPooled: true, pool: pool);

        // Dispose should return to the pool's ArrayPool, not throw
        buffer.Dispose();

        // Verify the pool is functional after return
        var array2 = pool.Pool.Rent(1024);
        await Assert.That(array2).IsNotNull();
        pool.Pool.Return(array2);
    }

    [Test]
    public async Task PooledResponseBuffer_SlicePreservesPool()
    {
        var pool = ResponseBufferPool.Create(20 * 1024 * 1024);
        var array = pool.Pool.Rent(1024);

        // Write test data
        array[10] = 42;

        var buffer = new PooledResponseBuffer(array, 1024, isPooled: true, pool: pool);
        var sliced = buffer.Slice(10);

        await Assert.That(sliced.Data.Span[0]).IsEqualTo((byte)42);

        // Disposing the slice should return to the correct pool
        sliced.Dispose();
    }

    [Test]
    public async Task PooledResponseBuffer_TransferOwnershipPreservesPool()
    {
        var pool = ResponseBufferPool.Create(20 * 1024 * 1024);
        var array = pool.Pool.Rent(1024);
        array[0] = 99;

        var buffer = new PooledResponseBuffer(array, 1024, isPooled: true, pool: pool);
        var memory = buffer.TransferOwnership();

        await Assert.That(memory.Memory.Span[0]).IsEqualTo((byte)99);

        // Disposing the transferred memory should return to the correct pool
        memory.Dispose();
    }

    [Test]
    public async Task PooledResponseBuffer_UnpooledBuffer_DoesNotReturnToPool()
    {
        var array = new byte[100];

        // isPooled: false means Dispose is a no-op, no pool reference needed
        var buffer = new PooledResponseBuffer(array, 100, isPooled: false);
        buffer.Dispose(); // Should not throw

        await Assert.That(buffer.IsPooled).IsFalse();
    }

    [Test]
    public async Task PooledResponseBuffer_UnpooledBuffer_NullPoolDoesNotThrow()
    {
        // Unpooled buffers never call Return, so null pool is fine
        var array = new byte[1024];

        var buffer = new PooledResponseBuffer(array, 1024, isPooled: false);
        buffer.Dispose(); // Should not throw — isPooled is false so Return is never called

        await Assert.That(buffer.IsPooled).IsFalse();
    }

    [Test]
    public async Task Create_WithNearMaxIntFetchMaxBytes_DoesNotOverflow()
    {
        // Verify that large fetchMaxBytes values don't cause integer overflow
        var pool = ResponseBufferPool.Create(int.MaxValue - 500_000);

        await Assert.That(pool.MaxArrayLength).IsEqualTo(int.MaxValue);
    }

    [Test]
    public async Task Default_IsSingletonInstance()
    {
        var default1 = ResponseBufferPool.Default;
        var default2 = ResponseBufferPool.Default;

        await Assert.That(default1).IsSameReferenceAs(default2);
    }
}
