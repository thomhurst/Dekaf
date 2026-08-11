using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Producer;

public class BrokerSenderDiagnosticFormattingTests
{
    [Test]
    public async Task FormatBatchKeys_SkipsEmptySlotsAndPreservesOrder()
    {
        var first = CreateBatch("orders", 2);
        var second = CreateBatch("payments", 7);
        ReadyBatch[] batches = [first, null!, second];

        var result = BrokerSender.FormatBatchKeys(batches, batches.Length);

        await Assert.That(result).IsEqualTo("orders-2, payments-7");
    }

    [Test]
    public async Task FormatBatchKeys_FormatsPartitionExtremes()
    {
        var minimum = CreateBatch("minimum", int.MinValue);
        var maximum = CreateBatch("maximum", int.MaxValue);
        ReadyBatch[] batches = [minimum, maximum];

        var result = BrokerSender.FormatBatchKeys(batches, batches.Length);

        await Assert.That(result).IsEqualTo($"minimum-{int.MinValue}, maximum-{int.MaxValue}");
    }

    private static ReadyBatch CreateBatch(string topic, int partition)
    {
        var batch = new ReadyBatch();
        batch.Initialize(
            new TopicPartition(topic, partition),
            new RecordBatch { Records = [] },
            completionSourcesArray: null,
            completionSourcesCount: 0,
            recordCount: 0,
            dataSize: 0);
        return batch;
    }
}
