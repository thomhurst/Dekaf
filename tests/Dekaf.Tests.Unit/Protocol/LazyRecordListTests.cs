using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Protocol;

public class LazyRecordListTests
{
    [Test]
    public async Task LazyRecordList_TruncatedData_ReducesCountToParseableRecords()
    {
        // Arrange: serialize 3 records into a buffer using the real protocol writer
        var buffer = new ArrayBufferWriter<byte>();
        var records = new[]
        {
            new Record
            {
                OffsetDelta = 0,
                TimestampDelta = 0,
                Key = "key0"u8.ToArray(),
                Value = "value0"u8.ToArray()
            },
            new Record
            {
                OffsetDelta = 1,
                TimestampDelta = 1,
                Key = "key1"u8.ToArray(),
                Value = "value1"u8.ToArray()
            },
            new Record
            {
                OffsetDelta = 2,
                TimestampDelta = 2,
                Key = "key2"u8.ToArray(),
                Value = "value2-with-extra-data-to-make-it-longer"u8.ToArray()
            }
        };

        // Write all 3 records
        var writer = new KafkaProtocolWriter(buffer);
        foreach (var record in records)
        {
            record.Write(ref writer);
        }

        var fullData = buffer.WrittenMemory;

        // Figure out where the 3rd record starts by writing only the first 2
        var twoRecordBuffer = new ArrayBufferWriter<byte>();
        var twoWriter = new KafkaProtocolWriter(twoRecordBuffer);
        records[0].Write(ref twoWriter);
        records[1].Write(ref twoWriter);
        var twoRecordLength = twoRecordBuffer.WrittenCount;

        // Truncate: keep all of records 0 and 1, plus only 2 bytes of record 2
        var truncatedLength = twoRecordLength + 2;
        var truncatedData = fullData[..truncatedLength];

        // Act: create LazyRecordList claiming 3 records but with truncated data
        using var lazyList = LazyRecordList.Create(truncatedData, count: 3);

        // Assert: Count should start at 3 (the claimed count)
        await Assert.That(lazyList.Count).IsEqualTo(3);

        // Enumerate all available records — this triggers parsing and truncation handling
        var enumeratedRecords = new List<Record>();
        foreach (var record in lazyList)
        {
            enumeratedRecords.Add(record);
        }

        // After enumeration, Count should be reduced to 2 (only 2 fully parseable records)
        await Assert.That(lazyList.Count).IsEqualTo(2);
        await Assert.That(enumeratedRecords).Count().IsEqualTo(2);

        // Verify the successfully parsed records have correct data
        await Assert.That(enumeratedRecords[0].Key.ToArray()).IsEquivalentTo("key0"u8.ToArray());
        await Assert.That(enumeratedRecords[0].Value.ToArray()).IsEquivalentTo("value0"u8.ToArray());
        await Assert.That(enumeratedRecords[1].Key.ToArray()).IsEquivalentTo("key1"u8.ToArray());
        await Assert.That(enumeratedRecords[1].Value.ToArray()).IsEquivalentTo("value1"u8.ToArray());
    }

    [Test]
    public async Task LazyRecordList_TruncatedData_IndexAccessReturnsParseableRecords()
    {
        // Arrange: serialize 2 records, truncate partway through the 2nd
        var buffer = new ArrayBufferWriter<byte>();
        var records = new[]
        {
            new Record
            {
                OffsetDelta = 0,
                TimestampDelta = 0,
                Key = "k"u8.ToArray(),
                Value = "value-data-here"u8.ToArray()
            },
            new Record
            {
                OffsetDelta = 1,
                TimestampDelta = 1,
                Key = "k2"u8.ToArray(),
                Value = "another-value-with-enough-bytes"u8.ToArray()
            }
        };

        var writer = new KafkaProtocolWriter(buffer);
        foreach (var record in records)
        {
            record.Write(ref writer);
        }

        // Write only first record to find its boundary
        var oneRecordBuffer = new ArrayBufferWriter<byte>();
        var oneWriter = new KafkaProtocolWriter(oneRecordBuffer);
        records[0].Write(ref oneWriter);
        var oneRecordLength = oneRecordBuffer.WrittenCount;

        // Truncate: keep record 0 plus 1 byte of record 1
        var truncatedData = buffer.WrittenMemory[..(oneRecordLength + 1)];

        // Act: create LazyRecordList claiming 2 records
        using var lazyList = LazyRecordList.Create(truncatedData, count: 2);

        // Access record 0 — should succeed
        var record0 = lazyList[0];
        await Assert.That(record0.Key.ToArray()).IsEquivalentTo("k"u8.ToArray());

        // Accessing record 1 triggers truncation detection — Count gets reduced.
        // The indexer re-checks index >= _count after EnsureParsedUpTo reduces _count,
        // so it should throw ArgumentOutOfRangeException.
        await Assert.That(() => lazyList[1]).ThrowsExactly<ArgumentOutOfRangeException>();

        await Assert.That(lazyList.Count).IsEqualTo(1);
    }

