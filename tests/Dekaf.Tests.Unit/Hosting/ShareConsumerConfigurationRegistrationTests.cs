using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Networking;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Extensions.Hosting;
using Dekaf.Security.Sasl;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Tests.Unit.Hosting;

public class ShareConsumerConfigurationRegistrationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConfigurationAndProviderOverload_BindsThenRunsCallback(bool keyed)
    {
        var configuration = Configuration(new()
        {
            ["BootstrapServers"] = "config-1:9092, config-2:9092",
            ["GroupId"] = "configured",
            ["MaxPollRecords"] = "7",
            ["ClientId"] = "from-config"
        });
        var services = new ServiceCollection();
        services.AddSingleton(new Settings("from-provider"));
        services.AddDekaf(builder =>
        {
            void Configure(IServiceProvider provider, ShareConsumerBuilder<string, string> consumer) =>
                consumer.WithClientId(provider.GetRequiredService<Settings>().ClientId).WithMaxPollRecords(3);

            if (keyed) builder.AddShareConsumerFromConfiguration<string, string>("orders", configuration, Configure);
            else builder.AddShareConsumerFromConfiguration<string, string>(configuration, Configure);
        });
        await using var provider = services.BuildServiceProvider();

        var options = Options(keyed
            ? provider.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>("orders")
            : provider.GetRequiredService<IKafkaShareConsumer<string, string>>());

        await Assert.That(options.BootstrapServers).IsEquivalentTo(["config-1:9092", "config-2:9092"]);
        await Assert.That(options.GroupId).IsEqualTo("configured");
        await Assert.That(options.ClientId).IsEqualTo("from-provider");
        await Assert.That(options.MaxPollRecords).IsEqualTo(3);
    }

    [Test]
    public async Task ConfigurationBinding_LeavesUnsetOptionsToTheBuilder()
    {
        // Only connection and security keys are configured; the group comes from code.
        var configuration = Configuration(new()
        {
            ["BootstrapServers:0"] = "broker:9092",
            ["UseTls"] = "true",
            ["SaslMechanism"] = "ScramSha512",
            ["SaslUsername"] = "user",
            ["SaslPassword"] = "secret"
        });
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder.AddShareConsumer<string, string>(configuration, consumer => consumer
            .WithGroupId("from-code")
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)));
        await using var provider = services.BuildServiceProvider();

        var options = Options(provider.GetRequiredService<IKafkaShareConsumer<string, string>>());

        await Assert.That(options.GroupId).IsEqualTo("from-code");
        await Assert.That(options.AcknowledgementMode).IsEqualTo(ShareAcknowledgementMode.Explicit);
        await Assert.That(options.UseTls).IsTrue();
        await Assert.That(options.SaslMechanism).IsEqualTo(SaslMechanism.ScramSha512);
        await Assert.That(options.SaslUsername).IsEqualTo("user");
    }

    [Test]
    public async Task ConfigurationBinding_BindsEveryShareConsumerSetting()
    {
        var configuration = Configuration(new()
        {
            ["BootstrapServers"] = "broker:9092",
            ["ClientId"] = "client",
            ["GroupId"] = "group",
            ["AutoOffsetReset"] = "Latest",
            ["RackId"] = "rack-a",
            ["FetchMinBytes"] = "11",
            ["FetchMaxBytes"] = "2000000",
            ["MaxPartitionFetchBytes"] = "300000",
            ["FetchMaxWaitMs"] = "150",
            ["MaxPollRecords"] = "42",
            ["AcknowledgementMode"] = "Explicit",
            ["ShareAcquireMode"] = "RecordLimit",
            ["SessionTimeoutMs"] = "30000",
            ["HeartbeatIntervalMs"] = "2000",
            ["RequestTimeoutMs"] = "25000",
            ["RetryBackoffMs"] = "150",
            ["RetryBackoffMaxMs"] = "1500",
            ["ReconnectBackoffMs"] = "60",
            ["ReconnectBackoffMaxMs"] = "1200",
            ["ConnectionsMaxIdleMs"] = "90000",
            ["ConnectionTimeout"] = "00:00:07",
            ["ConnectionTimeoutMax"] = "00:00:40",
            ["TcpKeepAliveTime"] = "00:00:45",
            ["TcpKeepAliveInterval"] = "00:00:05",
            ["TcpKeepAliveRetryCount"] = "4",
            ["SaslMechanism"] = "ScramSha256",
            ["SaslUsername"] = "user",
            ["SaslPassword"] = "secret",
            ["SaslScramMaxIterations"] = "20000",
            ["SocketSendBufferBytes"] = "65536",
            ["SocketReceiveBufferBytes"] = "131072",
            ["ConnectionsPerBroker"] = "3",
            ["ClientDnsLookup"] = "ResolveCanonicalBootstrapServersOnly",
            ["MetadataClusterCheckEnabled"] = "false",
            ["BootstrapResolveTimeoutMs"] = "5000"
        });
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder.AddShareConsumer<string, string>(configuration));
        await using var provider = services.BuildServiceProvider();

        var options = Options(provider.GetRequiredService<IKafkaShareConsumer<string, string>>());

        await Assert.That(options.BootstrapServers).IsEquivalentTo(["broker:9092"]);
        await Assert.That(options.ClientId).IsEqualTo("client");
        await Assert.That(options.GroupId).IsEqualTo("group");
        await Assert.That(options.AutoOffsetReset).IsEqualTo(AutoOffsetReset.Latest);
        await Assert.That(options.RackId).IsEqualTo("rack-a");
        await Assert.That(options.FetchMinBytes).IsEqualTo(11);
        await Assert.That(options.FetchMaxBytes).IsEqualTo(2000000);
        await Assert.That(options.MaxPartitionFetchBytes).IsEqualTo(300000);
        await Assert.That(options.FetchMaxWaitMs).IsEqualTo(150);
        await Assert.That(options.MaxPollRecords).IsEqualTo(42);
        await Assert.That(options.AcknowledgementMode).IsEqualTo(ShareAcknowledgementMode.Explicit);
        await Assert.That(options.ShareAcquireMode).IsEqualTo(ShareAcquireMode.RecordLimit);
        await Assert.That(options.SessionTimeoutMs).IsEqualTo(30000);
        await Assert.That(options.HeartbeatIntervalMs).IsEqualTo(2000);
        await Assert.That(options.RequestTimeoutMs).IsEqualTo(25000);
        await Assert.That(options.RetryBackoffMs).IsEqualTo(150);
        await Assert.That(options.RetryBackoffMaxMs).IsEqualTo(1500);
        await Assert.That(options.ReconnectBackoffMs).IsEqualTo(60);
        await Assert.That(options.ReconnectBackoffMaxMs).IsEqualTo(1200);
        await Assert.That(options.ConnectionsMaxIdleMs).IsEqualTo(90000);
        await Assert.That(options.ConnectionTimeout).IsEqualTo(TimeSpan.FromSeconds(7));
        await Assert.That(options.ConnectionTimeoutMax).IsEqualTo(TimeSpan.FromSeconds(40));
        await Assert.That(options.EnableTcpKeepAlive).IsTrue();
        await Assert.That(options.TcpKeepAliveTime).IsEqualTo(TimeSpan.FromSeconds(45));
        await Assert.That(options.TcpKeepAliveInterval).IsEqualTo(TimeSpan.FromSeconds(5));
        await Assert.That(options.TcpKeepAliveRetryCount).IsEqualTo(4);
        await Assert.That(options.SaslMechanism).IsEqualTo(SaslMechanism.ScramSha256);
        await Assert.That(options.SaslUsername).IsEqualTo("user");
        await Assert.That(options.SaslPassword).IsEqualTo("secret");
        await Assert.That(options.SaslScramMaxIterations).IsEqualTo(20000);
        await Assert.That(options.SocketSendBufferBytes).IsEqualTo(65536);
        await Assert.That(options.SocketReceiveBufferBytes).IsEqualTo(131072);
        await Assert.That(options.ConnectionsPerBroker).IsEqualTo(3);
        await Assert.That(options.ClientDnsLookup).IsEqualTo(ClientDnsLookup.ResolveCanonicalBootstrapServersOnly);
        await Assert.That(options.MetadataClusterCheckEnabled).IsFalse();
        await Assert.That(options.BootstrapResolveTimeoutMs).IsEqualTo(5000);
    }

    [Test]
    public async Task ConfigurationBinding_MapsNegativeIdleTimeoutAndDisabledKeepAlive()
    {
        var configuration = Configuration(new()
        {
            ["BootstrapServers"] = "broker:9092",
            ["GroupId"] = "group",
            ["ConnectionsMaxIdleMs"] = "-1",
            ["EnableTcpKeepAlive"] = "false"
        });
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder.AddShareConsumer<string, string>(configuration));
        await using var provider = services.BuildServiceProvider();

        var options = Options(provider.GetRequiredService<IKafkaShareConsumer<string, string>>());

        await Assert.That(options.ConnectionsMaxIdleMs).IsEqualTo(-1);
        await Assert.That(options.EnableTcpKeepAlive).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EmptyConfigurationSection_LeavesEverySettingToTheCallback(bool keyed)
    {
        // An empty section is valid: the callback supplies the required settings.
        var configuration = Configuration([]);
        var services = new ServiceCollection();
        services.AddDekaf(builder =>
        {
            void Configure(IServiceProvider _, ShareConsumerBuilder<string, string> consumer) =>
                consumer.WithBootstrapServers("code:9092").WithGroupId("from-code");

            if (keyed) builder.AddShareConsumerFromConfiguration<string, string>("orders", configuration, Configure);
            else builder.AddShareConsumerFromConfiguration<string, string>(configuration, Configure);
        });
        await using var provider = services.BuildServiceProvider();

        var options = Options(keyed
            ? provider.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>("orders")
            : provider.GetRequiredService<IKafkaShareConsumer<string, string>>());

        await Assert.That(options.BootstrapServers).IsEquivalentTo(["code:9092"]);
        await Assert.That(options.GroupId).IsEqualTo("from-code");
        await Assert.That(options.ClientId).IsEqualTo("dekaf-share-consumer");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConfigurationAndProviderOverload_RunsLazilyOnceAndRegistersForInitialization(bool keyed)
    {
        var configuration = Configuration(new() { ["BootstrapServers"] = "broker:9092", ["GroupId"] = "group" });
        var calls = 0;
        var services = new ServiceCollection();
        services.AddDekaf(builder =>
        {
            void Configure(IServiceProvider _, ShareConsumerBuilder<string, string> consumer) => calls++;

            if (keyed) builder.AddShareConsumerFromConfiguration<string, string>("orders", configuration, Configure);
            else builder.AddShareConsumerFromConfiguration<string, string>(configuration, Configure);
        });
        await Assert.That(calls).IsEqualTo(0);
        await using var provider = services.BuildServiceProvider();

        var consumer = keyed
            ? provider.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>("orders")
            : provider.GetRequiredService<IKafkaShareConsumer<string, string>>();

        await Assert.That(provider.GetServices<IInitializableKafkaClient>().Single()).IsSameReferenceAs(consumer);
        await Assert.That(calls).IsEqualTo(1);
        if (keyed)
            await Assert.That(provider.GetService<IKafkaShareConsumer<string, string>>()).IsNull();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConfigurationAndProviderOverload_RegistersDeadLetterOptionsWithConfiguredServers(bool keyed)
    {
        var configuration = Configuration(new() { ["BootstrapServers"] = "config:9092", ["GroupId"] = "group" });
        var services = new ServiceCollection();
        services.AddDekaf(builder =>
        {
            void Configure(IServiceProvider _, ShareConsumerBuilder<string, string> consumer) { }

            if (keyed) builder.AddShareConsumerFromConfiguration<string, string>("orders", configuration, Configure, dlq => dlq.WithTopicSuffix(".dead"));
            else builder.AddShareConsumerFromConfiguration<string, string>(configuration, Configure, dlq => dlq.WithTopicSuffix(".dead"));
        });
        await using var provider = services.BuildServiceProvider();

        var descriptor = services.Single(d => d.ServiceType == typeof(DeadLetterOptions));
        var deadLetter = provider.GetRequiredKeyedService<DeadLetterOptions>(descriptor.ServiceKey);

        await Assert.That(deadLetter.TopicSuffix).IsEqualTo(".dead");
        await Assert.That(deadLetter.BootstrapServers).IsEqualTo("config:9092");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task HostedConfigurationAndProviderOverload_ForwardsDeadLetterOptions(bool keyed)
    {
        var configuration = Configuration(new() { ["BootstrapServers"] = "config:9092", ["GroupId"] = "workers" });
        var services = new ServiceCollection();
        services.AddDekaf(builder =>
        {
            void Configure(IServiceProvider _, ShareConsumerBuilder<string, string> consumer) { }

            if (keyed) builder.AddShareConsumerServiceFromConfiguration<DeadLetterWorker, string, string>("orders", configuration, Configure, dlq => dlq.WithMaxFailures(4));
            else builder.AddShareConsumerServiceFromConfiguration<DeadLetterWorker, string, string>(configuration, Configure, dlq => dlq.WithMaxFailures(4));
        });
        await using var provider = services.BuildServiceProvider();

        var worker = provider.GetServices<IHostedService>().OfType<DeadLetterWorker>().Single();

        await Assert.That(worker.DeadLetter!.MaxFailures).IsEqualTo(4);
        await Assert.That(worker.DeadLetter.BootstrapServers).IsEqualTo("config:9092");
        await Assert.That(worker.Consumer).IsSameReferenceAs(Own<DeadLetterWorker>(provider, keyed ? "orders" : null));
        await Assert.That(provider.GetService<DeadLetterOptions>()).IsNull();
    }

    [Test]
    [Arguments(null, null, ShareAcknowledgementMode.Explicit)]
    [Arguments("Implicit", null, ShareAcknowledgementMode.Implicit)]
    [Arguments("Implicit", ShareAcknowledgementMode.Explicit, ShareAcknowledgementMode.Explicit)]
    [Arguments(null, ShareAcknowledgementMode.Implicit, ShareAcknowledgementMode.Implicit)]
    public async Task HostedConfigurationAndProviderOverload_AppliesDefaultThenConfigurationThenCallback(
        string? configured, ShareAcknowledgementMode? fromCallback, ShareAcknowledgementMode expected)
    {
        var values = new Dictionary<string, string?> { ["BootstrapServers"] = "broker:9092", ["GroupId"] = "workers" };
        if (configured is not null) values["AcknowledgementMode"] = configured;
        var configuration = Configuration(values);
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder.AddShareConsumerServiceFromConfiguration<Worker, string, string>(
            configuration,
            (_, consumer) =>
            {
                if (fromCallback is { } mode) consumer.WithAcknowledgementMode(mode);
            }));
        await using var provider = services.BuildServiceProvider();

        var worker = provider.GetServices<IHostedService>().OfType<Worker>().Single();

        await Assert.That(Options(worker.Consumer).AcknowledgementMode).IsEqualTo(expected);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task HostedConfigurationAndProviderOverload_RejectsDuplicateServiceAndKey(bool keyed)
    {
        var configuration = Configuration(new() { ["BootstrapServers"] = "broker:9092", ["GroupId"] = "workers" });
        var services = new ServiceCollection();
        DekafBuilder builder = null!;
        services.AddDekaf(dekaf =>
        {
            builder = dekaf;
            if (keyed) dekaf.AddShareConsumerService<Worker, string, string>("orders", Configure);
            else dekaf.AddShareConsumerService<Worker, string, string>(Configure);
        });
        var registered = services.ToArray();

        void Duplicate()
        {
            if (keyed) builder.AddShareConsumerServiceFromConfiguration<Worker, string, string>("orders", configuration, (_, _) => { });
            else builder.AddShareConsumerServiceFromConfiguration<Worker, string, string>(configuration, (_, _) => { });
        }

        await Assert.That(Duplicate).Throws<InvalidOperationException>();
        await Assert.That(services.SequenceEqual(registered)).IsTrue();
    }

    [Test]
    public async Task ConfigurationAndProviderOverloads_RejectNullArguments()
    {
        var services = new ServiceCollection();
        DekafBuilder builder = null!;
        services.AddDekaf(dekaf => builder = dekaf);
        var registered = services.Count;
        var configuration = Configuration([]);
        Action<IServiceProvider, ShareConsumerBuilder<string, string>> configure = (_, _) => { };
        Action<IServiceProvider, ShareConsumerBuilder<string, string>> missing = null!;

        await Assert.That(() => DekafBuilderShareConsumerExtensions.AddShareConsumerFromConfiguration(null!, configuration, configure)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddShareConsumerFromConfiguration<string, string>(null!, configure)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddShareConsumerFromConfiguration(configuration, missing)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddShareConsumerFromConfiguration(null!, configuration, configure)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddShareConsumerFromConfiguration<string, string>("orders", null!, configure)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddShareConsumerFromConfiguration("orders", configuration, missing)).Throws<ArgumentNullException>();
        await Assert.That(() => DekafBuilderShareConsumerHostingExtensions.AddShareConsumerServiceFromConfiguration<Worker, string, string>(null!, configuration, configure)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddShareConsumerServiceFromConfiguration<Worker, string, string>(null!, configure)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddShareConsumerServiceFromConfiguration<Worker, string, string>(configuration, missing)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddShareConsumerServiceFromConfiguration<Worker, string, string>(null!, configuration, configure)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddShareConsumerServiceFromConfiguration<Worker, string, string>("orders", null!, configure)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddShareConsumerServiceFromConfiguration<Worker, string, string>("orders", configuration, missing)).Throws<ArgumentNullException>();
        await Assert.That(services.Count).IsEqualTo(registered);
    }

    [Test]
    public async Task ServiceKey_HasValueEquality()
    {
        await Assert.That(KafkaShareConsumerServiceKey.For<Worker>()).IsEqualTo(new KafkaShareConsumerServiceKey(typeof(Worker), null));
        await Assert.That(KafkaShareConsumerServiceKey.For<Worker>("orders").GetHashCode())
            .IsEqualTo(KafkaShareConsumerServiceKey.For<Worker>("orders").GetHashCode());
        await Assert.That(KafkaShareConsumerServiceKey.For<Worker>("orders")).IsNotEqualTo(KafkaShareConsumerServiceKey.For<Worker>("billing"));
        await Assert.That(KafkaShareConsumerServiceKey.For<Worker>("orders")).IsNotEqualTo(KafkaShareConsumerServiceKey.For<OtherWorker>("orders"));
        await Assert.That(KafkaShareConsumerServiceKey.For<Worker>()).IsNotEqualTo(KafkaShareConsumerServiceKey.For<Worker>("orders"));
    }

    [Test]
    public async Task ServiceKey_DoesNotResolveUnregisteredWorker()
    {
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder.AddShareConsumerService<Worker, string, string>("orders", Configure));
        await using var provider = services.BuildServiceProvider();

        await Assert.That(provider.GetKeyedService<IKafkaShareConsumer<string, string>>(KafkaShareConsumerServiceKey.For<Worker>())).IsNull();
        await Assert.That(provider.GetKeyedService<IKafkaShareConsumer<string, string>>(KafkaShareConsumerServiceKey.For<OtherWorker>("orders"))).IsNull();
        await Assert.That(provider.GetKeyedService<IKafkaShareConsumer<string, string>>(KafkaShareConsumerServiceKey.For<Worker>("orders"))).IsNotNull();
    }

    [Test]
    public async Task ConfigurationBinding_WithoutClientId_UsesShareConsumerDefault()
    {
        var configuration = Configuration(new() { ["BootstrapServers"] = "broker:9092", ["GroupId"] = "workers" });
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder.AddShareConsumer<string, string>(configuration));
        await using var provider = services.BuildServiceProvider();

        var options = Options(provider.GetRequiredService<IKafkaShareConsumer<string, string>>());

        await Assert.That(options.ClientId).IsEqualTo("dekaf-share-consumer");
    }

    [Test]
    [Arguments(null)]
    [Arguments("Earliest")]
    public async Task ConfigurationBinding_RejectsDurationWithoutByDurationPolicy(string? policy)
    {
        var values = new Dictionary<string, string?>
        {
            ["BootstrapServers"] = "broker:9092",
            ["GroupId"] = "workers",
            ["AutoOffsetResetDuration"] = "00:05:00"
        };
        if (policy is not null) values["AutoOffsetReset"] = policy;
        var configuration = Configuration(values);
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder.AddShareConsumer<string, string>(configuration));
        await using var provider = services.BuildServiceProvider();

        await Assert.That(() => provider.GetRequiredService<IKafkaShareConsumer<string, string>>())
            .Throws<InvalidOperationException>()
            .WithMessageContaining("AutoOffsetResetDuration");
    }

    [Test]
    public async Task ConfigurationOverload_AcceptsNullCallbackWithDeadLetterQueue()
    {
        // A null callback must bind to the nullable configuration overload, not a provider-aware one.
        var configuration = Configuration(new() { ["BootstrapServers"] = "broker:9092", ["GroupId"] = "workers" });
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder
            .AddShareConsumer<string, string>(configuration, null, dlq => dlq.WithTopicSuffix(".dlq"))
            .AddShareConsumer<string, string>("orders", configuration, null, dlq => dlq.WithTopicSuffix(".dlq")));
        await using var provider = services.BuildServiceProvider();

        await Assert.That(Options(provider.GetRequiredService<IKafkaShareConsumer<string, string>>()).GroupId)
            .IsEqualTo("workers");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task HostedConfigurationAndProviderOverload_DefaultsToExplicitAcknowledgement(bool keyed)
    {
        var configuration = Configuration(new() { ["BootstrapServers"] = "broker:9092", ["GroupId"] = "workers" });
        var services = new ServiceCollection();
        services.AddSingleton(new Settings("worker-client"));
        services.AddDekaf(builder =>
        {
            void Configure(IServiceProvider provider, ShareConsumerBuilder<string, string> consumer) =>
                consumer.WithClientId(provider.GetRequiredService<Settings>().ClientId);

            if (keyed) builder.AddShareConsumerServiceFromConfiguration<Worker, string, string>("orders", configuration, Configure);
            else builder.AddShareConsumerServiceFromConfiguration<Worker, string, string>(configuration, Configure);
        });
        await using var provider = services.BuildServiceProvider();

        var worker = provider.GetServices<IHostedService>().OfType<Worker>().Single();
        var options = Options(worker.Consumer);

        await Assert.That(options.AcknowledgementMode).IsEqualTo(ShareAcknowledgementMode.Explicit);
        await Assert.That(options.ClientId).IsEqualTo("worker-client");
        await Assert.That(options.GroupId).IsEqualTo("workers");
    }

    [Test]
    public async Task ServiceKey_ResolvesEachHostedServicesOwnConsumer()
    {
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder
            .AddShareConsumerService<Worker, string, string>(Configure)
            .AddShareConsumerService<OtherWorker, string, string>(Configure)
            .AddShareConsumerService<Worker, string, string>("orders", Configure));
        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().ToArray();

        var worker = hosted.OfType<Worker>().Single(w => ReferenceEquals(w.Consumer, Own<Worker>(provider)));
        var other = hosted.OfType<OtherWorker>().Single();
        var keyed = hosted.OfType<Worker>().Single(w => !ReferenceEquals(w, worker));

        await Assert.That(other.Consumer).IsSameReferenceAs(Own<OtherWorker>(provider));
        await Assert.That(keyed.Consumer).IsSameReferenceAs(Own<Worker>(provider, "orders"));
        await Assert.That(Own<Worker>(provider)).IsNotSameReferenceAs(Own<OtherWorker>(provider));
        await Assert.That(KafkaShareConsumerServiceKey.For<Worker>("orders"))
            .IsEqualTo(new KafkaShareConsumerServiceKey(typeof(Worker), "orders"));
    }

    private static IKafkaShareConsumer<string, string> Own<TService>(IServiceProvider provider, object? serviceKey = null) =>
        provider.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>(KafkaShareConsumerServiceKey.For<TService>(serviceKey));

    private static void Configure(ShareConsumerBuilder<string, string> consumer)
        => consumer.WithBootstrapServers("localhost:9092").WithGroupId("shared-group");

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ShareConsumerOptions Options(IKafkaShareConsumer<string, string> consumer) =>
        (ShareConsumerOptions)consumer.GetType().GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(consumer)!;

    private sealed record Settings(string ClientId);

    private sealed class Worker(IKafkaShareConsumer<string, string> consumer)
        : KafkaShareConsumerService<string, string>(consumer, NullLogger.Instance)
    {
        public IKafkaShareConsumer<string, string> Consumer { get; } = consumer;
        protected override IEnumerable<string> Topics => ["orders"];
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> record, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class DeadLetterWorker(IKafkaShareConsumer<string, string> consumer, DeadLetterOptions? deadLetter = null)
        : KafkaShareConsumerService<string, string>(consumer, NullLogger.Instance, deadLetter)
    {
        public IKafkaShareConsumer<string, string> Consumer { get; } = consumer;
        public DeadLetterOptions? DeadLetter { get; } = deadLetter;
        protected override IEnumerable<string> Topics => ["orders"];
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> record, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class OtherWorker(IKafkaShareConsumer<string, string> consumer)
        : KafkaShareConsumerService<string, string>(consumer, NullLogger.Instance)
    {
        public IKafkaShareConsumer<string, string> Consumer { get; } = consumer;
        protected override IEnumerable<string> Topics => ["orders"];
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> record, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
