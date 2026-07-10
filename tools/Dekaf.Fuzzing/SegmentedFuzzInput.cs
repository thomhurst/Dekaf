using System.Buffers;

namespace Dekaf.Fuzzing;

internal static class SegmentedFuzzInput
{
    public static ReadOnlySequence<byte> Create(ReadOnlySpan<byte> input)
    {
        var buffer = input.ToArray();
        var split = buffer.Length / 2;
        var first = new BufferSegment(buffer.AsMemory(0, split));
        var last = first.Append(buffer.AsMemory(split));
        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    private sealed class BufferSegment : ReadOnlySequenceSegment<byte>
    {
        public BufferSegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        public BufferSegment Append(ReadOnlyMemory<byte> memory)
        {
            var segment = new BufferSegment(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };
            Next = segment;
            return segment;
        }
    }
}
