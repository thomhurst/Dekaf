using System.Net.Security;
using System.Reflection;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Serialization;

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
    public async Task Builders_WithOnlyReconnectBackoff_UseFixedBackoff()
    {
        var expected = TimeSpan.FromMilliseconds(123);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithReconnectBackoff(expected)
            .Build();
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("consumer-group")
            .WithReconnectBackoff(expected)
            .Build();
        await using var shareConsumer = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("share-group")
            .WithReconnectBackoff(expected)
            .Build();
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers("localhost:9092")
            .WithReconnectBackoff(expected)
            .Build();
        await using var client = Kafka.Connect("localhost:9092", builder => builder
            .WithReconnectBackoff(expected));
        await using var clientProducer = client.CreateProducer<string, string>().Build();

        await AssertFixedReconnectBackoff(GetConnectionOptions(producer), expected);
        await AssertFixedReconnectBackoff(GetConnectionOptions(consumer), expected);
        await AssertFixedReconnectBackoff(GetConnectionOptions(shareConsumer), expected);
        await AssertFixedReconnectBackoff(GetConnectionOptions(admin), expected);
        await AssertFixedReconnectBackoff(GetConnectionOptions(clientProducer), expected);
    }

    [Test]
    public async Task DirectClientOptions_WithOnlyReconnectBackoff_UseFixedBackoff()
    {
        var expected = TimeSpan.FromMilliseconds(123);

        await using var producer = new KafkaProducer<string, string>(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ReconnectBackoffMs = 123
            },
            Serializers.String,
            Serializers.String);
        await using var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "consumer-group",
                ReconnectBackoffMs = 123
            },
            Serializers.String,
            Serializers.String);
        await using var admin = new AdminClient(new AdminClientOptions
        {
            BootstrapServers = ["localhost:9092"],
            ReconnectBackoffMs = 123
        });

        await AssertFixedReconnectBackoff(GetConnectionOptions(producer), expected);
        await AssertFixedReconnectBackoff(GetConnectionOptions(consumer), expected);
        await AssertFixedReconnectBackoff(GetConnectionOptions(admin), expected);
    }

    [Test]
    public async Task ProducerBuilder_WithBothReconnectBackoffs_PreservesExponentialRange()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithReconnectBackoff(TimeSpan.FromMilliseconds(123))
            .WithReconnectBackoffMax(TimeSpan.FromMilliseconds(456))
            .Build();

        var options = GetConnectionOptions(producer);

        await Assert.That(options.ReconnectBackoff).IsEqualTo(TimeSpan.FromMilliseconds(123));
        await Assert.That(options.ReconnectBackoffMax).IsEqualTo(TimeSpan.FromMilliseconds(456));
    }

    [Test]
    public async Task Builders_WithRetryBackoff_PreserveCommonSettings()
    {
        var initial = TimeSpan.FromMilliseconds(123);
        var maximum = TimeSpan.FromMilliseconds(456);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithRetryBackoff(initial)
            .WithRetryBackoffMax(maximum)
            .Build();
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("consumer-group")
            .WithRetryBackoff(initial)
            .WithRetryBackoffMax(maximum)
            .Build();
        await using var shareConsumer = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("share-group")
            .WithRetryBackoff(initial)
            .WithRetryBackoffMax(maximum)
            .Build();
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers("localhost:9092")
            .WithRetryBackoff(initial)
            .WithRetryBackoffMax(maximum)
            .Build();

        await AssertRetryBackoff(GetPrivateOptions<ProducerOptions>(producer));
        await AssertRetryBackoff(GetPrivateOptions<ConsumerOptions>(consumer));
        await AssertRetryBackoff(GetPrivateOptions<Dekaf.ShareConsumer.ShareConsumerOptions>(shareConsumer));
        await AssertRetryBackoff(GetPrivateOptions<AdminClientOptions>(admin));
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

    private static async Task AssertFixedReconnectBackoff(ConnectionOptions options, TimeSpan expected)
    {
        await Assert.That(options.ReconnectBackoff).IsEqualTo(expected);
        await Assert.That(options.ReconnectBackoffMax).IsEqualTo(expected);
    }

    private static ConnectionOptions GetConnectionOptions(object client)
    {
        var poolField = client.GetType().GetField("_connectionPool", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Could not find _connectionPool on {client.GetType()}");

        return ((ConnectionPool)poolField.GetValue(client)!).EffectiveConnectionOptions;
    }

    private static TOptions GetPrivateOptions<TOptions>(object client)
    {
        var optionsField = client.GetType().GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Could not find _options on {client.GetType()}");
        return (TOptions)optionsField.GetValue(client)!;
    }

    private static async Task AssertRetryBackoff(object options)
    {
        var optionsType = options.GetType();
        var initial = (int)optionsType.GetProperty("RetryBackoffMs")!.GetValue(options)!;
        var maximum = (int)optionsType.GetProperty("RetryBackoffMaxMs")!.GetValue(options)!;
        await Assert.That(initial).IsEqualTo(123);
        await Assert.That(maximum).IsEqualTo(456);
    }
}
