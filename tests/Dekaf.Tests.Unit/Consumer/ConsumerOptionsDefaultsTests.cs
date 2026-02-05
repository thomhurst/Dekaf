using Dekaf.Consumer;
using Dekaf.Protocol.Messages;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Consumer;

public class ConsumerOptionsDefaultsTests
{
    private static ConsumerOptions CreateOptions() => new()
    {
        BootstrapServers = ["localhost:9092"]
    };

    [Test]
    public async Task ClientId_DefaultsTo_DekafConsumer()
    {
        var options = CreateOptions();
        await Assert.That(options.ClientId).IsEqualTo("dekaf-consumer");
    }

    [Test]
    public async Task GroupId_DefaultsTo_Null()
    {
        var options = CreateOptions();
        await Assert.That(options.GroupId).IsNull();
    }

    [Test]
    public async Task GroupInstanceId_DefaultsTo_Null()
    {
        var options = CreateOptions();
        await Assert.That(options.GroupInstanceId).IsNull();
    }

    [Test]
    public async Task OffsetCommitMode_DefaultsTo_Auto()
    {
        var options = CreateOptions();
        await Assert.That(options.OffsetCommitMode).IsEqualTo(OffsetCommitMode.Auto);
    }

    [Test]
    public async Task AutoCommitIntervalMs_DefaultsTo_5000()
    {
        var options = CreateOptions();
        await Assert.That(options.AutoCommitIntervalMs).IsEqualTo(5000);
    }

    [Test]
    public async Task AutoOffsetReset_DefaultsTo_Latest()
    {
        var options = CreateOptions();
        await Assert.That(options.AutoOffsetReset).IsEqualTo(AutoOffsetReset.Latest);
    }

    [Test]
    public async Task FetchMinBytes_DefaultsTo_1()
    {
        var options = CreateOptions();
        await Assert.That(options.FetchMinBytes).IsEqualTo(1);
    }

    [Test]
    public async Task FetchMaxBytes_DefaultsTo_50MB()
    {
        var options = CreateOptions();
        await Assert.That(options.FetchMaxBytes).IsEqualTo(52428800);
    }

    [Test]
    public async Task MaxPartitionFetchBytes_DefaultsTo_1MB()
    {
        var options = CreateOptions();
        await Assert.That(options.MaxPartitionFetchBytes).IsEqualTo(1048576);
    }

    [Test]
    public async Task FetchMaxWaitMs_DefaultsTo_500()
    {
        var options = CreateOptions();
        await Assert.That(options.FetchMaxWaitMs).IsEqualTo(500);
    }

    [Test]
    public async Task MaxPollRecords_DefaultsTo_500()
    {
        var options = CreateOptions();
        await Assert.That(options.MaxPollRecords).IsEqualTo(500);
    }

    [Test]
    public async Task MaxPollIntervalMs_DefaultsTo_300000()
    {
        var options = CreateOptions();
        await Assert.That(options.MaxPollIntervalMs).IsEqualTo(300000);
    }

    [Test]
    public async Task SessionTimeoutMs_DefaultsTo_45000()
    {
        var options = CreateOptions();
        await Assert.That(options.SessionTimeoutMs).IsEqualTo(45000);
    }

    [Test]
    public async Task HeartbeatIntervalMs_DefaultsTo_3000()
    {
        var options = CreateOptions();
        await Assert.That(options.HeartbeatIntervalMs).IsEqualTo(3000);
    }

    [Test]
    public async Task RebalanceTimeoutMs_DefaultsTo_60000()
    {
        var options = CreateOptions();
        await Assert.That(options.RebalanceTimeoutMs).IsEqualTo(60000);
    }

    [Test]
    public async Task PartitionAssignmentStrategy_DefaultsTo_CooperativeSticky()
    {
        var options = CreateOptions();
        await Assert.That(options.PartitionAssignmentStrategy).IsEqualTo(PartitionAssignmentStrategy.CooperativeSticky);
    }

    [Test]
    public async Task IsolationLevel_DefaultsTo_ReadUncommitted()
    {
        var options = CreateOptions();
        await Assert.That(options.IsolationLevel).IsEqualTo(IsolationLevel.ReadUncommitted);
    }

    [Test]
    public async Task RequestTimeoutMs_DefaultsTo_30000()
    {
        var options = CreateOptions();
        await Assert.That(options.RequestTimeoutMs).IsEqualTo(30000);
    }

    [Test]
    public async Task CheckCrcs_DefaultsTo_False()
    {
        var options = CreateOptions();
        await Assert.That(options.CheckCrcs).IsFalse();
    }

    [Test]
    public async Task UseTls_DefaultsTo_False()
    {
        var options = CreateOptions();
        await Assert.That(options.UseTls).IsFalse();
    }

    [Test]
    public async Task TlsConfig_DefaultsTo_Null()
    {
        var options = CreateOptions();
        await Assert.That(options.TlsConfig).IsNull();
    }

    [Test]
    public async Task SaslMechanism_DefaultsTo_None()
    {
        var options = CreateOptions();
        await Assert.That(options.SaslMechanism).IsEqualTo(SaslMechanism.None);
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
    public async Task RebalanceListener_DefaultsTo_Null()
    {
        var options = CreateOptions();
        await Assert.That(options.RebalanceListener).IsNull();
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

    [Test]
    public async Task QueuedMinMessages_DefaultsTo_100000()
    {
        var options = CreateOptions();
        await Assert.That(options.QueuedMinMessages).IsEqualTo(100000);
    }

    [Test]
    public async Task QueuedMaxMessagesKbytes_DefaultsTo_65536()
    {
        var options = CreateOptions();
        await Assert.That(options.QueuedMaxMessagesKbytes).IsEqualTo(65536);
    }

    [Test]
    public async Task EnablePartitionEof_DefaultsTo_False()
    {
        var options = CreateOptions();
        await Assert.That(options.EnablePartitionEof).IsFalse();
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
}
