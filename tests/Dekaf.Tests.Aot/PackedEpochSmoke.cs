using System.Runtime.CompilerServices;
using Dekaf.Consumer;

internal static class PackedEpochSmoke
{
    internal static void Run()
    {
        if (Unsafe.SizeOf<PackedProcessingEpoch>() != 8 ||
            Unsafe.SizeOf<ConsumeResult<string, string>>() > 96)
            throw new InvalidOperationException("Packed epoch exceeded the record size budget.");

        int?[] epochs = [null, int.MinValue, -1, 0, 1, 0x13579bdf, int.MaxValue];
        int[] indices = [0, 1, 255, 256, 65535, 65536, 0x5aa55a, PackedProcessingEpoch.IndexCapacity - 1];
        var copies = new PackedProcessingEpoch[indices.Length];
        foreach (var epoch in epochs)
        {
            var original = PackedProcessingEpoch.FromEpoch(epoch);
            for (var index = 0; index < indices.Length; index++)
                copies[index] = original.WithIndex(indices[index]);
            for (var index = 0; index < indices.Length; index++)
            {
                var copy = copies[index];
                if (copy.LeaderEpoch != epoch || copy.Index != indices[index] ||
                    copy.WithIndex(0).LeaderEpoch != epoch || copy.WithIndex(0).Index != 0)
                    throw new InvalidOperationException("Packed epoch changed across index updates or array copies.");
            }
            var result = new ConsumeResult<string, string>("epoch", 0, 10, "key", "value",
                null, 0, TimestampType.CreateTime, epoch)
            { ProcessingIndex = PackedProcessingEpoch.IndexCapacity - 1 };
            if (result.LeaderEpoch != epoch || result.ProcessingIndex != PackedProcessingEpoch.IndexCapacity - 1)
                throw new InvalidOperationException("ConsumeResult lost its leader epoch or completion index.");
        }
    }
}
