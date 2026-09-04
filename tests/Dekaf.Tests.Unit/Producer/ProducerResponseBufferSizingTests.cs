using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Producer;

public sealed class ProducerResponseBufferSizingTests
{
    [Test]
    public async Task ProducerInfrastructure_SizesResponsePoolForPeakInFlightWorkingSet()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092", "localhost:9093"],
            ConnectionsPerBroker = 1,
            MaxConnectionsPerBroker = 3,
            MaxInFlightRequestsPerConnection = 5
        };
        await using var producer = new KafkaProducer<string, string>(
            options,
            Serializers.String,
            Serializers.String);

        var connectionPool = AccumulatorTestHelpers.GetPrivateField<ConnectionPool>(producer, "_connectionPool");
        var responsePool = AccumulatorTestHelpers.GetPrivateField<ResponseBufferPool>(connectionPool, "_responseBufferPool");

        var expected = PoolSizing.ForSharedPools(
            brokerCount: options.BootstrapServers.Count,
            connectionsPerBroker: options.ConnectionsPerBroker,
            maxInFlightRequestsPerConnection: options.MaxInFlightRequestsPerConnection,
            batchSize: options.BatchSize,
            maxConnectionsPerBroker: options.MaxConnectionsPerBroker).ResponseBuffersPerBucket;
        await Assert.That(responsePool.ManagedArraysPerBucket).IsEqualTo(expected);
        await Assert.That(responsePool.MaxRetainedNativeBuffers).IsEqualTo(expected);
        await Assert.That(responsePool.MaxArrayLength).IsEqualTo(ResponseBufferPool.DefaultMaxArrayLength);
        await Assert.That(responsePool).IsNotSameReferenceAs(ResponseBufferPool.Default);
    }

    [Test]
    public async Task ProducerInfrastructure_RatchetsResponsePoolWhenMetadataDiscoversMoreBrokers()
    {
        // One seed endpoint for a larger cluster: the pool is sized from the seed count and
        // must grow to the discovered topology's working set, never shrink.
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ConnectionsPerBroker = 1,
            MaxConnectionsPerBroker = 9,
            MaxInFlightRequestsPerConnection = 5
        };
        await using var producer = new KafkaProducer<string, string>(
            options,
            Serializers.String,
            Serializers.String);

        var connectionPool = AccumulatorTestHelpers.GetPrivateField<ConnectionPool>(producer, "_connectionPool");
        var responsePool = AccumulatorTestHelpers.GetPrivateField<ResponseBufferPool>(connectionPool, "_responseBufferPool");
        var metadataManager = AccumulatorTestHelpers.GetPrivateField<MetadataManager>(producer, "_metadataManager");
        var seeded = PoolSizing.ForSharedPools(
            brokerCount: 1,
            connectionsPerBroker: options.ConnectionsPerBroker,
            maxInFlightRequestsPerConnection: options.MaxInFlightRequestsPerConnection,
            batchSize: options.BatchSize,
            maxConnectionsPerBroker: options.MaxConnectionsPerBroker).ResponseBuffersPerBucket;
        await Assert.That(responsePool.MaxRetainedNativeBuffers).IsEqualTo(seeded);

        const int discoveredBrokers = 10;
        metadataManager.NotifyBrokerCountDiscovered(discoveredBrokers);

        var expected = PoolSizing.ForSharedPools(
            brokerCount: discoveredBrokers,
            connectionsPerBroker: options.ConnectionsPerBroker,
            maxInFlightRequestsPerConnection: options.MaxInFlightRequestsPerConnection,
            batchSize: options.BatchSize,
            maxConnectionsPerBroker: options.MaxConnectionsPerBroker).ResponseBuffersPerBucket;
        await Assert.That(expected).IsGreaterThan(seeded);
        await Assert.That(responsePool.ManagedArraysPerBucket).IsEqualTo(expected);
        await Assert.That(responsePool.MaxRetainedNativeBuffers).IsEqualTo(expected);

        metadataManager.NotifyBrokerCountDiscovered(1);

        await Assert.That(responsePool.MaxRetainedNativeBuffers).IsEqualTo(expected)
            .Because("a smaller metadata response never shrinks the pool");
    }
}
