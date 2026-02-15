using Dekaf.Producer;
using Dekaf.Protocol.Records;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Producer;

public class ProducerOptionsDefaultsTests
{
    private static ProducerOptions CreateOptions() => new()
    {
        BootstrapServers = ["localhost:9092"]
    };

    [Test]
    public async Task ClientId_DefaultsTo_DekafProducer()
    {
        var options = CreateOptions();
        await Assert.That(options.ClientId).IsEqualTo("dekaf-producer");
    }

    [Test]
    public async Task Acks_DefaultsTo_All()
    {
        var options = CreateOptions();
        await Assert.That(options.Acks).IsEqualTo(Acks.All);
    }

    [Test]
    public async Task RequestTimeoutMs_DefaultsTo_30000()
    {
        var options = CreateOptions();
        await Assert.That(options.RequestTimeoutMs).IsEqualTo(30000);
    }

    [Test]
    public async Task LingerMs_DefaultsTo_0()
    {
        var options = CreateOptions();
        await Assert.That(options.LingerMs).IsEqualTo(0);
    }

    [Test]
    public async Task BatchSize_DefaultsTo_1MB()
    {
        var options = CreateOptions();
        await Assert.That(options.BatchSize).IsEqualTo(1048576);
    }

    [Test]
    public async Task BufferMemory_DefaultsTo_1GB()
    {
        var options = CreateOptions();
        await Assert.That(options.BufferMemory).IsEqualTo(1073741824UL);
    }

    [Test]
    public async Task CompressionType_DefaultsTo_None()
    {
        var options = CreateOptions();
        await Assert.That(options.CompressionType).IsEqualTo(CompressionType.None);
    }

    [Test]
    public async Task MaxInFlightRequestsPerConnection_DefaultsTo_5()
    {
        var options = CreateOptions();
        await Assert.That(options.MaxInFlightRequestsPerConnection).IsEqualTo(5);
    }

    [Test]
    public async Task Retries_DefaultsTo_IntMaxValue()
    {
        var options = CreateOptions();
        await Assert.That(options.Retries).IsEqualTo(int.MaxValue);
    }

    [Test]
    public async Task RetryBackoffMs_DefaultsTo_100()
    {
        var options = CreateOptions();
        await Assert.That(options.RetryBackoffMs).IsEqualTo(100);
    }

    [Test]
    public async Task RetryBackoffMaxMs_DefaultsTo_1000()
    {
        var options = CreateOptions();
        await Assert.That(options.RetryBackoffMaxMs).IsEqualTo(1000);
    }

    [Test]
    public async Task DeliveryTimeoutMs_DefaultsTo_120000()
    {
        var options = CreateOptions();
        await Assert.That(options.DeliveryTimeoutMs).IsEqualTo(120000);
    }

    [Test]
    public async Task TransactionalId_DefaultsTo_Null()
    {
        var options = CreateOptions();
        await Assert.That(options.TransactionalId).IsNull();
    }

    [Test]
    public async Task TransactionTimeoutMs_DefaultsTo_60000()
    {
        var options = CreateOptions();
        await Assert.That(options.TransactionTimeoutMs).IsEqualTo(60000);
    }

    [Test]
    public async Task CloseTimeoutMs_DefaultsTo_30000()
    {
        var options = CreateOptions();
        await Assert.That(options.CloseTimeoutMs).IsEqualTo(30000);
    }

    [Test]
    public async Task MaxRequestSize_DefaultsTo_1MB()
    {
        var options = CreateOptions();
        await Assert.That(options.MaxRequestSize).IsEqualTo(1048576);
    }

    [Test]
    public async Task Partitioner_DefaultsTo_Default()
    {
        var options = CreateOptions();
        await Assert.That(options.Partitioner).IsEqualTo(PartitionerType.Default);
    }

    [Test]
    public async Task UseTls_DefaultsTo_False()
    {
        var options = CreateOptions();
        await Assert.That(options.UseTls).IsFalse();
    }

    [Test]
    public async Task SaslMechanism_DefaultsTo_None()
    {
        var options = CreateOptions();
        await Assert.That(options.SaslMechanism).IsEqualTo(SaslMechanism.None);
    }

    [Test]
    public async Task ArenaCapacity_DefaultsTo_0()
    {
        var options = CreateOptions();
        await Assert.That(options.ArenaCapacity).IsEqualTo(0);
    }

    [Test]
    public async Task InitialBatchRecordCapacity_DefaultsTo_0()
    {
        var options = CreateOptions();
        await Assert.That(options.InitialBatchRecordCapacity).IsEqualTo(0);
    }

    [Test]
    public async Task ValueTaskSourcePoolSize_DefaultsTo_4096()
    {
        var options = CreateOptions();
        await Assert.That(options.ValueTaskSourcePoolSize).IsEqualTo(4096);
    }

    [Test]
    public async Task SaslUsername_DefaultsTo_Null()
    {
        var options = CreateOptions();
        await Assert.That(options.SaslUsername).IsNull();
    }

    [Test]
    public async Task SaslPassword_DefaultsTo_Null()
    {
        var options = CreateOptions();
        await Assert.That(options.SaslPassword).IsNull();
    }

    [Test]
    public async Task TlsConfig_DefaultsTo_Null()
    {
        var options = CreateOptions();
        await Assert.That(options.TlsConfig).IsNull();
    }

    [Test]
    public async Task StatisticsInterval_DefaultsTo_Null()
    {
        var options = CreateOptions();
        await Assert.That(options.StatisticsInterval).IsNull();
    }

    [Test]
    public async Task StatisticsHandler_DefaultsTo_Null()
    {
        var options = CreateOptions();
        await Assert.That(options.StatisticsHandler).IsNull();
    }

    [Test]
    public async Task MaxBlockMs_DefaultsTo_60000()
    {
        var options = CreateOptions();
        await Assert.That(options.MaxBlockMs).IsEqualTo(60000);
    }

    [Test]
    public async Task SocketSendBufferBytes_DefaultsTo_0()
    {
        var options = CreateOptions();
        await Assert.That(options.SocketSendBufferBytes).IsEqualTo(0);
    }

    [Test]
    public async Task SocketReceiveBufferBytes_DefaultsTo_0()
    {
        var options = CreateOptions();
        await Assert.That(options.SocketReceiveBufferBytes).IsEqualTo(0);
    }
}
