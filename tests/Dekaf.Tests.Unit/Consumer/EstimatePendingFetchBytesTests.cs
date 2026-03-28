using Dekaf.Consumer;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Consumer;

public class EstimatePendingFetchBytesTests
{
    [Test]
    public async Task EmptyBatches_ReturnsPartitionOverheadOnly()
    {
        // Arrange - no batches, just partition-level overhead
        using var pending = new PendingFetchData(
            "test-topic",
            partitionIndex: 0,
            batches: Array.Empty<RecordBatch>());

        // Act
        var bytes = KafkaConsumer<string, string>.EstimatePendingFetchBytes(pending);

        // Assert - should include per-partition response overhead (38 bytes)
        await Assert.That(bytes).IsEqualTo(38);
    }

    [Test]
    public async Task SingleBatch_IncludesBatchLengthPlusOverhead()
    {
        // Arrange
        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BatchLength = 1000,
            BaseTimestamp = 1700000000000L,
            Attributes = 0,
            Records = []
        };

        using var pending = new PendingFetchData(
            "test-topic",
            partitionIndex: 0,
            batches: [batch]);

        // Act
        var bytes = KafkaConsumer<string, string>.EstimatePendingFetchBytes(pending);

        // Assert: 38 (partition overhead) + 1000 (batch body) + 12 (batch header: baseOffset + batchLength prefix)
        await Assert.That(bytes).IsEqualTo(38 + 1000 + 12);
    }

    [Test]
    public async Task MultipleBatches_IncludesPerBatchOverhead()
    {
        // Arrange
        var batch1 = new RecordBatch
        {
            BaseOffset = 0,
            BatchLength = 500,
            BaseTimestamp = 1700000000000L,
            Attributes = 0,
            Records = []
        };
        var batch2 = new RecordBatch
        {
            BaseOffset = 100,
            BatchLength = 700,
            BaseTimestamp = 1700000000000L,
            Attributes = 0,
            Records = []
        };

        using var pending = new PendingFetchData(
            "test-topic",
            partitionIndex: 0,
            batches: [batch1, batch2]);

        // Act
        var bytes = KafkaConsumer<string, string>.EstimatePendingFetchBytes(pending);

        // Assert: 38 (partition) + (500 + 12) + (700 + 12) = 38 + 512 + 712 = 1262
        await Assert.That(bytes).IsEqualTo(38 + 500 + 12 + 700 + 12);
    }

    [Test]
    public async Task LargeBatch_OverheadIsNegligibleProportion()
    {
        // Arrange - 1 MB batch (typical real-world size)
        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BatchLength = 1_048_576, // 1 MB
            BaseTimestamp = 1700000000000L,
            Attributes = 0,
            Records = []
        };

        using var pending = new PendingFetchData(
            "test-topic",
            partitionIndex: 0,
            batches: [batch]);

        // Act
        var bytes = KafkaConsumer<string, string>.EstimatePendingFetchBytes(pending);

        // Assert: overhead is 50 bytes on a 1 MB batch - negligible but present
        var overhead = bytes - batch.BatchLength;
        await Assert.That(overhead).IsEqualTo(38 + 12);
        await Assert.That(bytes).IsGreaterThan(batch.BatchLength);
    }

}
