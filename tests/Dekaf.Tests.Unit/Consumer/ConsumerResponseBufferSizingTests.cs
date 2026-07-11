using Dekaf.Consumer;
using Dekaf.Networking;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerResponseBufferSizingTests
{
    [Test]
    public async Task MaximumPayload_StaticSizing_IncludesOneOversizedPartitionBatch()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            FetchMaxBytes = 50 * 1024 * 1024,
            MaxPartitionFetchBytes = 4 * 1024 * 1024
        };

        var maximumPayload = KafkaConsumer<string, string>
            .CalculateMaximumFetchResponsePayloadBytes(options);

        await Assert.That(maximumPayload).IsEqualTo(54 * 1024 * 1024);
    }

    [Test]
    public async Task MaximumPayload_AdaptiveSizing_UsesConfiguredMaxima()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            FetchMaxBytes = 100 * 1024 * 1024,
            MaxPartitionFetchBytes = 8 * 1024 * 1024,
            EnableAdaptiveFetchSizing = true,
            AdaptiveFetchSizingOptions = new AdaptiveFetchSizingOptions
            {
                InitialFetchMaxBytes = 100 * 1024 * 1024,
                MaxFetchMaxBytes = 200 * 1024 * 1024,
                InitialPartitionFetchBytes = 8 * 1024 * 1024,
                MaxPartitionFetchBytes = 16 * 1024 * 1024
            }
        };

        var maximumPayload = KafkaConsumer<string, string>
            .CalculateMaximumFetchResponsePayloadBytes(options);
        var responsePool = ResponseBufferPool.Create(maximumPayload);

        await Assert.That(maximumPayload).IsEqualTo(216 * 1024 * 1024);
        await Assert.That(responsePool.MaxArrayLength)
            .IsEqualTo(216 * 1024 * 1024 + ResponseBufferPool.ProtocolOverheadBytes);
    }

    [Test]
    public async Task MaximumPayload_NearIntegerLimit_Saturates()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            FetchMaxBytes = int.MaxValue - 10,
            MaxPartitionFetchBytes = 1_024
        };

        var maximumPayload = KafkaConsumer<string, string>
            .CalculateMaximumFetchResponsePayloadBytes(options);

        await Assert.That(maximumPayload).IsEqualTo(int.MaxValue);
    }
}
