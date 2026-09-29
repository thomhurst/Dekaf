using Dekaf.Admin;
using Dekaf.Aspire;
using Dekaf.Consumer;
using Dekaf.Extensions.HealthChecks;
using Dekaf.Extensions.Hosting;
using Dekaf.Producer;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using static Dekaf.Tests.Unit.Aspire.AspireTestHost;

namespace Dekaf.Tests.Unit.Aspire;

public class AspireClientRegistrationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Producer_AppliesConfigurationPrecedence(bool keyed)
    {
        var builder = HostBuilder(
            ("ConnectionStrings:messaging", "connection:9092"),
            ("Aspire:Dekaf:Producer:ConnectionString", "shared:9092"),
            ("Aspire:Dekaf:Producer:Config:ClientId", "shared"),
            ("Aspire:Dekaf:Producer:Config:LingerMs", "7"),
            ("Aspire:Dekaf:Producer:messaging:Config:ClientId", "named"));
        if (keyed) builder.AddKeyedDekafProducer<string, string>("messaging");
        else builder.AddDekafProducer<string, string>("messaging");
        await using var provider = builder.Services.BuildServiceProvider();

        var options = Options(keyed
            ? provider.GetRequiredKeyedService<IKafkaProducer<string, string>>("messaging")
            : provider.GetRequiredService<IKafkaProducer<string, string>>());

        await Assert.That(options.BootstrapServers).IsEquivalentTo(["connection:9092"]);
        await Assert.That(options.ClientId).IsEqualTo("named");
        await Assert.That(options.LingerMs).IsEqualTo(7);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Producer_SettingsAndBuilderCallbacksRunLast(bool keyed)
    {
        var builder = HostBuilder(
            ("ConnectionStrings:messaging", "connection:9092"),
            ("Aspire:Dekaf:Producer:Config:ClientId", "configured"));
        builder.Services.AddSingleton(new ClientName("from-services"));
        void Settings(DekafProducerSettings settings) => settings.ConnectionString = "settings:9092";
        void Configure(IServiceProvider services, ProducerBuilder<string, string> producer) =>
            producer.WithClientId(services.GetRequiredService<ClientName>().Value);
        if (keyed) builder.AddKeyedDekafProducer<string, string>("messaging", Settings, Configure);
        else builder.AddDekafProducer<string, string>("messaging", Settings, Configure);
        await using var provider = builder.Services.BuildServiceProvider();

        var options = Options(keyed
            ? provider.GetRequiredKeyedService<IKafkaProducer<string, string>>("messaging")
            : provider.GetRequiredService<IKafkaProducer<string, string>>());

        await Assert.That(options.BootstrapServers).IsEquivalentTo(["settings:9092"]);
        await Assert.That(options.ClientId).IsEqualTo("from-services");
    }

    [Test]
    [Arguments("ConnectionString", "named:9092")]
    [Arguments("Config:BootstrapServers", "named:9092")]
    [Arguments("Config:BootstrapServers:0", "named:9092")]
    public async Task Consumer_NamedConnectionReplacesSharedBrokers(string namedKey, string value)
    {
        var builder = HostBuilder(
            ("Aspire:Dekaf:Consumer:ConnectionString", "shared:9092"),
            ("Aspire:Dekaf:Consumer:Config:BootstrapServers:0", "shared-a:9092"),
            ("Aspire:Dekaf:Consumer:Config:BootstrapServers:1", "shared-b:9092"),
            ("Aspire:Dekaf:Consumer:Config:GroupId", "orders"),
            ($"Aspire:Dekaf:Consumer:messaging:{namedKey}", value));
        builder.AddDekafConsumer<string, string>("messaging");
        await using var provider = builder.Services.BuildServiceProvider();

        var options = Options(provider.GetRequiredService<IKafkaConsumer<string, string>>());

        await Assert.That(options.BootstrapServers).IsEquivalentTo(["named:9092"]);
        await Assert.That(options.GroupId).IsEqualTo("orders");
    }

    [Test]
    public async Task Consumer_BindsHealthCheckOptions()
    {
        var builder = HostBuilder(
            ("ConnectionStrings:messaging", "broker:9092"),
            ("Aspire:Dekaf:Consumer:Config:GroupId", "orders"),
            ("Aspire:Dekaf:Consumer:HealthCheck:Timeout", "00:00:02"),
            ("Aspire:Dekaf:Consumer:messaging:HealthCheck:DegradedThreshold", "5"));
        DekafConsumerSettings? bound = null;
        builder.AddKeyedDekafConsumer<string, string>("messaging", settings => bound = settings);
        await using var provider = builder.Services.BuildServiceProvider();

        var check = HealthCheck(provider, "Dekaf_consumer<System.String,System.String>_messaging").Factory(provider);

        await Assert.That(check).IsTypeOf<DekafConsumerHealthCheck<string, string>>();
        await Assert.That(bound!.HealthCheck.Timeout).IsEqualTo(TimeSpan.FromSeconds(2));
        await Assert.That(bound.HealthCheck.DegradedThreshold).IsEqualTo(5);
    }

    [Test]
    public async Task HealthChecks_AreNamedPerRoleTypeAndKey_AndNotDuplicated()
    {
        var builder = HostBuilder(("ConnectionStrings:messaging", "broker:9092"), ("ConnectionStrings:orders", "broker:9092"));
        builder.AddDekafProducer<string, string>("messaging");
        builder.AddDekafProducer<string, byte[]>("messaging");
        builder.AddKeyedDekafProducer<string, string>("orders");
        builder.AddKeyedDekafProducer<string, string>("orders");
        builder.AddDekafAdminClient("messaging");
        await using var provider = builder.Services.BuildServiceProvider();

        await Assert.That(HealthCheckNames(provider)).IsEquivalentTo([
            "Dekaf_producer<System.String,System.String>",
            "Dekaf_producer<System.String,System.Byte[]>",
            "Dekaf_producer<System.String,System.String>_orders",
            "Dekaf_admin"
        ]);
    }

    [Test]
    public async Task DisabledHealthChecksAndTelemetry_RegisterNeither()
    {
        var builder = HostBuilder(
            ("ConnectionStrings:messaging", "broker:9092"),
            ("Aspire:Dekaf:Producer:DisableHealthChecks", "true"),
            ("Aspire:Dekaf:Producer:DisableMetrics", "true"),
            ("Aspire:Dekaf:Producer:DisableTracing", "true"));
        builder.AddDekafProducer<string, string>("messaging");
        await using var provider = builder.Services.BuildServiceProvider();

        await Assert.That(HealthCheckNames(provider)).IsEmpty();
        await Assert.That(builder.Services.Any(IsOpenTelemetry)).IsFalse();
    }

    [Test]
    public async Task EnabledTelemetry_RegistersOpenTelemetry()
    {
        var builder = HostBuilder(("ConnectionStrings:messaging", "broker:9092"));
        builder.AddDekafProducer<string, string>("messaging");

        await Assert.That(builder.Services.Any(IsOpenTelemetry)).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShareConsumer_BindsConfigurationAndConnectionString(bool keyed)
    {
        var builder = HostBuilder(
            ("ConnectionStrings:messaging", "connection:9092"),
            ("Aspire:Dekaf:ShareConsumer:Config:BootstrapServers", "configured:9092"),
            ("Aspire:Dekaf:ShareConsumer:Config:GroupId", "workers"),
            ("Aspire:Dekaf:ShareConsumer:Config:AcknowledgementMode", "Explicit"),
            ("Aspire:Dekaf:ShareConsumer:Config:SaslMechanism", "ScramSha512"),
            ("Aspire:Dekaf:ShareConsumer:Config:SaslUsername", "user"),
            ("Aspire:Dekaf:ShareConsumer:Config:SaslPassword", "secret"));
        if (keyed) builder.AddKeyedDekafShareConsumer<string, string>("messaging");
        else builder.AddDekafShareConsumer<string, string>("messaging");
        await using var provider = builder.Services.BuildServiceProvider();

        var options = Options(keyed
            ? provider.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>("messaging")
            : provider.GetRequiredService<IKafkaShareConsumer<string, string>>());

        await Assert.That(options.BootstrapServers).IsEquivalentTo(["connection:9092"]);
        await Assert.That(options.GroupId).IsEqualTo("workers");
        await Assert.That(options.AcknowledgementMode).IsEqualTo(ShareAcknowledgementMode.Explicit);
        await Assert.That(options.SaslUsername).IsEqualTo("user");
    }

    [Test]
    public async Task ShareConsumerServices_WithSameTypes_HaveIndependentConsumersAndHealthChecks_AliasResolvesLast()
    {
        var builder = HostBuilder(("ConnectionStrings:messaging", "broker:9092"));
        builder.AddDekafShareConsumerService<Worker, string, string>("messaging",
            configureBuilder: (_, consumer) => consumer.WithGroupId("workers"));
        builder.AddDekafShareConsumerService<OtherWorker, string, string>("messaging",
            configureBuilder: (_, consumer) => consumer.WithGroupId("audit"));
        await using var provider = builder.Services.BuildServiceProvider();

        var hosted = provider.GetServices<IHostedService>().ToArray();
        var worker = hosted.OfType<Worker>().Single();
        var other = hosted.OfType<OtherWorker>().Single();

        await Assert.That(HealthCheckNames(provider)).IsEquivalentTo([
            $"Dekaf_shareconsumer_service<{typeof(Worker)},System.String,System.String>",
            $"Dekaf_shareconsumer_service<{typeof(OtherWorker)},System.String,System.String>"
        ]);
        await Assert.That(Options(worker.Consumer).GroupId).IsEqualTo("workers");
        await Assert.That(Options(other.Consumer).GroupId).IsEqualTo("audit");
        await Assert.That(Options(worker.Consumer).AcknowledgementMode).IsEqualTo(ShareAcknowledgementMode.Explicit);
        await Assert.That(ReferenceEquals(worker.Consumer, other.Consumer)).IsFalse();
        // The public alias follows standard DI semantics: last registration wins, enumeration returns both.
        await Assert.That(provider.GetRequiredService<IKafkaShareConsumer<string, string>>()).IsSameReferenceAs(other.Consumer);
        await Assert.That(provider.GetServices<IKafkaShareConsumer<string, string>>().ToArray())
            .IsEquivalentTo([worker.Consumer, other.Consumer]);
        await Assert.That(provider.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>(
            KafkaShareConsumerServiceKey.For<Worker>())).IsSameReferenceAs(worker.Consumer);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AdminClient_ConnectionStringReplacesConfiguredControllers(bool keyed)
    {
        var builder = HostBuilder(
            ("ConnectionStrings:messaging", "broker:9092"),
            ("Aspire:Dekaf:AdminClient:Config:BootstrapControllers:0", "controller:9093"),
            ("Aspire:Dekaf:AdminClient:Config:ClientId", "admin"));
        if (keyed) builder.AddKeyedDekafAdminClient("messaging");
        else builder.AddDekafAdminClient("messaging");
        await using var provider = builder.Services.BuildServiceProvider();

        var options = Options(keyed
            ? provider.GetRequiredKeyedService<IAdminClient>("messaging")
            : provider.GetRequiredService<IAdminClient>());

        await Assert.That(options.BootstrapServers).IsEquivalentTo(["broker:9092"]);
        await Assert.That(options.BootstrapControllers).IsEmpty();
        await Assert.That(options.ClientId).IsEqualTo("admin");
    }

    [Test]
    public async Task AdminClient_NamedControllersReplaceSharedBrokers()
    {
        var builder = HostBuilder(
            ("Aspire:Dekaf:AdminClient:Config:BootstrapServers", "shared:9092"),
            ("Aspire:Dekaf:AdminClient:controllers:Config:BootstrapControllers", "controller:9093"));
        builder.AddKeyedDekafAdminClient("controllers");
        await using var provider = builder.Services.BuildServiceProvider();

        var options = Options(provider.GetRequiredKeyedService<IAdminClient>("controllers"));

        await Assert.That(options.BootstrapControllers).IsEquivalentTo(["controller:9093"]);
        await Assert.That(options.BootstrapServers).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RootClient_IsSingletonWithOwnHealthClient(bool keyed)
    {
        var builder = HostBuilder(("ConnectionStrings:messaging", "broker:9092"));
        if (keyed) builder.AddKeyedDekafClient("messaging", configureBuilder: (_, client) => client.WithClientId("root"));
        else builder.AddDekafClient("messaging", configureBuilder: (_, client) => client.WithClientId("root"));
        await using var provider = builder.Services.BuildServiceProvider();

        var root = keyed
            ? provider.GetRequiredKeyedService<KafkaClient>("messaging")
            : provider.GetRequiredService<KafkaClient>();
        var name = keyed ? "Dekaf_client_messaging" : "Dekaf_client";

        await Assert.That(root).IsSameReferenceAs(keyed
            ? provider.GetRequiredKeyedService<KafkaClient>("messaging")
            : provider.GetRequiredService<KafkaClient>());
        await Assert.That(HealthCheckNames(provider)).IsEquivalentTo([name]);
        await Assert.That(HealthCheck(provider, name).Factory(provider)).IsTypeOf<DekafBrokerHealthCheck>();
    }

    [Test]
    public async Task MissingConnection_FailsWhenTheClientIsResolved()
    {
        var builder = HostBuilder();
        builder.AddDekafProducer<string, string>("missing");
        await using var provider = builder.Services.BuildServiceProvider();

        await Assert.That(() => provider.GetRequiredService<IKafkaProducer<string, string>>()).Throws<Exception>();
    }

    [Test]
    public async Task Registration_ValidatesArguments()
    {
        var builder = HostBuilder();

        await Assert.That(() => builder.AddDekafProducer<string, string>("")).Throws<ArgumentException>();
        await Assert.That(() => builder.AddKeyedDekafConsumer<string, string>(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => AspireDekafAdminClientExtensions.AddDekafAdminClient(null!, "messaging")).Throws<ArgumentNullException>();
    }

    private static bool IsOpenTelemetry(ServiceDescriptor descriptor) =>
        descriptor.ServiceType.Namespace?.StartsWith("OpenTelemetry", StringComparison.Ordinal) == true;

    private sealed record ClientName(string Value);

    private sealed class Worker(IKafkaShareConsumer<string, string> consumer)
        : KafkaShareConsumerService<string, string>(consumer, NullLogger.Instance)
    {
        public IKafkaShareConsumer<string, string> Consumer { get; } = consumer;
        protected override IEnumerable<string> Topics => ["tasks"];
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> result, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class OtherWorker(IKafkaShareConsumer<string, string> consumer)
        : KafkaShareConsumerService<string, string>(consumer, NullLogger.Instance)
    {
        public IKafkaShareConsumer<string, string> Consumer { get; } = consumer;
        protected override IEnumerable<string> Topics => ["tasks"];
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> result, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
