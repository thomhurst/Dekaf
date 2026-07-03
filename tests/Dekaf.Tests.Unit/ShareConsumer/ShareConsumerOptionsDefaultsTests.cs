using Dekaf.Networking;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed class ShareConsumerOptionsDefaultsTests
{
    private static ShareConsumerOptions CreateOptions() => new()
    {
        BootstrapServers = ["localhost:9092"],
        GroupId = "share-group"
    };

    [Test]
    public async Task ReconnectBackoffMs_DefaultsTo_50()
    {
        var options = CreateOptions();
        await Assert.That(options.ReconnectBackoffMs).IsEqualTo(50);
    }

    [Test]
    public async Task ReconnectBackoffMaxMs_DefaultsTo_1000()
    {
        var options = CreateOptions();
        await Assert.That(options.ReconnectBackoffMaxMs).IsEqualTo(1000);
    }

    [Test]
    public async Task ConnectionsMaxIdleMs_DefaultsTo_540000()
    {
        var options = CreateOptions();
        await Assert.That(options.ConnectionsMaxIdleMs).IsEqualTo(ConnectionOptions.DefaultConnectionsMaxIdleMs);
    }

    [Test]
    public async Task ClientDnsLookup_DefaultsTo_UseAllDnsIps()
    {
        var options = CreateOptions();
        await Assert.That(options.ClientDnsLookup).IsEqualTo(ClientDnsLookup.UseAllDnsIps);
    }
}
