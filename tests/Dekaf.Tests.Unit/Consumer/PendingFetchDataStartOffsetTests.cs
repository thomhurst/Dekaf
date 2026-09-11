using System.Buffers;
using System.Collections;
using Dekaf.Consumer;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Consumer;

public class PendingFetchDataStartOffsetTests
{
    [Test]
    [Arguments(false, 99L, -1L)]
    [Arguments(true, 99L, -1L)]
    [Arguments(false, 103L, -1L)]
    [Arguments(true, 103L, -1L)]
    [Arguments(false, 104L, 128L)]
    [Arguments(true, 104L, 128L)]
    [Arguments(false, 146L, -1L)]
    [Arguments(true, 146L, -1L)]
    [Arguments(false, 121L, 120L)]
    [Arguments(true, 121L, 120L)]
    public async Task StartFloorPreservesGappedOffsetsAndSnapshotBounds(bool sharedSlab, long floor, long stop)
    {
        int[] deltas = [0, 1, 3, 6, 10, 15, 21, 28, 36, 45];
        var records = deltas.Select(delta => new Record { OffsetDelta = delta }).ToArray();
        var batch = new RecordBatch { BaseOffset = 100, LastOffsetDelta = 45, Records = records };
        if (sharedSlab)
        {
            using var source = batch;
            var buffer = new ArrayBufferWriter<byte>();
            source.Write(buffer);
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            batch = RecordBatch.Read(ref reader);
            var slab = new Record[records.Length + 4];
            slab[0] = new Record { OffsetDelta = int.MaxValue };
            slab[1] = new Record { OffsetDelta = int.MaxValue };
            batch.UseParsedRecordSlab(slab, offset: 2);
            batch.EnsureAllRecordsParsed();
        }

        using var pending = PendingFetchData.Create("start-floor", 0, [batch],
            skipRecordsBelowOffset: floor, stopAtOffsetExclusive: stop);
        var actual = new List<long>();
        while (pending.MoveNext())
            actual.Add(pending.CurrentBaseOffset + pending.CurrentRecord.OffsetDelta);

        var expected = deltas.Select(delta => 100L + delta)
            .Where(offset => offset >= floor && (stop < 0 || offset < stop)).ToArray();
        await Assert.That(actual.SequenceEqual(expected)).IsTrue();
        await Assert.That(pending.IsExhausted).IsTrue();
    }

    [Test]
    public async Task FloorCanSkipWholeAndEmptyBatchesBeforeFindingAGappedSuffix()
    {
        using var pending = PendingFetchData.Create("start-floor", 0,
        [
            new RecordBatch { BaseOffset = 100, LastOffsetDelta = 2, Records = [new Record(), new Record { OffsetDelta = 2 }] },
            new RecordBatch { BaseOffset = 103, LastOffsetDelta = 0, Records = [] },
            new RecordBatch { BaseOffset = 105, LastOffsetDelta = 4, Records = [new Record(), new Record { OffsetDelta = 2 }, new Record { OffsetDelta = 4 }] }
        ], skipRecordsBelowOffset: 108);

        await Assert.That(pending.MoveNext()).IsTrue();
        await Assert.That(pending.CurrentBaseOffset + pending.CurrentRecord.OffsetDelta).IsEqualTo(109);
        await Assert.That(pending.MoveNext()).IsFalse();
    }

    [Test]
    public async Task NonArrayRecordsDoNotReadAFaultPastTheFirstIncludedRecord()
    {
        using var pending = PendingFetchData.Create("start-floor", 0,
            [new RecordBatch { LastOffsetDelta = 10, Records = new FaultingRecords() }], skipRecordsBelowOffset: 3);

        await Assert.That(pending.MoveNext()).IsTrue();
        await Assert.That(pending.CurrentRecord.OffsetDelta).IsEqualTo(5);
        await Assert.That(pending.MoveNext()).IsTrue();
        await Assert.That(() => pending.CurrentRecord.OffsetDelta).Throws<InvalidOperationException>();
    }

    private sealed class FaultingRecords : IReadOnlyList<Record>
    {
        public int Count => 3;
        public Record this[int index] => index < 2
            ? new Record { OffsetDelta = index * 5 }
            : throw new InvalidOperationException("The next record is malformed.");
        public IEnumerator<Record> GetEnumerator() => throw new NotSupportedException();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
