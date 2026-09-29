using System.Collections.Concurrent;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Extensions.Hosting;
using Dekaf.Producer;
using Dekaf.SchemaRegistry;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Integration.Aspire;

/// <summary>Round trips through clients registered by the Dekaf Aspire integrations.</summary>
[Category("Messaging")]
public sealed class AspireClientIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ProducerConsumerAndAdmin_RoundTripAndReportHealthy(bool keyed)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var builder = AspireHost.Create(("ConnectionStrings:messaging", KafkaContainer.BootstrapServers));
        builder.Configuration.AddInMemoryCollection([
            new("Aspire:Dekaf:Consumer:Config:GroupId", $"aspire-{Guid.NewGuid():N}"),
            new("Aspire:Dekaf:Consumer:Config:AutoOffsetReset", "Earliest")
        ]);
        if (keyed)
        {
            builder.AddKeyedDekafProducer<string, string>("messaging");
            builder.AddKeyedDekafConsumer<string, string>("messaging", consumer => consumer.SubscribeTo(topic));
            builder.AddKeyedDekafAdminClient("messaging");
        }
        else
        {
            builder.AddDekafProducer<string, string>("messaging");
            builder.AddDekafConsumer<string, string>("messaging", consumer => consumer.SubscribeTo(topic));
            builder.AddDekafAdminClient("messaging");
        }

        using var host = builder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await host.StartAsync(timeout.Token);
        var producer = Resolve<IKafkaProducer<string, string>>(host.Services, keyed);
        var consumer = Resolve<IKafkaConsumer<string, string>>(host.Services, keyed);

        await producer.ProduceAsync(topic, "key", "value", timeout.Token);
        string? received = null;
        await foreach (var record in consumer.ConsumeAsync(timeout.Token))
        {
            received = record.Value;
            break;
        }

        var report = await AspireHost.WaitForHealthyAsync(host.Services, timeout.Token);
        await host.StopAsync(timeout.Token);

        await Assert.That(received).IsEqualTo("value");
        await Assert.That(report.Entries.Keys.Count(name => name.StartsWith("Dekaf_", StringComparison.Ordinal))).IsEqualTo(3);
    }

    [Test]
    public async Task RootClient_SharesConnectionsWithChildrenAndReportsHealthy()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var builder = AspireHost.Create(("ConnectionStrings:messaging", KafkaContainer.BootstrapServers));
        builder.AddDekafClient("messaging");

        using var host = builder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await host.StartAsync(timeout.Token);
        var root = host.Services.GetRequiredService<KafkaClient>();
        await using var producer = await root.CreateProducer<string, string>().BuildAsync(timeout.Token);
        var metadata = await producer.ProduceAsync(topic, "key", "value", timeout.Token);

        await AspireHost.WaitForHealthyAsync(host.Services, timeout.Token);
        await host.StopAsync(timeout.Token);

        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    private static T Resolve<T>(IServiceProvider services, bool keyed) where T : notnull =>
        keyed ? services.GetRequiredKeyedService<T>("messaging") : services.GetRequiredService<T>();
}

