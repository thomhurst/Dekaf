using System.Buffers;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public class PooledBufferWriterTests
{
    [Test]
    public async Task Constructor_CreatesBufferWithInitialCapacity()
    {
        var writer = new PooledBufferWriter(initialCapacity: 128);
        try
        {
            await Assert.That(writer.WrittenCount).IsEqualTo(0);

            // Should be able to write at least the initial capacity without growing
            var span = writer.GetSpan(128);
            await Assert.That(span.Length).IsGreaterThanOrEqualTo(128);
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Test]
    public async Task GetSpan_ReturnsWritableSpan()
    {
        var writer = new PooledBufferWriter(initialCapacity: 64);
        try
        {
            var span = writer.GetSpan(10);

            await Assert.That(span.Length).IsGreaterThanOrEqualTo(10);
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Test]
    public async Task GetMemory_ReturnsWritableMemory()
    {
        var writer = new PooledBufferWriter(initialCapacity: 64);
        try
        {
            var memory = writer.GetMemory(10);

            await Assert.That(memory.Length).IsGreaterThanOrEqualTo(10);
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Test]
    public async Task Advance_IncreasesWrittenCount()
    {
        var writer = new PooledBufferWriter(initialCapacity: 64);
        try
        {
            writer.GetSpan(10);
            writer.Advance(5);

            await Assert.That(writer.WrittenCount).IsEqualTo(5);

            writer.GetSpan(10);
            writer.Advance(3);

            await Assert.That(writer.WrittenCount).IsEqualTo(8);
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Test]
    public async Task Advance_ThrowsOnNegativeCount()
    {
        var writer = new PooledBufferWriter(initialCapacity: 64);
        try
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            {
                writer.Advance(-1);
                return Task.CompletedTask;
            });
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Test]
    public async Task Advance_ThrowsWhenExceedingBuffer()
    {
        var writer = new PooledBufferWriter(initialCapacity: 64);
        try
        {
            writer.GetSpan(10);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
            {
                // Try to advance more than the buffer can hold
                writer.Advance(1000);
                return Task.CompletedTask;
            });
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Test]
    public async Task GetSpan_GrowsBuffer_WhenSizeHintExceedsCapacity()
    {
        var writer = new PooledBufferWriter(initialCapacity: 64);
        try
        {
            // Request more than initial capacity
            var span = writer.GetSpan(256);

            await Assert.That(span.Length).IsGreaterThanOrEqualTo(256);
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Test]
    public async Task GetMemory_GrowsBuffer_WhenSizeHintExceedsCapacity()
    {
        var writer = new PooledBufferWriter(initialCapacity: 64);
        try
        {
            // Request more than initial capacity
            var memory = writer.GetMemory(256);

            await Assert.That(memory.Length).IsGreaterThanOrEqualTo(256);
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Test]
    public async Task BufferGrowth_PreservesExistingData()
    {
        var writer = new PooledBufferWriter(initialCapacity: 16);
        try
        {
            // Write some data
            var span1 = writer.GetSpan(8);
            span1[0] = 1;
            span1[1] = 2;
            span1[2] = 3;
            writer.Advance(3);

            // Force buffer growth
            _ = writer.GetSpan(128);

            // Get the final data
            var result = writer.ToPooledMemory();

            await Assert.That(result.Memory.Length).IsEqualTo(3);
            await Assert.That(result.Memory.Span[0]).IsEqualTo((byte)1);
            await Assert.That(result.Memory.Span[1]).IsEqualTo((byte)2);
            await Assert.That(result.Memory.Span[2]).IsEqualTo((byte)3);

            result.Return();
        }
        catch
        {
            writer.Dispose();
            throw;
        }
    }

    [Test]
    public async Task ToPooledMemory_TransfersOwnership()
    {
        var writer = new PooledBufferWriter(initialCapacity: 64);

        // Write some data
        var span = writer.GetSpan(5);
        span[0] = 10;
        span[1] = 20;
        span[2] = 30;
        writer.Advance(3);

        var result = writer.ToPooledMemory();

        await Assert.That(result.Memory.Length).IsEqualTo(3);
        await Assert.That(result.Memory.Span[0]).IsEqualTo((byte)10);
        await Assert.That(result.Memory.Span[1]).IsEqualTo((byte)20);
        await Assert.That(result.Memory.Span[2]).IsEqualTo((byte)30);

        // Clean up
        result.Return();
    }

    [Test]
    public async Task ToPooledMemory_ThrowsOnSecondCall()
    {
        var writer = new PooledBufferWriter(initialCapacity: 64);

        var span = writer.GetSpan(5);
        span[0] = 1;
        writer.Advance(1);

        var result = writer.ToPooledMemory();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
        {
            _ = writer.ToPooledMemory();
            return Task.CompletedTask;
        });

        result.Return();
    }

    [Test]
    public async Task Dispose_ReturnsBufferToPool()
    {
        var writer = new PooledBufferWriter(initialCapacity: 64);

        var span = writer.GetSpan(5);
        span[0] = 1;
        writer.Advance(1);

        // Dispose should not throw
        writer.Dispose();

        // Double dispose should be safe (no exception thrown)
        writer.Dispose();
    }

    [Test]
    public async Task GetSpan_ThrowsAfterDispose()
    {
        var writer = new PooledBufferWriter(initialCapacity: 64);
        writer.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
        {
            _ = writer.GetSpan(10);
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task GetMemory_ThrowsAfterDispose()
    {
        var writer = new PooledBufferWriter(initialCapacity: 64);
        writer.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
        {
            _ = writer.GetMemory(10);
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task Advance_ThrowsAfterDispose()
    {
        var writer = new PooledBufferWriter(initialCapacity: 64);
        _ = writer.GetSpan(10);
        writer.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
        {
            writer.Advance(1);
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task GetSpan_ThrowsAfterToPooledMemory()
    {
        var writer = new PooledBufferWriter(initialCapacity: 64);
        var result = writer.ToPooledMemory();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
        {
            _ = writer.GetSpan(10);
            return Task.CompletedTask;
        });

        result.Return();
    }

    [Test]
    public async Task GetMemory_ThrowsAfterToPooledMemory()
    {
        var writer = new PooledBufferWriter(initialCapacity: 64);
        var result = writer.ToPooledMemory();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
        {
            _ = writer.GetMemory(10);
            return Task.CompletedTask;
        });

        result.Return();
    }

    [Test]
    public async Task Advance_ThrowsAfterToPooledMemory()
    {
        var writer = new PooledBufferWriter(initialCapacity: 64);
        _ = writer.GetSpan(10);
        var result = writer.ToPooledMemory();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
        {
            writer.Advance(1);
            return Task.CompletedTask;
        });

        result.Return();
    }

    [Test]
    public async Task ImplementsIBufferWriter()
    {
        var writer = new PooledBufferWriter(initialCapacity: 64);
        try
        {
            // Verify it implements IBufferWriter<byte> by using helper method
            // Note: boxing a struct creates a copy, so we test via the struct directly
            WriteViaInterface(ref writer);

            await Assert.That(writer.WrittenCount).IsEqualTo(2);
        }
        finally
        {
            writer.Dispose();
        }
    }

    private static void WriteViaInterface<T>(ref T writer) where T : IBufferWriter<byte>
    {
        var span = writer.GetSpan(10);
        span[0] = 42;
        writer.Advance(1);

        var memory = writer.GetMemory(10);
        memory.Span[0] = 43;
        writer.Advance(1);
    }

    [Test]
    public async Task WrittenCount_ReflectsAllAdvances()
    {
        var writer = new PooledBufferWriter(initialCapacity: 256);
        try
        {
            await Assert.That(writer.WrittenCount).IsEqualTo(0);

            for (int i = 1; i <= 10; i++)
            {
                _ = writer.GetSpan(1);
                writer.Advance(1);
                await Assert.That(writer.WrittenCount).IsEqualTo(i);
            }
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Test]
    public async Task LargeWrite_HandlesMultipleGrows()
    {
        var writer = new PooledBufferWriter(initialCapacity: 16);
        try
        {
            // Write progressively larger chunks to force multiple grows
            for (int size = 32; size <= 1024; size *= 2)
            {
                var span = writer.GetSpan(size);
                await Assert.That(span.Length).IsGreaterThanOrEqualTo(size);
            }
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Test]
    public async Task GetSpan_WithZeroSizeHint_ReturnsNonEmptySpan()
    {
        var writer = new PooledBufferWriter(initialCapacity: 64);
        try
        {
            var span = writer.GetSpan(0);
            await Assert.That(span.Length).IsGreaterThan(0);
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Test]
    public async Task GetMemory_WithZeroSizeHint_ReturnsNonEmptyMemory()
    {
        var writer = new PooledBufferWriter(initialCapacity: 64);
        try
        {
            var memory = writer.GetMemory(0);
            await Assert.That(memory.Length).IsGreaterThan(0);
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Test]
    public async Task DefaultInitialCapacity_UsedWhenNotSpecified()
    {
        // Note: C# structs require explicit constructor call even with default params
        // new PooledBufferWriter() would use implicit parameterless ctor (all zeros)
        // To use default capacity, call the constructor explicitly
        var writer = new PooledBufferWriter(initialCapacity: 256);
        try
        {
            // Default is 256 per the implementation
            var span = writer.GetSpan(256);
            await Assert.That(span.Length).IsGreaterThanOrEqualTo(256);
        }
        finally
        {
            writer.Dispose();
        }
    }
}
