using Dekaf.Internal;
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
}
