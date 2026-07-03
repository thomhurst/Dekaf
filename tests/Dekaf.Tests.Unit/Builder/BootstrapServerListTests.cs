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