    [Test]
    public async Task LazyRecordList_MalformedVarint_ReducesCountToParseableRecords()
    {
        // Arrange: serialize 2 good records, then append bytes that form a malformed varint.
        // A varint with 6 continuation bytes (all 0x80) is invalid and triggers
        // MalformedProtocolDataException("Malformed variable-length integer").
        var buffer = new ArrayBufferWriter<byte>();
        var records = new[]
        {
            new Record
            {
                OffsetDelta = 0,
                TimestampDelta = 0,
                Key = "key0"u8.ToArray(),
                Value = "value0"u8.ToArray()
            },
            new Record
            {
                OffsetDelta = 1,
                TimestampDelta = 1,
                Key = "key1"u8.ToArray(),
                Value = "value1"u8.ToArray()
            }
        };

        var writer = new KafkaProtocolWriter(buffer);
        foreach (var record in records)
        {
            record.Write(ref writer);
        }

        var goodDataLength = buffer.WrittenCount;

        // Append 6 bytes of 0x80 (continuation bits set, no termination) to form a malformed varint.
        // Records start with a varint-encoded length, so parsing the 3rd "record" will hit this.
        ReadOnlySpan<byte> malformedVarint = [0x80, 0x80, 0x80, 0x80, 0x80, 0x80];
        var combinedBuffer = new byte[goodDataLength + malformedVarint.Length];
        buffer.WrittenSpan.CopyTo(combinedBuffer);
        malformedVarint.CopyTo(combinedBuffer.AsSpan(goodDataLength));

        // Act: create LazyRecordList claiming 3 records but with malformed data for the 3rd
        using var lazyList = LazyRecordList.Create(combinedBuffer.AsMemory(), count: 3);

        // Enumerate all available records
        var enumeratedRecords = new List<Record>();
        foreach (var record in lazyList)
        {
            enumeratedRecords.Add(record);
        }

        // Assert: Count should be reduced to 2, only the 2 good records returned
        await Assert.That(lazyList.Count).IsEqualTo(2);
        await Assert.That(enumeratedRecords).Count().IsEqualTo(2);
        await Assert.That(enumeratedRecords[0].Key.ToArray()).IsEquivalentTo("key0"u8.ToArray());
        await Assert.That(enumeratedRecords[1].Key.ToArray()).IsEquivalentTo("key1"u8.ToArray());
    }

    [Test]
    public async Task LazyRecordList_InteriorMalformedVarint_Throws()
    {
        var records = new[]
        {
            new Record { OffsetDelta = 0, Value = "value-0"u8.ToArray() },
            new Record { OffsetDelta = 1, Value = "value-1"u8.ToArray() },
            new Record { OffsetDelta = 2, Value = "value-2"u8.ToArray() }
        };
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        foreach (var record in records)
            record.Write(ref writer);

        var firstRecordBuffer = new ArrayBufferWriter<byte>();
        var firstRecordWriter = new KafkaProtocolWriter(firstRecordBuffer);
        records[0].Write(ref firstRecordWriter);

        var bytes = buffer.WrittenSpan.ToArray();
        bytes.AsSpan(firstRecordBuffer.WrittenCount, 6).Fill(0x80);
        using var lazyList = LazyRecordList.Create(bytes, count: records.Length);

        await Assert.That(() => lazyList.ToArray()).Throws<MalformedProtocolDataException>();
    }

    [Test]
    public async Task LazyRecordList_InteriorOversizedRecordLength_Throws()
    {
        var records = new[]
        {
            new Record { OffsetDelta = 0, Value = "value-0"u8.ToArray() },
            new Record { OffsetDelta = 1, Value = "value-1"u8.ToArray() },
            new Record { OffsetDelta = 2, Value = "value-2"u8.ToArray() }
        };
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        foreach (var record in records)
            record.Write(ref writer);

        var firstRecordBuffer = new ArrayBufferWriter<byte>();
        var firstRecordWriter = new KafkaProtocolWriter(firstRecordBuffer);
        records[0].Write(ref firstRecordWriter);

        var bytes = buffer.WrittenSpan.ToArray();
        bytes[firstRecordBuffer.WrittenCount] = 0x7E; // Zig-zag encoded length 63.
        using var lazyList = LazyRecordList.Create(bytes, count: records.Length);

        await Assert.That(() => lazyList.ToArray()).Throws<MalformedProtocolDataException>();
    }

