using Dekaf.SchemaRegistry;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TUnit.Aspire;

public class AppFixture : AspireFixture<Projects.AppHost>
{
    protected override TimeSpan ResourceTimeout => TimeSpan.FromSeconds(180);

    protected override string[] Args =>
        [$"REPRO_VARIANT={Environment.GetEnvironmentVariable("REPRO_VARIANT") ?? "issue"}"];

    protected override void ConfigureBuilder(IDistributedApplicationTestingBuilder builder)
    {
        builder.Services.AddLogging(logging => logging
            .AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss.fff ")
            .SetMinimumLevel(LogLevel.Debug)
            .AddFilter("Aspire.Hosting", LogLevel.Debug)
            .AddFilter("Microsoft", LogLevel.Information));
    }
}

public class TUnitAspireFixtureTests
{
    [ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
    public required AppFixture Fixture { get; init; }

    [Test]
    public async Task FixtureStarts()
    {
        Console.WriteLine("[repro] fixture started");
        await Task.CompletedTask;
    }
}

public class PlainTestingBuilderTests
{
    [Test]
    [Timeout(240_000)]
    public async Task StartAsyncCompletes(CancellationToken cancellationToken)
    {
        var variant = Environment.GetEnvironmentVariable("REPRO_VARIANT") ?? "issue";
        await using var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.AppHost>([$"REPRO_VARIANT={variant}"], cancellationToken);
        builder.Services.AddLogging(logging => logging
            .AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss.fff ")
            .SetMinimumLevel(LogLevel.Debug)
            .AddFilter("Microsoft", LogLevel.Information));

        await using var app = await builder.BuildAsync(cancellationToken);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(180));
        await app.StartAsync(cts.Token);
        Console.WriteLine("[repro] plain StartAsync completed");
    }
}

public sealed record Order(string Id, decimal Amount);

public class KafkaAndSchemaRegistryTests
{
    [ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
    public required AppFixture Fixture { get; init; }

    [Test]
    [Timeout(120_000)]
    public async Task ProduceAndConsume_ThroughSchemaRegistry(CancellationToken cancellationToken)
    {
        var bootstrap = await Fixture.GetConnectionStringAsync("kafka", cancellationToken);
        var registryUrl = await Fixture.GetConnectionStringAsync("schema-registry", cancellationToken);
        Console.WriteLine($"[repro] kafka={bootstrap} registry={registryUrl}");

        using var registry = new Dekaf.SchemaRegistry.SchemaRegistryClient(
            new Dekaf.SchemaRegistry.SchemaRegistryConfig { Url = registryUrl! });

        const string orderSchema = """
            { "type": "object", "properties": { "Id": { "type": "string" }, "Amount": { "type": "number" } }, "required": ["Id", "Amount"] }
            """;
        var topic = $"orders-{Guid.NewGuid():N}";

        await using var producer = await Dekaf.Kafka.CreateProducer<string, Order>()
            .WithBootstrapServers(bootstrap!)
            .UseJsonSchemaRegistry(registry, orderSchema)
            .BuildAsync(cancellationToken);

        for (var i = 0; i < 10; i++)
            await producer.ProduceAsync(topic, $"key-{i}", new Order($"order-{i}", i * 1.5m), cancellationToken);

        var subjects = await registry.GetAllSubjectsAsync(cancellationToken);
        Console.WriteLine($"[repro] subjects: {string.Join(", ", subjects)}");
        await Assert.That(subjects).Contains($"{topic}-value");

        await using var consumer = await Dekaf.Kafka.CreateConsumer<string, Order>()
            .WithBootstrapServers(bootstrap!)
            .WithGroupId($"repro-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Dekaf.Consumer.AutoOffsetReset.Earliest)
            .UseJsonSchemaRegistry(registry)
            .SubscribeTo(topic)
            .BuildAsync(cancellationToken);

        var received = new List<Order>();
        await foreach (var result in consumer.ConsumeAsync(cancellationToken))
        {
            received.Add(result.Value);
            if (received.Count == 10)
                break;
        }

        Console.WriteLine($"[repro] consumed {received.Count}: {received[0]} .. {received[^1]}");
        await Assert.That(received[3]).IsEqualTo(new Order("order-3", 4.5m));
    }
}
