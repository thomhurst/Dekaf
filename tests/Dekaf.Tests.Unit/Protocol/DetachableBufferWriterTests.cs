using System.Buffers;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Protocol;

[NotInParallel]
public sealed class DetachableBufferWriterTests
{
    [Test]
    public async Task Rent_AfterDispose_ReusesWrapperAndPreservesDetachedBufferOwnership()
    {
        var pool = ArrayPool<byte>.Create();
        var first = DetachableBufferWriter.Rent(pool, initialCapacity: 16);
        var span = first.GetSpan(1);
        span[0] = 42;
        first.Advance(1);
        var detached = first.DetachBuffer(out var detachedLength);
        first.Dispose();

        var second = DetachableBufferWriter.Rent(pool, initialCapacity: 16);
        try
        {
            await Assert.That(second).IsSameReferenceAs(first);
            await Assert.That(second.WrittenSpan.Length).IsEqualTo(0);
            await Assert.That(detachedLength).IsEqualTo(1);
            await Assert.That(detached[0]).IsEqualTo((byte)42);
            await Assert.That(second.GetSpan(1).Length).IsGreaterThanOrEqualTo(1);
        }
        finally
        {
            second.Dispose();
            pool.Return(detached);
        }
    }

    [Test]
    public async Task Rent_WhileOutstanding_ReturnsIndependentWriter()
    {
        // Pins the documented single-slot cache invariant: a nested Rent while another
        // writer is outstanding must return a distinct wrapper instead of sharing
        // live state (RecordBatch.cs cache comment).
        var pool = ArrayPool<byte>.Create();
        var outer = DetachableBufferWriter.Rent(pool, initialCapacity: 16);
        var inner = DetachableBufferWriter.Rent(pool, initialCapacity: 16);
        try
        {
            await Assert.That(inner).IsNotSameReferenceAs(outer);

            inner.GetSpan(1)[0] = 2;
            inner.Advance(1);
            await Assert.That(outer.WrittenSpan.Length).IsEqualTo(0);
        }
        finally
        {
            inner.Dispose();
            outer.Dispose();
        }
    }

    [Test]
    public async Task Rent_AfterDisposeWithoutDetach_ReusesClearedWrapper()
    {
        var pool = ArrayPool<byte>.Create();
        var first = DetachableBufferWriter.Rent(pool, initialCapacity: 16);
        first.Advance(1);
        first.Dispose();

        var second = DetachableBufferWriter.Rent(pool, initialCapacity: 16);
        try
        {
            await Assert.That(second).IsSameReferenceAs(first);
            await Assert.That(second.WrittenSpan.Length).IsEqualTo(0);
        }
        finally
        {
            second.Dispose();
        }
    }
}
