using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Internal;
using Dekaf.Networking;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerResponseBufferSizingTests
{
    [Test]
    public async Task QueuedPrefetchBudget_DoesNotRaiseConfiguredLimit()
    {
        var configured = 64UL * 1024 * 1024;

        var effective = KafkaConsumer<string, string>.CalculatePrefetchMaxBytes(configured);

        await Assert.That(effective).IsEqualTo(64L * 1024 * 1024);
    }

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

    [Test]
    public async Task ConsumerInfrastructure_SizesResponsePoolForAdaptiveConnectionCeiling()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092", "localhost:9093"],
            ConnectionsPerBroker = 1,
            MaxConnectionsPerBroker = 4,
            EnableAdaptiveConnections = true,
            PrefetchPipelineDepth = 3
        };
        await using var consumer = new KafkaConsumer<string, string>(
            options,
            Serializers.String,
            Serializers.String);

        var connectionPool = GetField<ConnectionPool>(consumer, "_connectionPool");
        var responsePool = GetField<ResponseBufferPool>(connectionPool, "_responseBufferPool");

        var expectedWorkingSet = PoolSizing.ForConsumerResponseBuffers(
            options.BootstrapServers.Count,
            options.PrefetchPipelineDepth,
            options.MaxConnectionsPerBroker);

        await Assert.That(responsePool.ManagedArraysPerBucket).IsEqualTo(expectedWorkingSet);
        await Assert.That(responsePool.MaxRetainedNativeBuffers).IsEqualTo(expectedWorkingSet);
    }

    private static T GetField<T>(object target, string name) =>
        (T)(target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(target)!);
}
