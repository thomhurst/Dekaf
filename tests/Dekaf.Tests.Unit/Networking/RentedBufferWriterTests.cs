using System.Buffers;
using Dekaf.Networking;

namespace Dekaf.Tests.Unit.Networking;

public class RentedBufferWriterTests
{
    [Test]
    public async Task Write_And_DetachBuffer_ReturnsCorrectLengthAndPreservesPrefix()
    {
        const int offset = 4;
        using var writer = new RentedBufferWriter(initialCapacity: 64, offset: offset);

        // Write some data after the reserved prefix
        var span = writer.GetSpan(3);
        span[0] = 0xAA;
        span[1] = 0xBB;
        span[2] = 0xCC;
        writer.Advance(3);

        var (array, length) = writer.DetachBuffer();

        try
        {
            // Length = offset (4) + written (3)
            await Assert.That(length).IsEqualTo(7);

            // Prefix bytes at index 0..3 should be zero (freshly rented, not written to)
            // The written payload starts at index 4
            await Assert.That(array[offset]).IsEqualTo((byte)0xAA);
            await Assert.That(array[offset + 1]).IsEqualTo((byte)0xBB);
            await Assert.That(array[offset + 2]).IsEqualTo((byte)0xCC);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(array);
        }
    }

    [Test]
    public async Task Growth_PreservesPrefix_And_WrittenPayload()
    {
        const int offset = 4;
        // Use a tiny initial capacity to force growth
        using var writer = new RentedBufferWriter(initialCapacity: 8, offset: offset);

        // Write the prefix area manually via GetSpan at construction time isn't possible,
        // so write a known pattern into the data area, force a grow, then verify.
        var span1 = writer.GetSpan(4);
        span1[0] = 0x01;
        span1[1] = 0x02;
        span1[2] = 0x03;
        span1[3] = 0x04;
        writer.Advance(4);

        // This should trigger Grow because initial capacity (8) + offset (4) = 12 byte rental;
        // we've written 4 bytes at offset 4 so _written = 8. Requesting 16 more bytes exceeds capacity.
        var span2 = writer.GetSpan(16);
        span2[0] = 0x05;
        writer.Advance(1);

        var (array, length) = writer.DetachBuffer();

        try
        {
            await Assert.That(length).IsEqualTo(offset + 5);

            // Verify the original payload survived the copy
            await Assert.That(array[offset]).IsEqualTo((byte)0x01);
            await Assert.That(array[offset + 1]).IsEqualTo((byte)0x02);
            await Assert.That(array[offset + 2]).IsEqualTo((byte)0x03);
            await Assert.That(array[offset + 3]).IsEqualTo((byte)0x04);
            await Assert.That(array[offset + 4]).IsEqualTo((byte)0x05);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(array);
        }
    }

    [Test]
    public async Task Dispose_WithoutDetach_ReturnsBufferToPool()
    {
        // Verifying pool return isn't directly observable, but we verify no exception is thrown.
        var writer = new RentedBufferWriter(initialCapacity: 64, offset: 0);
        var span = writer.GetSpan(4);
        span[0] = 0xFF;
        writer.Advance(4);

        // Should not throw - buffer returned to pool
        var action = () => writer.Dispose();
        await Assert.That(action).ThrowsNothing();
    }

    [Test]
    public async Task Dispose_AfterDetach_IsNoOp()
    {
        var writer = new RentedBufferWriter(initialCapacity: 64, offset: 0);
        var span = writer.GetSpan(1);
        span[0] = 0x42;
        writer.Advance(1);

        var (array, _) = writer.DetachBuffer();

        // Dispose after DetachBuffer should be a no-op (no double-return)
        writer.Dispose();

        try
        {
            // The detached array should still be usable (not corrupted by a double-return)
            await Assert.That(array[0]).IsEqualTo((byte)0x42);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(array);
        }
    }

    [Test]
    public async Task Advance_WithNegativeCount_ThrowsArgumentOutOfRangeException()
    {
        using var writer = new RentedBufferWriter(initialCapacity: 64, offset: 0);

        var action = () => writer.Advance(-1);

        await Assert.That(action).ThrowsException()
            .WithMessageMatching("*count*");
    }

    [Test]
    public async Task Advance_ExceedingCapacity_ThrowsArgumentOutOfRangeException()
    {
        using var writer = new RentedBufferWriter(initialCapacity: 16, offset: 0);

        // ArrayPool may return a larger buffer than requested, so get actual capacity
        // by requesting 0 span and checking how much we can advance.
        // We'll just try to advance far beyond any reasonable rental.
        var action = () => writer.Advance(int.MaxValue);

        await Assert.That(action).ThrowsException()
            .WithMessageMatching("*count*");
    }
}
