using System.Reflection;
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

            if (keyed) builder.AddShareConsumer<string, string>("orders", configuration, Configure);
            else builder.AddShareConsumer<string, string>(configuration, Configure);
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

            if (keyed) builder.AddShareConsumerService<Worker, string, string>("orders", configuration, Configure);
            else builder.AddShareConsumerService<Worker, string, string>(configuration, Configure);
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

    private sealed class OtherWorker(IKafkaShareConsumer<string, string> consumer)
        : KafkaShareConsumerService<string, string>(consumer, NullLogger.Instance)
    {
        public IKafkaShareConsumer<string, string> Consumer { get; } = consumer;
        protected override IEnumerable<string> Topics => ["orders"];
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> record, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
