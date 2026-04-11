using System.Text;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Consumer;

public class ConsumeRawBatchTests
{
    [Test]
    public async Task ConsumeRawBatch_EnumeratesAllRecords()
    {
        // Arrange
        using var pending = CreatePendingFetchData("test-topic", partitionIndex: 0, baseOffset: 0, messageCount: 5);
        var batch = new ConsumeRawBatch(pending);

        // Act
        var results = new List<ConsumeRawRecord>();
        foreach (var result in batch)
        {
            results.Add(result);
        }

        // Assert
        await Assert.That(results.Count).IsEqualTo(5);
    }

    [Test]
    public async Task ConsumeRawBatch_TopicAndPartition_AreCorrect()
    {
        using var pending = CreatePendingFetchData("my-topic", partitionIndex: 7, baseOffset: 100, messageCount: 1);
        var batch = new ConsumeRawBatch(pending);

        await Assert.That(batch.Topic).IsEqualTo("my-topic");
        await Assert.That(batch.Partition).IsEqualTo(7);
        await Assert.That(batch.TopicPartition).IsEqualTo(new TopicPartition("my-topic", 7));
    }

    [Test]
    public async Task ConsumeRawBatch_Count_MatchesRecordCount()
    {
        using var pending = CreatePendingFetchData("test-topic", partitionIndex: 0, baseOffset: 0, messageCount: 3);
        var batch = new ConsumeRawBatch(pending);

        // Enumerate to populate the count
        foreach (var _ in batch) { }

        await Assert.That(batch.Count).IsEqualTo(3);
    }

    [Test]
    public async Task ConsumeRawBatch_Records_HaveCorrectOffsets()
    {
        using var pending = CreatePendingFetchData("test-topic", partitionIndex: 0, baseOffset: 42, messageCount: 3);
        var batch = new ConsumeRawBatch(pending);

        var offsets = new List<long>();
        foreach (var result in batch)
        {
            offsets.Add(result.Offset);
        }

        await Assert.That(offsets.Count).IsEqualTo(3);
        await Assert.That(offsets[0]).IsEqualTo(42);
        await Assert.That(offsets[1]).IsEqualTo(43);
        await Assert.That(offsets[2]).IsEqualTo(44);
    }

    [Test]
    public async Task ConsumeRawBatch_Records_HaveRawBytesWithoutDeserialization()
    {
        using var pending = CreatePendingFetchData("test-topic", partitionIndex: 0, baseOffset: 0, messageCount: 2);
        var batch = new ConsumeRawBatch(pending);

        var results = new List<ConsumeRawRecord>();
        foreach (var result in batch)
        {
            results.Add(result);
        }

        // Verify raw bytes match what was put in
        await Assert.That(Encoding.UTF8.GetString(results[0].Key.Span)).IsEqualTo("key-0");
        await Assert.That(Encoding.UTF8.GetString(results[0].Value.Span)).IsEqualTo("value-0");
        await Assert.That(Encoding.UTF8.GetString(results[1].Key.Span)).IsEqualTo("key-1");
        await Assert.That(Encoding.UTF8.GetString(results[1].Value.Span)).IsEqualTo("value-1");
        await Assert.That(results[0].IsKeyNull).IsFalse();
        await Assert.That(results[0].IsValueNull).IsFalse();
    }

    [Test]
    public async Task ConsumeRawBatch_EmptyBatch_YieldsNoRecords()
    {
        using var pending = PendingFetchData.Create("test-topic", 0, Array.Empty<RecordBatch>());
        pending.EagerParseAll();

        var batch = new ConsumeRawBatch(pending);

        var count = 0;
        foreach (var _ in batch)
        {
            count++;
        }

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task ConsumeRawBatch_NullKeyAndValue_FlagsAreCorrect()
    {
        var records = new[]
        {
            new Record
            {
                OffsetDelta = 0,
                TimestampDelta = 0,
                Key = ReadOnlyMemory<byte>.Empty,
                Value = ReadOnlyMemory<byte>.Empty,
                IsKeyNull = true,
                IsValueNull = true,
                Headers = null,
                HeaderCount = 0,
            }
        };

        var recordBatch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Records = records,
        };

        using var pending = PendingFetchData.Create("test-topic", 0, new List<RecordBatch> { recordBatch });
        pending.EagerParseAll();

        var batch = new ConsumeRawBatch(pending);

        var results = new List<ConsumeRawRecord>();
        foreach (var result in batch)
        {
            results.Add(result);
        }

        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].IsKeyNull).IsTrue();
        await Assert.That(results[0].IsValueNull).IsTrue();
        await Assert.That(results[0].Key.Length).IsEqualTo(0);
        await Assert.That(results[0].Value.Length).IsEqualTo(0);
    }

    /// <summary>
    /// Creates a PendingFetchData with a single RecordBatch containing the specified number of records.
    /// </summary>
    private static PendingFetchData CreatePendingFetchData(string topic, int partitionIndex, long baseOffset, int messageCount)
    {
        var records = new Record[messageCount];
        for (var i = 0; i < messageCount; i++)
        {
            var key = Encoding.UTF8.GetBytes($"key-{i}");
            var value = Encoding.UTF8.GetBytes($"value-{i}");

            records[i] = new Record
            {
                OffsetDelta = i,
                TimestampDelta = i * 1000,
                Key = key,
                Value = value,
                IsKeyNull = false,
                IsValueNull = false,
                Headers = null,
                HeaderCount = 0,
            };
        }

        var recordBatch = new RecordBatch
        {
            BaseOffset = baseOffset,
            BaseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Records = records,
        };

        var pending = PendingFetchData.Create(topic, partitionIndex, new List<RecordBatch> { recordBatch });
        pending.EagerParseAll();
        return pending;
    }
}