    [Test]
    public async Task LazyRecordList_InteriorValueLengthCannotConsumeFollowingRecord()
    {
        var records = new[]
        {
            new Record { OffsetDelta = 0, Value = new byte[] { 0x01 } },
            new Record { OffsetDelta = 1, Value = new byte[] { 0x02 } },
            new Record { OffsetDelta = 2, Value = new byte[] { 0x03 } }
        };
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        foreach (var record in records)
            record.Write(ref writer);

        var firstRecordBuffer = new ArrayBufferWriter<byte>();
        var firstRecordWriter = new KafkaProtocolWriter(firstRecordBuffer);
        records[0].Write(ref firstRecordWriter);

        var bytes = buffer.WrittenSpan.ToArray();
        // One-byte length, attributes, timestamp delta, offset delta, then key length.
        var secondValueLengthOffset = firstRecordBuffer.WrittenCount + 5;
        bytes[secondValueLengthOffset] = 0x0E; // Zig-zag encoded length 7.
        using var lazyList = LazyRecordList.Create(bytes, count: records.Length);

        await Assert.That(() => lazyList.ToArray()).Throws<MalformedProtocolDataException>();
    }

    [Test]
    public async Task LazyRecordList_TruncatedPayloadEndingInRecordLikeBytes_ReducesCount()
    {
        var records = new[]
        {
            new Record { OffsetDelta = 0, Value = new byte[] { 0x01 } },
            new Record { OffsetDelta = 1, Value = new byte[] { 0x02 } },
            new Record { OffsetDelta = 2, Value = new byte[] { 0x03 } }
        };
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        foreach (var record in records)
            record.Write(ref writer);

        var firstRecordBuffer = new ArrayBufferWriter<byte>();
        var firstRecordWriter = new KafkaProtocolWriter(firstRecordBuffer);
        records[0].Write(ref firstRecordWriter);

        var bytes = buffer.WrittenSpan.ToArray();
        var secondRecordOffset = firstRecordBuffer.WrittenCount;
        // The oversized lengths make every remaining byte part of the truncated second
        // record. Its payload ends with bytes that also encode a complete third record.
        bytes[secondRecordOffset] = 0x7E; // Zig-zag encoded record body length 63.
        bytes[secondRecordOffset + 5] = 0x7E; // Zig-zag encoded value length 63.
        using var lazyList = LazyRecordList.Create(bytes, count: records.Length);

        await Assert.That(lazyList.ToArray()).Count().IsEqualTo(1);
    }

    [Test]
    public async Task LazyRecordList_ShortMalformedTail_ReducesCountToParseableRecords()
    {
        var records = new[]
        {
            new Record { OffsetDelta = 0, Value = "value-0"u8.ToArray() },
            new Record { OffsetDelta = 1, Value = "value-1"u8.ToArray() }
        };
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        foreach (var record in records)
            record.Write(ref writer);

        var bytes = new byte[buffer.WrittenCount + 6];
        buffer.WrittenSpan.CopyTo(bytes);
        bytes.AsSpan(buffer.WrittenCount, 5).Fill(0x80);
        using var lazyList = LazyRecordList.Create(bytes, count: 5);

        await Assert.That(lazyList.ToArray()).Count().IsEqualTo(2);
    }

    [Test]
    public async Task LazyRecordList_LongTruncatedTail_ReducesCountToParseableRecords()
    {
        var records = new[]
        {
            new Record { OffsetDelta = 0, Value = "value-0"u8.ToArray() },
            new Record { OffsetDelta = 1, Value = "value-1"u8.ToArray() },
            new Record { OffsetDelta = 2, Value = new byte[100] }
        };
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        foreach (var record in records)
            record.Write(ref writer);

        var firstTwoBuffer = new ArrayBufferWriter<byte>();
        var firstTwoWriter = new KafkaProtocolWriter(firstTwoBuffer);
        records[0].Write(ref firstTwoWriter);
        records[1].Write(ref firstTwoWriter);
        var truncatedData = buffer.WrittenMemory[..(firstTwoBuffer.WrittenCount + 40)];
        using var lazyList = LazyRecordList.Create(truncatedData, count: 5);

        await Assert.That(lazyList.ToArray()).Count().IsEqualTo(2);
    }

    [Test]
    public async Task LazyRecordList_CompletelyTruncated_ReducesCountToZero()
    {
        // Arrange: provide just 1 byte of data but claim 5 records
        var truncatedData = new byte[] { 0xFF }.AsMemory();

        // Act
        using var lazyList = LazyRecordList.Create(truncatedData, count: 5);

        // Enumerate — should get 0 records with no exception
        var enumeratedRecords = new List<Record>();
        foreach (var record in lazyList)
        {
            enumeratedRecords.Add(record);
        }

        await Assert.That(lazyList.Count).IsEqualTo(0);
        await Assert.That(enumeratedRecords).Count().IsEqualTo(0);
    }

