using System.Net.Security;
using System.Reflection;
using Dekaf.Admin;
using Dekaf.Networking;

namespace Dekaf.Tests.Unit.Builder;

public sealed class ConnectionBuilderOptionsTests
{
    [Test]
    public async Task ProducerBuilder_WithConnectionOptions_ConfiguresConnectionPool()
    {
        RemoteCertificateValidationCallback callback = (_, _, _, errors) => errors == SslPolicyErrors.None;

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithConnectionTimeout(TimeSpan.FromSeconds(7))
            .WithTcpKeepAlive(TimeSpan.FromSeconds(11), TimeSpan.FromSeconds(3), retryCount: 4)
            .WithRemoteCertificateValidationCallback(callback)
            .Build();

        var options = GetConnectionOptions(producer);

        await AssertConnectionOptions(options, callback, keepAliveEnabled: true);
    }

    [Test]
    public async Task ConsumerBuilder_WithConnectionOptions_ConfiguresConnectionPool()
    {
        RemoteCertificateValidationCallback callback = (_, _, _, errors) => errors == SslPolicyErrors.None;

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("group")
            .WithConnectionTimeout(TimeSpan.FromSeconds(7))
            .WithTcpKeepAlive(TimeSpan.FromSeconds(11), TimeSpan.FromSeconds(3), retryCount: 4)
            .WithRemoteCertificateValidationCallback(callback)
            .Build();

        var options = GetConnectionOptions(consumer);

        await AssertConnectionOptions(options, callback, keepAliveEnabled: true);
    }

    [Test]
    public async Task AdminClientBuilder_WithConnectionOptions_ConfiguresConnectionPool()
    {
        RemoteCertificateValidationCallback callback = (_, _, _, errors) => errors == SslPolicyErrors.None;

        await using var admin = new AdminClientBuilder()
            .WithBootstrapServers("localhost:9092")
            .WithConnectionTimeout(TimeSpan.FromSeconds(7))
            .WithTcpKeepAlive(TimeSpan.FromSeconds(11), TimeSpan.FromSeconds(3), retryCount: 4)
            .WithRemoteCertificateValidationCallback(callback)
            .Build();

        var options = GetConnectionOptions(admin);

        await AssertConnectionOptions(options, callback, keepAliveEnabled: true);
    }

    [Test]
    public async Task KafkaClientBuilder_WithConnectionOptions_ConfiguresSharedConnectionPool()
    {
        RemoteCertificateValidationCallback callback = (_, _, _, errors) => errors == SslPolicyErrors.None;

        await using var client = Kafka.Connect("localhost:9092", builder => builder
            .WithConnectionTimeout(TimeSpan.FromSeconds(7))
            .WithTcpKeepAlive(TimeSpan.FromSeconds(11), TimeSpan.FromSeconds(3), retryCount: 4)
            .WithRemoteCertificateValidationCallback(callback));
        await using var producer = client.CreateProducer<string, string>().Build();

        var options = GetConnectionOptions(producer);

        await AssertConnectionOptions(options, callback, keepAliveEnabled: true);
    }

    [Test]
    public async Task BuilderConnectionOptions_CanDisableTcpKeepAlive()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTcpKeepAlive(false)
            .Build();

        var options = GetConnectionOptions(producer);

        await Assert.That(options.EnableTcpKeepAlive).IsFalse();
    }

    [Test]
    public async Task BuilderConnectionOptions_ValidateInputs()
    {
        var builder = Kafka.CreateProducer<string, string>();

        await Assert.That(() => builder.WithConnectionTimeout(TimeSpan.Zero))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => builder.WithTcpKeepAlive(TimeSpan.Zero, TimeSpan.FromSeconds(1)))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => builder.WithTcpKeepAlive(TimeSpan.FromSeconds(1), TimeSpan.Zero))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => builder.WithTcpKeepAlive(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), retryCount: 0))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => builder.WithRemoteCertificateValidationCallback(null!))
            .Throws<ArgumentNullException>();
    }

    private static async Task AssertConnectionOptions(
        ConnectionOptions options,
        RemoteCertificateValidationCallback callback,
        bool keepAliveEnabled)
    {
        await Assert.That(options.UseTls).IsTrue();
        await Assert.That((object?)options.RemoteCertificateValidationCallback).IsSameReferenceAs(callback);
        await Assert.That(options.ConnectionTimeout).IsEqualTo(TimeSpan.FromSeconds(7));
        await Assert.That(options.EnableTcpKeepAlive).IsEqualTo(keepAliveEnabled);
        await Assert.That(options.TcpKeepAliveTime).IsEqualTo(TimeSpan.FromSeconds(11));
        await Assert.That(options.TcpKeepAliveInterval).IsEqualTo(TimeSpan.FromSeconds(3));
        await Assert.That(options.TcpKeepAliveRetryCount).IsEqualTo(4);
    }

    private static ConnectionOptions GetConnectionOptions(object client)
    {
        var poolField = client.GetType().GetField("_connectionPool", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Could not find _connectionPool on {client.GetType()}");

        return ((ConnectionPool)poolField.GetValue(client)!).EffectiveConnectionOptions;
    }
}
