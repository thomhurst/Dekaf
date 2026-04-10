using System.Buffers;
using Dekaf.Networking;

namespace Dekaf.Tests.Unit.Networking;

public class RetainedBufferPipeWriterTests
{
    private static RetainedBufferPipeWriter CreateWriter(MemoryStream? stream = null, int minimumBufferSize = 256)
    {
        return new RetainedBufferPipeWriter(
            stream ?? new MemoryStream(),
            MemoryPool<byte>.Shared,
            minimumBufferSize);
    }

    [Test]
    public async Task GetMemory_ReturnsWritableMemory()
    {
        var writer = CreateWriter();

        var memory = writer.GetMemory(16);

        await Assert.That(memory.Length).IsGreaterThanOrEqualTo(16);
    }

    [Test]
    public async Task GetSpan_ReturnsWritableSpan()
    {
        var writer = CreateWriter();

        var span = writer.GetSpan(16);

        await Assert.That(span.Length).IsGreaterThanOrEqualTo(16);
    }

    [Test]
    public async Task FlushAsync_WritesDataToStream()
    {
        var stream = new MemoryStream();
        var writer = CreateWriter(stream);

        var memory = writer.GetMemory(4);
        memory.Span[0] = 0xDE;
        memory.Span[1] = 0xAD;
        memory.Span[2] = 0xBE;
        memory.Span[3] = 0xEF;
        writer.Advance(4);

        await writer.FlushAsync();

        await Assert.That(stream.ToArray()).IsEquivalentTo(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
    }

    [Test]
    public async Task FlushAsync_ResetsPositionButRetainsBuffer()
    {
        var stream = new MemoryStream();
        var writer = CreateWriter(stream, minimumBufferSize: 64);

        // First write-flush cycle
        var memory1 = writer.GetMemory(4);
        memory1.Span[0] = 1;
        memory1.Span[1] = 2;
        writer.Advance(2);
        await writer.FlushAsync();

        // Second write-flush cycle — should reuse the same buffer (no rent/return)
        var memory2 = writer.GetMemory(4);
        memory2.Span[0] = 3;
        memory2.Span[1] = 4;
        writer.Advance(2);
        await writer.FlushAsync();

        await Assert.That(stream.ToArray()).IsEquivalentTo(new byte[] { 1, 2, 3, 4 });
    }

    [Test]
    public async Task FlushAsync_WithNoData_DoesNotWriteToStream()
    {
        var stream = new MemoryStream();
        var writer = CreateWriter(stream);

        var result = await writer.FlushAsync();

        await Assert.That(stream.Length).IsEqualTo(0);
        await Assert.That(result.IsCompleted).IsFalse();
    }

    [Test]
    public async Task FlushAsync_AfterComplete_ReturnsIsCompletedTrue()
    {
        var writer = CreateWriter();

        writer.Complete();
        var result = await writer.FlushAsync();

        await Assert.That(result.IsCompleted).IsTrue();
    }

    [Test]
    public async Task Advance_PastBufferEnd_ThrowsInvalidOperationException()
    {
        var writer = CreateWriter(minimumBufferSize: 64);
        writer.GetMemory(64);

        await Assert.That(() => writer.Advance(int.MaxValue)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Advance_NegativeBytes_ThrowsArgumentOutOfRangeException()
    {
        var writer = CreateWriter();
        writer.GetMemory(16);

        await Assert.That(() => writer.Advance(-1)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Advance_WithoutGetMemory_ThrowsInvalidOperationException()
    {
        var writer = CreateWriter();

        await Assert.That(() => writer.Advance(1)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetMemory_AfterComplete_ThrowsInvalidOperationException()
    {
        var writer = CreateWriter();
        writer.Complete();

        await Assert.That(() => writer.GetMemory(16)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetSpan_AfterComplete_ThrowsInvalidOperationException()
    {
        var writer = CreateWriter();
        writer.Complete();

        await Assert.That(() => writer.GetSpan(16)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Advance_AfterComplete_ThrowsInvalidOperationException()
    {
        var writer = CreateWriter();
        writer.GetMemory(16);
        writer.Complete();

        await Assert.That(() => writer.Advance(1)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Complete_CalledTwice_DoesNotThrow()
    {
        var writer = CreateWriter();

        writer.Complete();
        writer.Complete();

        // Should not throw — second call is a no-op.
        // Verify writer is completed by checking FlushAsync returns isCompleted.
        var result = await writer.FlushAsync();
        await Assert.That(result.IsCompleted).IsTrue();
    }

    [Test]
    public async Task CompleteAsync_CompletesWriter()
    {
        var writer = CreateWriter();

        await writer.CompleteAsync();

        await Assert.That(() => writer.GetMemory(16)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task EnsureCapacity_PreservesUnflushedBytesWhenGrowing()
    {
        var stream = new MemoryStream();
        // Use a very small minimum buffer to force a grow.
        var writer = CreateWriter(stream, minimumBufferSize: 8);

        // Write some data that fits in the initial buffer.
        var memory = writer.GetMemory(4);
        memory.Span[0] = 0xAA;
        memory.Span[1] = 0xBB;
        writer.Advance(2);

        // Now request more memory than remains — forces a grow with unflushed data.
        var grownMemory = writer.GetMemory(1024);
        grownMemory.Span[0] = 0xCC;
        grownMemory.Span[1] = 0xDD;
        writer.Advance(2);

        await writer.FlushAsync();

        // All 4 bytes should appear: the 2 preserved from before the grow + 2 after.
        await Assert.That(stream.ToArray()).IsEquivalentTo(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });
    }

    [Test]
    public async Task FlushAsync_OptimisticReset_PreventsDoubleSend()
    {
        var stream = new MemoryStream();
        var writer = CreateWriter(stream);

        var memory = writer.GetMemory(2);
        memory.Span[0] = 0x01;
        memory.Span[1] = 0x02;
        writer.Advance(2);

        await writer.FlushAsync();

        // A second flush without new data should not re-send.
        await writer.FlushAsync();

        await Assert.That(stream.ToArray()).IsEquivalentTo(new byte[] { 0x01, 0x02 });
    }
}