    [Test]
    public async Task LazyRecordList_EnsureAllParsed_TruncatedData_ReducesCountWithoutThrowing()
    {
        // Arrange: serialize 3 records, truncate partway through record 3
        var buffer = new ArrayBufferWriter<byte>();
        var records = new[]
        {
            new Record
            {
                OffsetDelta = 0,
                TimestampDelta = 0,
                Key = "key0"u8.ToArray(),
                Value = "value0"u8.ToArray()
            },
            new Record
            {
                OffsetDelta = 1,
                TimestampDelta = 1,
                Key = "key1"u8.ToArray(),
                Value = "value1"u8.ToArray()
            },
            new Record
            {
                OffsetDelta = 2,
                TimestampDelta = 2,
                Key = "key2"u8.ToArray(),
                Value = "value2-with-extra-data-to-make-it-longer"u8.ToArray()
            }
        };

        var writer = new KafkaProtocolWriter(buffer);
        foreach (var record in records)
        {
            record.Write(ref writer);
        }

        // Find boundary after record 1 to truncate partway through the third record (index 2)
        var twoRecordBuffer = new ArrayBufferWriter<byte>();
        var twoWriter = new KafkaProtocolWriter(twoRecordBuffer);
        records[0].Write(ref twoWriter);
        records[1].Write(ref twoWriter);
        var twoRecordLength = twoRecordBuffer.WrittenCount;

        // Truncate: keep all of records 0 and 1, plus only 2 bytes of record 2
        var truncatedData = buffer.WrittenMemory[..(twoRecordLength + 2)];

        // Act: create with claimed count of 3, then call EnsureAllParsed directly
        using var lazyList = LazyRecordList.Create(truncatedData, count: 3);
        lazyList.EnsureAllParsed();

        // Assert: Count reduced to 2 (only fully parseable records)
        await Assert.That(lazyList.Count).IsEqualTo(2);

        // Indexer access to records 0 and 1 still works
        var r0 = lazyList[0];
        await Assert.That(r0.Key.ToArray()).IsEquivalentTo("key0"u8.ToArray());
        await Assert.That(r0.Value.ToArray()).IsEquivalentTo("value0"u8.ToArray());

        var r1 = lazyList[1];
        await Assert.That(r1.Key.ToArray()).IsEquivalentTo("key1"u8.ToArray());
        await Assert.That(r1.Value.ToArray()).IsEquivalentTo("value1"u8.ToArray());
    }

    [Test]
    public async Task LazyRecordList_PooledInstance_ReusesCorrectlyAfterDispose()
    {
        // Arrange: create a LazyRecordList with valid data, dispose it (returns to pool),
        // then create another to verify pool reuse works correctly.
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var record = new Record
        {
            OffsetDelta = 0,
            TimestampDelta = 0,
            Key = "key"u8.ToArray(),
            Value = "value"u8.ToArray()
        };
        record.Write(ref writer);
        var data = buffer.WrittenMemory;

        // First usage: create, iterate, dispose
        var list1 = LazyRecordList.Create(data, count: 1);
        var record0 = list1[0];
        await Assert.That(record0.Key.ToArray()).IsEquivalentTo("key"u8.ToArray());
        list1.Dispose();

        // Second usage: should reuse pooled instance
        var list2 = LazyRecordList.Create(data, count: 1);
        var record1 = list2[0];
        await Assert.That(record1.Key.ToArray()).IsEquivalentTo("key"u8.ToArray());
        list2.Dispose();
    }

    [Test]
    public async Task LazyRecordList_DoubleDispose_IsSafe()
    {
        // Arrange
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var record = new Record
        {
            OffsetDelta = 0,
            TimestampDelta = 0,
            Key = "key"u8.ToArray(),
            Value = "value"u8.ToArray()
        };
        record.Write(ref writer);

        var list = LazyRecordList.Create(buffer.WrittenMemory, count: 1);

        // Access before dispose works
        var r = list[0];
        await Assert.That(r.Key.ToArray()).IsEquivalentTo("key"u8.ToArray());

        // Double dispose must not throw (Interlocked.Exchange guard)
        list.Dispose();
        list.Dispose();
    }

    [Test]
    public async Task LazyRecordList_RatchetPoolSize_GrowsAndDoesNotShrink()
    {
        var target = LazyRecordList.MaxPoolSizeValue + 1;

        LazyRecordList.RatchetPoolSize(target);
        LazyRecordList.RatchetPoolSize(1);

        await Assert.That(LazyRecordList.MaxPoolSizeValue).IsGreaterThanOrEqualTo(target);
    }
}
