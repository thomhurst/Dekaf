namespace Dekaf.Tests.Unit.Builder;

public class BootstrapServerListTests
{
    [Test]
    public async Task FromCommaSeparated_TrimsEntries()
    {
        var servers = BootstrapServerList.FromCommaSeparated(" broker1:9092, broker2:9092 ");

        await Assert.That(servers).IsEquivalentTo(["broker1:9092", "broker2:9092"]);
    }

    [Test]
    public async Task FromValues_TrimsEntries()
    {
        var servers = BootstrapServerList.FromValues(" broker1:9092 ", "broker2:9092");

        await Assert.That(servers).IsEquivalentTo(["broker1:9092", "broker2:9092"]);
    }

    [Test]
    public async Task FromValues_NormalizesSchemesTrailingSlashesAndIpv6()
    {
        var servers = BootstrapServerList.FromValues(
            "broker1:9092",
            "PLAINTEXT://broker2:9093/",
            "sasl_ssl://broker3:9094///",
            "[2001:db8::1]:9095",
            "SSL://[2001:db8::2]:9096/");

        await Assert.That(servers).IsEquivalentTo([
            "broker1:9092",
            "broker2:9093",
            "broker3:9094",
            "[2001:db8::1]:9095",
            "[2001:db8::2]:9096",
        ]);
    }

    [Test]
    [Arguments("broker")]
    [Arguments(":9092")]
    [Arguments("broker:not-a-port")]
    [Arguments("broker:0")]
    [Arguments("broker:65536")]
    [Arguments("broker:9092/path")]
    [Arguments("://broker:9092")]
    [Arguments("PLAINTEXT://")]
    [Arguments("2001:db8::1:9092")]
    [Arguments("[2001:db8::1]9092")]
    [Arguments("[not-ipv6]:9092")]
    public void FromValues_RejectsMalformedEntries(string server)
    {
        Assert.Throws<ArgumentException>(() => BootstrapServerList.FromValues(server));
    }

    [Test]
    public void FromCommaSeparated_RejectsEmptyEntries()
    {
        Assert.Throws<ArgumentException>(() =>
            BootstrapServerList.FromCommaSeparated("broker1:9092,,broker2:9092"));
    }

    [Test]
    public void FromValues_RejectsEmptyEntries()
    {
        Assert.Throws<ArgumentException>(() =>
            BootstrapServerList.FromValues("broker1:9092", " "));
    }

    [Test]
    public void FromValues_RejectsNullEntries()
    {
        Assert.Throws<ArgumentException>(() =>
            BootstrapServerList.FromValues("broker1:9092", null!));
    }

    [Test]
    public void FromValues_RejectsEmptyList()
    {
        Assert.Throws<ArgumentException>(() =>
            BootstrapServerList.FromValues());
    }
}