/// <summary>Share consumer workers registered through the Dekaf Aspire integration.</summary>
[Category("ShareConsumer")]
[SupportsKafka(420)]
[NotInParallel("ShareConsumerKafka42")]
public sealed class AspireShareConsumerIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShareConsumerService_ProcessesRecordsAndReportsHealthy(bool keyed)
    {
        // Create the topic first: the worker must not subscribe to a topic that does not exist yet.
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var state = new WorkerState(expected: 5);
        var builder = AspireHost.Create(
            ("ConnectionStrings:messaging", KafkaContainer.BootstrapServers),
            ("Aspire:Dekaf:ShareConsumer:Config:GroupId", $"aspire-share-{Guid.NewGuid():N}"),
            ("Aspire:Dekaf:ShareConsumer:Config:AutoOffsetReset", "Earliest"));
        builder.Services.AddSingleton(state);
        builder.Services.AddSingleton(new WorkerTopic(topic));
        if (keyed) builder.AddKeyedDekafShareConsumerService<Worker, string, string>("messaging");
        else builder.AddDekafShareConsumerService<Worker, string, string>("messaging");
        builder.AddDekafProducer<string, string>("messaging");

        using var host = builder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await host.StartAsync(timeout.Token);
        var producer = host.Services.GetRequiredService<IKafkaProducer<string, string>>();
        for (var index = 0; index < 5; index++)
            await producer.ProduceAsync(topic, $"key-{index}", $"value-{index}", timeout.Token);

        await state.Completed.Task.WaitAsync(timeout.Token);
        var report = await AspireHost.WaitForHealthyAsync(host.Services, timeout.Token);
        await host.StopAsync(timeout.Token);

        await Assert.That(state.Values.Count).IsEqualTo(5);
        await Assert.That(report.Entries.Keys).Contains(keyed
            ? $"Dekaf_shareconsumer_service<{typeof(Worker)},System.String,System.String>_messaging"
            : $"Dekaf_shareconsumer_service<{typeof(Worker)},System.String,System.String>");
    }

    private sealed record WorkerTopic(string Name);

    private sealed class WorkerState(int expected)
    {
        public ConcurrentDictionary<string, byte> Values { get; } = new();
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Record(string value)
        {
            if (Values.TryAdd(value, 0) && Values.Count >= expected)
                Completed.TrySetResult();
        }
    }

    private sealed class Worker(IKafkaShareConsumer<string, string> consumer, ILogger<Worker> logger, WorkerState state, WorkerTopic topic)
        : KafkaShareConsumerService<string, string>(consumer, logger)
    {
        protected override IEnumerable<string> Topics => [topic.Name];

        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            state.Record(result.Value);
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>Schema Registry clients registered through the Dekaf Aspire integration.</summary>
[Category("Serialization")]
[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class AspireSchemaRegistryIntegrationTests(KafkaWithSchemaRegistryContainer testInfra)
{
    private const string OrderSchema = """
        {"type":"object","properties":{"Id":{"type":"string"},"Total":{"type":"number"}},"required":["Id","Total"]}
        """;

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task JsonSchemaRegistry_RoundTripsAndReportsHealthy(bool keyed)
    {
        var topic = await testInfra.CreateTestTopicAsync();
        var builder = AspireHost.Create(
            ("ConnectionStrings:messaging", testInfra.BootstrapServers),
            ("ConnectionStrings:registry", testInfra.RegistryUrl),
            ("Aspire:Dekaf:Consumer:Config:GroupId", $"aspire-sr-{Guid.NewGuid():N}"),
            ("Aspire:Dekaf:Consumer:Config:AutoOffsetReset", "Earliest"));
        ISchemaRegistryClient Registry(IServiceProvider services) => keyed
            ? services.GetRequiredKeyedService<ISchemaRegistryClient>("registry")
            : services.GetRequiredService<ISchemaRegistryClient>();
        if (keyed) builder.AddKeyedDekafSchemaRegistryClient("registry");
        else builder.AddDekafSchemaRegistryClient("registry");
        builder.AddDekafProducer<string, Order>("messaging", configureBuilder: (services, producer) =>
            producer.UseJsonSchemaRegistry(Registry(services), OrderSchema));
        builder.AddDekafConsumer<string, Order>("messaging", configureBuilder: (services, consumer) => consumer
            .UseJsonSchemaRegistry(Registry(services))
            .SubscribeTo(topic));

        using var host = builder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await host.StartAsync(timeout.Token);
        await host.Services.GetRequiredService<IKafkaProducer<string, Order>>()
            .ProduceAsync(topic, "order-1", new Order("order-1", 12.5m), timeout.Token);
        Order? received = null;
        await foreach (var record in host.Services.GetRequiredService<IKafkaConsumer<string, Order>>().ConsumeAsync(timeout.Token))
        {
            received = record.Value;
            break;
        }

        var report = await AspireHost.WaitForHealthyAsync(host.Services, timeout.Token);
        var subjects = await Registry(host.Services).GetAllSubjectsAsync(timeout.Token);
        await host.StopAsync(timeout.Token);

        await Assert.That(received).IsEqualTo(new Order("order-1", 12.5m));
        await Assert.That(subjects).Contains($"{topic}-value");
        await Assert.That(report.Entries.Keys).Contains(keyed ? "Dekaf_schema_registry_registry" : "Dekaf_schema_registry");
    }

    public sealed record Order(string Id, decimal Total);
}

internal static class AspireHost
{
    internal static HostApplicationBuilder Create(params (string Key, string? Value)[] configuration)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(configuration.Select(entry => new KeyValuePair<string, string?>(entry.Key, entry.Value)));
        builder.Logging.ClearProviders();
        return builder;
    }

    /// <summary>Polls the host's health checks until every check is healthy.</summary>
    internal static async Task<HealthReport> WaitForHealthyAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var health = services.GetRequiredService<HealthCheckService>();
        while (true)
        {
            var report = await health.CheckHealthAsync(cancellationToken);
            if (report.Status == HealthStatus.Healthy)
                return report;

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException("Health checks did not become healthy: " + string.Join("; ",
                    report.Entries.Select(entry => $"{entry.Key}={entry.Value.Status} {entry.Value.Description} {entry.Value.Exception?.Message}")));
            }
        }
    }
}
