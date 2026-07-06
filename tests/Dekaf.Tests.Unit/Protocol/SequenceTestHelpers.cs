using System.Buffers;

namespace Dekaf.Tests.Unit.Protocol;

internal static class SequenceTestHelpers
{
    public static ReadOnlySequence<byte> CreateMultiSegmentSequence(byte[] data, int splitAt)
    {
        if (splitAt <= 0 || splitAt >= data.Length)
            throw new ArgumentException("Split position must be within data bounds", nameof(splitAt));

        var first = new TestSegment(data.AsMemory(0, splitAt));
        var second = new TestSegment(data.AsMemory(splitAt));
        first.SetNext(second);

        return new ReadOnlySequence<byte>(first, 0, second, second.Memory.Length);
    }

    private sealed class TestSegment : ReadOnlySequenceSegment<byte>
    {
        public TestSegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        public void SetNext(TestSegment next)
        {
            Next = next;
            next.RunningIndex = RunningIndex + Memory.Length;
        }
    }
}
