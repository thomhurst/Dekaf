using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

public enum ShareAcquisitionShape
{
    Contiguous,
    SparseRanges,
    InterleavedRanges
}

internal static class ShareAcquisitionFixture
{
    internal static ShareFetchAcquiredRecords[] Create(long firstOffset, int recordCount, ShareAcquisitionShape shape)
    {
        if (shape == ShareAcquisitionShape.Contiguous)
            return [new ShareFetchAcquiredRecords
            {
                FirstOffset = firstOffset, LastOffset = firstOffset + recordCount - 1, DeliveryCount = 1
            }];

        // Sparse ranges acquire four records then skip four. Interleaved ranges
        // acquire every other record, alternating delivery counts between ranges.
        var width = shape == ShareAcquisitionShape.SparseRanges ? 4 : 1;
        var ranges = new ShareFetchAcquiredRecords[recordCount / (2 * width)];
        for (var index = 0; index < ranges.Length; index++)
            ranges[index] = new ShareFetchAcquiredRecords
            {
                FirstOffset = firstOffset + index * 2 * width,
                LastOffset = firstOffset + index * 2 * width + width - 1,
                DeliveryCount = (short)(1 + index % 2)
            };
        return ranges;
    }

    internal static long[] Offsets(ShareFetchAcquiredRecords[] ranges)
    {
        var offsets = new List<long>();
        foreach (var range in ranges)
            for (var offset = range.FirstOffset; offset <= range.LastOffset; offset++)
                offsets.Add(offset);
        return offsets.ToArray();
    }
}
