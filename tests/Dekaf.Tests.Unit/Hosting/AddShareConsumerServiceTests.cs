using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Extensions.Hosting;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using static Dekaf.Tests.Unit.Hosting.KafkaShareConsumerServiceTests;

namespace Dekaf.Tests.Unit.Hosting;

public class AddShareConsumerServiceTests
{
    [Test]
    public async Task RepeatedKeyedService_ReceivesIndependentConsumersAndOptions()
    {
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder
            .AddShareConsumerService<Worker, string, string>("first", Configure, dlq => dlq.WithTopicSuffix(".first"))
            .AddShareConsumerService<Worker, string, string>("second", Configure, dlq => dlq.WithTopicSuffix(".second")));
        await using var provider = services.BuildServiceProvider();
        var workers = provider.GetServices<IHostedService>().OfType<Worker>().ToArray();
        await Assert.That(workers.Length).IsEqualTo(2);
        await Assert.That(workers[0].Consumer).IsSameReferenceAs(provider.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>("first"));
        await Assert.That(workers[1].Consumer).IsSameReferenceAs(provider.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>("second"));
        await Assert.That(ReferenceEquals(workers[0].Consumer, workers[1].Consumer)).IsFalse();
        await Assert.That(workers[0].ConfiguredDeadLetterOptions!.TopicSuffix).IsEqualTo(".first");
        await Assert.That(workers[1].ConfiguredDeadLetterOptions!.TopicSuffix).IsEqualTo(".second");
        await Assert.That(workers[0].ConfiguredDeadLetterOptions!.BootstrapServers).IsEqualTo("localhost:9092");
    }

    [Test]
    public async Task RepeatedKeyedService_AllInstancesStartAndProcess()
    {
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder
            .AddShareConsumerService<Worker, string, string>("first", Configure)
            .AddShareConsumerService<Worker, string, string>("second", Configure));
        // Replace only private consumer factories, preserving aliases and hosted activation.
        var descriptors = services.Where(x => x.IsKeyedService &&
            x.ServiceType == typeof(IKafkaShareConsumer<string, string>) && x.ServiceKey is not string).ToArray();
        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
            services.AddKeyedSingleton<IKafkaShareConsumer<string, string>>(descriptor.ServiceKey, new TestConsumer(Record(0)));
        }
        await using var provider = services.BuildServiceProvider();
        var workers = provider.GetServices<IHostedService>().OfType<Worker>().ToArray();
        foreach (var worker in workers) await worker.StartAsync(default);
        await Task.WhenAll(workers.Select(x => x.ExecuteTask!)).WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(workers.Length).IsEqualTo(2);
        await Assert.That(workers.All(x => x.Processed == 1)).IsTrue();
        await Assert.That(ReferenceEquals(workers[0].Consumer, workers[1].Consumer)).IsFalse();
    }

    [Test]
    public async Task DifferentUnkeyedClasses_DoNotShareConsumers()
    {
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder
            .AddShareConsumerService<Worker, string, string>(Configure)
            .AddShareConsumerService<OtherWorker, string, string>(Configure));
        await using var provider = services.BuildServiceProvider();
        var first = provider.GetServices<IHostedService>().OfType<Worker>().Single();
        var second = provider.GetServices<IHostedService>().OfType<OtherWorker>().Single();
        await Assert.That(ReferenceEquals(first.Consumer, second.Consumer)).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DuplicateRegistration_ThrowsBeforeAnyMutation(bool keyed)
    {
        var services = new ServiceCollection();
        DekafBuilder? registration = null;
        services.AddDekaf(builder =>
        {
            registration = builder;
            if (keyed) builder.AddShareConsumerService<Worker, string, string>("worker", Configure);
            else builder.AddShareConsumerService<Worker, string, string>(Configure);
        });
        var descriptors = services.ToArray();
        var called = false;
        void Duplicate()
        {
            if (keyed) registration!.AddShareConsumerService<Worker, string, string>("worker", c => { called = true; Configure(c); });
            else registration!.AddShareConsumerService<Worker, string, string>(c => { called = true; Configure(c); });
        }
        await Assert.That(Duplicate).Throws<InvalidOperationException>();
        await Assert.That(services.SequenceEqual(descriptors)).IsTrue();
        await Assert.That(called).IsFalse();
    }

    [Test]
    public async Task OrdinaryAndShareServices_WithSameKey_KeepDeadLetterOptionsSeparate()
    {
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder
            .AddConsumerService<OrdinaryWorker, string, string>("worker", c => c.WithBootstrapServers("localhost:9092").WithGroupId("ordinary"),
                dlq => dlq.WithTopicSuffix(".ordinary"))
            .AddShareConsumerService<Worker, string, string>("worker", Configure, dlq => dlq.WithTopicSuffix(".share")));
        await using var provider = services.BuildServiceProvider();
        var workers = provider.GetServices<IHostedService>().ToArray();
        await Assert.That(workers.OfType<OrdinaryWorker>().Single().ConfiguredDeadLetterOptions!.TopicSuffix).IsEqualTo(".ordinary");
        await Assert.That(workers.OfType<Worker>().Single().ConfiguredDeadLetterOptions!.TopicSuffix).IsEqualTo(".share");
        await Assert.That(provider.GetService<DeadLetterOptions>()).IsNull();
    }

    [Test]
    public async Task ProviderConfiguration_RunsOnceAndUsesMatchingServices()
    {
        var services = new ServiceCollection();
        var settings = new Settings("localhost:9092", "from-provider");
        services.AddSingleton(settings);
        var calls = 0;
        services.AddDekaf(builder => builder.AddShareConsumerService<Worker, string, string>("worker", (provider, consumer) =>
        {
            calls++;
            var resolved = provider.GetRequiredService<Settings>();
            consumer.WithBootstrapServers(resolved.Servers).WithGroupId(resolved.Group);
        }, dlq => dlq.WithMaxFailures(3)));
        await Assert.That(calls).IsEqualTo(0);
        await using var provider = services.BuildServiceProvider();
        var worker = provider.GetServices<IHostedService>().OfType<Worker>().Single();
        _ = provider.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>("worker");
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(worker.ConfiguredDeadLetterOptions!.MaxFailures).IsEqualTo(3);
    }

    [Test]
    public async Task TypedAndConfigurationOverloads_ApplyExplicitModeAndFluentOverrides()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BootstrapServers:0"] = "localhost:9092", ["GroupId"] = "configured", ["AcknowledgementMode"] = "Explicit"
        }).Build();
        services.AddDekaf(builder => builder
            .AddShareConsumerService<Worker, string, string>("typed", new ShareConsumerOptions
            { BootstrapServers = ["localhost:9092"], GroupId = "typed", AcknowledgementMode = ShareAcknowledgementMode.Explicit })
            .AddShareConsumerService<Worker, string, string>("config", configuration, consumer => consumer.WithMaxPollRecords(1)));
        await using var provider = services.BuildServiceProvider();
        foreach (var worker in provider.GetServices<IHostedService>().OfType<Worker>())
            await Assert.That(((IShareConsumerConfiguration)worker.Consumer).AcknowledgementMode).IsEqualTo(ShareAcknowledgementMode.Explicit);
    }

    [Test]
    public async Task DirectShareRegistrations_NamespaceDeadLetterKeysByConsumerType()
    {
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder
            .AddConsumer<string, string>("same", c => c.WithBootstrapServers("localhost:9092").WithGroupId("ordinary"), dlq => dlq.WithTopicSuffix(".ordinary"))
            .AddShareConsumer<string, string>("same", Configure, dlq => dlq.WithTopicSuffix(".share"))
            .AddShareConsumer<string, string>(Configure));
        await using var provider = services.BuildServiceProvider();
        await Assert.That(provider.GetRequiredKeyedService<DeadLetterOptions>("same").TopicSuffix).IsEqualTo(".ordinary");
        await Assert.That(provider.GetRequiredKeyedService<DeadLetterOptions>(
            services.Single(descriptor => descriptor.ServiceType == typeof(DeadLetterOptions) && descriptor.ServiceKey is not string).ServiceKey).TopicSuffix).IsEqualTo(".share");
        await Assert.That(ReferenceEquals(provider.GetRequiredService<IKafkaShareConsumer<string, string>>(),
            provider.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>("same"))).IsFalse();
    }

    [Test]
    public async Task MissingDeadLetterForwarding_ProducesUsefulActivationError()
    {
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder.AddShareConsumerService<OtherWorker, string, string>(Configure, dlq => dlq.WithMaxFailures(1)));
        await using var provider = services.BuildServiceProvider();
        await Assert.That(() => provider.GetServices<IHostedService>().ToArray()).Throws<InvalidOperationException>();
    }

    private static void Configure(ShareConsumerBuilder<string, string> consumer)
        => consumer.WithBootstrapServers("localhost:9092").WithGroupId("shared-group");
    private sealed record Settings(string Servers, string Group);

    private sealed class Worker : KafkaShareConsumerService<string, string>
    {
        public Worker(IKafkaShareConsumer<string, string> consumer, DeadLetterOptions? deadLetterOptions = null)
            : base(consumer, NullLogger.Instance, deadLetterOptions) => Consumer = consumer;
        public IKafkaShareConsumer<string, string> Consumer { get; }
        public int Processed { get; private set; }
        protected override IEnumerable<string> Topics => ["orders"];
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> record, CancellationToken cancellationToken)
        { Processed++; return ValueTask.CompletedTask; }
    }

    private sealed class OtherWorker(IKafkaShareConsumer<string, string> consumer) : KafkaShareConsumerService<string, string>(consumer, NullLogger.Instance)
    {
        public IKafkaShareConsumer<string, string> Consumer { get; } = consumer;
        protected override IEnumerable<string> Topics => ["orders"];
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> record, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class OrdinaryWorker(IKafkaConsumer<string, string> consumer, DeadLetterOptions deadLetterOptions)
        : KafkaConsumerService<string, string>(consumer, NullLogger.Instance, deadLetterOptions)
    {
        protected override IEnumerable<string> Topics => ["orders"];
        protected override ValueTask ProcessAsync(ConsumeResult<string, string> record, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
