using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed class ShareAcknowledgedOffsetsTests
{
    [Test]
    public async Task OffsetView_EnumeratorSkipsEmptyAndCompactGapBatchesAndStaysExhausted()
    {
        List<AcknowledgementBatchData> batches =
        [
            new(0, -1, []), new(10, 12, [0]), new(20, 22, [2]),
            new(30, 32, [0, 1, 0]), new(40, 39, []), new(50, 52, [0])
        ];
        var offsets = new ShareAcknowledgedOffsets(batches);
        long[] expected = [20, 21, 22, 31];
        var enumerator = offsets.GetEnumerator();
        foreach (var offset in expected)
        {
            await Assert.That(enumerator.MoveNext()).IsTrue();
            await Assert.That(enumerator.Current).IsEqualTo(offset);
        }
        await Assert.That(enumerator.MoveNext()).IsFalse();
        await Assert.That(enumerator.MoveNext()).IsFalse();
        var copied = new long[expected.Length];
        offsets.CopyTo(copied);
        await Assert.That(copied).IsEquivalentTo(expected);
    }

    [Test]
    public async Task OffsetView_CompactRangesPreserveIndexEnumerationAndCopies()
    {
        List<AcknowledgementBatchData> batches = [new(10, 12, [2]), new(20, 22, [0]), new(30, 32, [1, 0, 3])];
        var offsets = new ShareAcknowledgedOffsets(batches);
        long[] expected = [10, 11, 12, 30, 32];
        var copy = new long[offsets.Length];
        offsets.CopyTo(copy);
        await Assert.That(copy).IsEquivalentTo(expected);
        var index = 0;
        foreach (var offset in offsets)
        {
            await Assert.That(offset).IsEqualTo(expected[index]);
            await Assert.That(offsets[index]).IsEqualTo(expected[index++]);
        }
        await Assert.That(index).IsEqualTo(expected.Length);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task OffsetView_FirstAccessDoesNotAllocate(bool sparse)
    {
        var types = new byte[64];
        long expectedSum = 0;
        for (var index = 0; index < types.Length; index++)
        {
            types[index] = sparse && index % 2 != 0 ? (byte)0 : (byte)1;
            if (types[index] != 0)
                expectedSum += 10 + index;
        }
        List<AcknowledgementBatchData> batches = [new(10, 73, types)];
        var destination = new long[64];

        // Construct this view inside the measured region, without warming an index or cursor.
        var before = GC.GetAllocatedBytesForCurrentThread();
        var offsets = new ShareAcknowledgedOffsets(batches);
        long sum = 0;
        for (var index = 0; index < offsets.Length; index++)
            sum += offsets[index];
        foreach (var offset in offsets)
            sum += offset;
        offsets.CopyTo(destination);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(sum).IsEqualTo(expectedSum * 2);
    }

    [Test]
    [Arguments(0, false)]
    [Arguments(0, true)]
    [Arguments(17, false)]
    [Arguments(17, true)]
    [Arguments(65, false)]
    [Arguments(65, true)]
    [Arguments(1025, false)]
    [Arguments(1025, true)]
    public async Task OffsetView_FirstAccessAcrossBatchesPreservesOffsetsWithoutAllocating(int length, bool sparse)
    {
        // Build an independent scalar oracle before constructing any offset view.
        byte[] dispositions = [1, 2, 3, 4, 127, 128, 255];
        var expected = new List<long>();
        var batches = new List<AcknowledgementBatchData>();
        for (var batchIndex = 0; batchIndex < 3; batchIndex++)
        {
            var firstOffset = 10L + batchIndex * (length + 100L);
            batches.Add(new AcknowledgementBatchData(firstOffset, firstOffset - 1, []));
            var types = new byte[length];
            for (var index = 0; index < length; index++)
            {
                if (sparse && index % 3 == 0)
                    continue;
                types[index] = dispositions[index % dispositions.Length];
                expected.Add(firstOffset + index);
            }
            batches.Add(new AcknowledgementBatchData(firstOffset, firstOffset + length - 1, types));
            if (sparse)
                batches.Add(new AcknowledgementBatchData(firstOffset + length, firstOffset + length + 32, new byte[33]));
        }

        var expectedOffsets = expected.ToArray();
        var indexed = new long[expectedOffsets.Length];
        var enumerated = new long[expectedOffsets.Length];
        var copied = new long[expectedOffsets.Length + 1];
        copied[^1] = -1;

        // Include construction and every access path, with no view or cursor warmup.
        var before = GC.GetAllocatedBytesForCurrentThread();
        var offsets = new ShareAcknowledgedOffsets(batches);
        var retainedCopy = offsets;
        var enumerator = offsets.GetEnumerator();
        var enumeratedCount = 0;
        for (var index = expectedOffsets.Length - 1; index >= 0; index--)
        {
            // Reverse indexing through a copied view must not move the forward enumerator.
            indexed[index] = retainedCopy[index];
            if (enumerator.MoveNext())
                enumerated[enumeratedCount++] = enumerator.Current;
        }
        var exhausted = !enumerator.MoveNext();
        retainedCopy.CopyTo(copied);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(offsets.Length).IsEqualTo(expectedOffsets.Length);
        await Assert.That(enumeratedCount).IsEqualTo(expectedOffsets.Length);
        await Assert.That(exhausted).IsTrue();
        await Assert.That(indexed.AsSpan().SequenceEqual(expectedOffsets)).IsTrue();
        await Assert.That(enumerated.AsSpan().SequenceEqual(expectedOffsets)).IsTrue();
        await Assert.That(copied.AsSpan(0, expectedOffsets.Length).SequenceEqual(expectedOffsets)).IsTrue();
        await Assert.That(copied[^1]).IsEqualTo(-1);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(7)]
    [Arguments(8)]
    [Arguments(9)]
    [Arguments(15)]
    [Arguments(16)]
    [Arguments(17)]
    [Arguments(31)]
    [Arguments(32)]
    [Arguments(33)]
    [Arguments(63)]
    [Arguments(64)]
    [Arguments(65)]
    [Arguments(127)]
    [Arguments(128)]
    [Arguments(129)]
    [Arguments(255)]
    [Arguments(256)]
    [Arguments(257)]
    [Arguments(511)]
    [Arguments(512)]
    [Arguments(513)]
    [Arguments(1024)]
    [Arguments(1025)]
    public async Task OffsetView_PreservesSparseAccessOrdersAndCopiedViews(int length)
    {
        // Include unknown dispositions and signed-byte boundaries: only Gap is excluded.
        byte[] dispositions = [1, 2, 3, 127, 128, 255];
        var random = new Random(3261 + length);
        foreach (var gapPercent in new[] { 0, 1, 50, 99, 100 })
        {
            var batches = new List<AcknowledgementBatchData>();
            var expected = new List<long>();
            for (var batchIndex = 0; batchIndex < 4; batchIndex++)
            {
                var types = new byte[length];
                var firstOffset = 10L + batchIndex * (length + 13L);
                for (var index = 0; index < types.Length; index++)
                {
                    types[index] = random.Next(100) < gapPercent
                        ? (byte)0
                        : dispositions[random.Next(dispositions.Length)];
                    if (types[index] != 0)
                        expected.Add(firstOffset + index);
                }
                batches.Add(new AcknowledgementBatchData(firstOffset, firstOffset + length - 1, types));
                // Empty and gap-only batches must not change logical indices in subsequent batches.
                batches.Add(new AcknowledgementBatchData(firstOffset + length, firstOffset + length - 1, []));
                batches.Add(new AcknowledgementBatchData(firstOffset + length, firstOffset + length + 2, [0, 0, 0]));
            }

            var offsets = new ShareAcknowledgedOffsets(batches);
            var retainedCopy = offsets;
            var result = new ShareAcknowledgementCommitResult(default, offsets, null);
            await Assert.That(offsets.Length).IsEqualTo(expected.Count);

            var ascending = new long[expected.Count];
            var descending = new long[expected.Count];
            var shuffled = new long[expected.Count];
            var order = Enumerable.Range(0, expected.Count).ToArray();
            random.Shuffle(order);
            for (var index = 0; index < expected.Count; index++)
            {
                ascending[index] = offsets[index];
                var reverseIndex = expected.Count - index - 1;
                descending[reverseIndex] = retainedCopy[reverseIndex];
                shuffled[order[index]] = result.Offsets[order[index]];
            }
            await Assert.That(ascending.SequenceEqual(expected)).IsTrue();
            await Assert.That(descending.SequenceEqual(expected)).IsTrue();
            await Assert.That(shuffled.SequenceEqual(expected)).IsTrue();

            var copied = new long[expected.Count + 1];
            copied[^1] = -1;
            retainedCopy.CopyTo(copied);
            await Assert.That(copied.Take(expected.Count).SequenceEqual(expected)).IsTrue();
            await Assert.That(copied[^1]).IsEqualTo(-1);
            var enumerated = new List<long>();
            foreach (var offset in result.Offsets)
                enumerated.Add(offset);
            await Assert.That(enumerated.SequenceEqual(expected)).IsTrue();
            await Assert.That(() => offsets[-1]).Throws<ArgumentOutOfRangeException>();
            await Assert.That(() => offsets[expected.Count]).Throws<ArgumentOutOfRangeException>();
        }
    }

    [Test]
    public async Task OffsetView_SkipsGapsAcrossEveryAccessPath()
    {
        var offsets = new ShareAcknowledgedOffsets(
        [
            new AcknowledgementBatchData(10, 14, [0, 1, 0, 2, 0]),
            new AcknowledgementBatchData(20, 21, [0, 0]),
            new AcknowledgementBatchData(30, 30, [3])
        ]);
        await Assert.That(offsets.Length).IsEqualTo(3);
        await Assert.That(offsets[0]).IsEqualTo(11);
        await Assert.That(offsets[1]).IsEqualTo(13);
        await Assert.That(offsets[2]).IsEqualTo(30);
        await Assert.That(() => offsets[3]).Throws<ArgumentOutOfRangeException>();
        var copy = new long[3];
        offsets.CopyTo(copy);
        await Assert.That(copy).IsEquivalentTo(new long[] { 11, 13, 30 });
        var enumerated = new List<long>();
        foreach (var offset in offsets)
            enumerated.Add(offset);
        await Assert.That(enumerated).IsEquivalentTo(copy);
    }

    [Test]
    public async Task OffsetView_IndexesCopiesAndEnumeratesAcrossBatches()
    {
        var offsets = new ShareAcknowledgedOffsets(
        [
            new AcknowledgementBatchData(10, 11, [(byte)AcknowledgeType.Accept, (byte)AcknowledgeType.Release]),
            new AcknowledgementBatchData(20, 22, [(byte)AcknowledgeType.Accept, (byte)AcknowledgeType.Accept, (byte)AcknowledgeType.Reject])
        ]);

        await Assert.That(offsets.Length).IsEqualTo(5);
        await Assert.That(offsets[0]).IsEqualTo(10);
        await Assert.That(offsets[2]).IsEqualTo(20);
        await Assert.That(offsets[4]).IsEqualTo(22);

        var copied = new long[offsets.Length];
        offsets.CopyTo(copied);
        await Assert.That(copied).IsEquivalentTo([10L, 11L, 20L, 21L, 22L]);

        var enumerated = new List<long>();
        foreach (var offset in offsets)
            enumerated.Add(offset);

        await Assert.That(enumerated).IsEquivalentTo(copied);
    }

    [Test]
    public async Task DefaultOffsetView_IsEmpty()
    {
        var offsets = default(ShareAcknowledgedOffsets);
        var enumerator = offsets.GetEnumerator();

        await Assert.That(offsets.Length).IsEqualTo(0);
        await Assert.That(enumerator.MoveNext()).IsFalse();
    }

    [Test]
    public async Task OffsetView_RejectsInvalidIndexAndShortDestination()
    {
        var offsets = new ShareAcknowledgedOffsets(
        [
            new AcknowledgementBatchData(10, 10, [(byte)AcknowledgeType.Accept])
        ]);

        await Assert.That(() => offsets[-1]).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => offsets[1]).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => offsets.CopyTo(Span<long>.Empty)).Throws<ArgumentException>();
    }
}
