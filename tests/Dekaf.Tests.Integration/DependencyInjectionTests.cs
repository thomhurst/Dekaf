using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Producer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for Dekaf DI extension methods.
/// </summary>
[Category("Messaging")]
public sealed class DependencyInjectionTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ProducerResolvesFromDI_ProduceSucceeds()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddDekaf(dekaf =>
                {
                    dekaf.AddProducer<string, string>(p =>
                        p.WithBootstrapServers(KafkaContainer.BootstrapServers));
                });
            })
            .Build();

        await host.StartAsync();

        var producer = host.Services.GetRequiredService<IKafkaProducer<string, string>>();

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "di-key",
            Value = "di-value"
        });

        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task ConsumerResolvesFromDI_ConsumeSucceeds()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"di-consumer-{Guid.NewGuid():N}";

        // Produce a message first
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "di-key",
            Value = "di-value"
        });

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddDekaf(dekaf =>
                {
                    dekaf.AddConsumer<string, string>(c =>
                        c.WithBootstrapServers(KafkaContainer.BootstrapServers)
                         .WithGroupId(groupId)
                         .WithAutoOffsetReset(AutoOffsetReset.Earliest));
                });
            })
            .Build();

        await host.StartAsync();

        var consumer = host.Services.GetRequiredService<IKafkaConsumer<string, string>>();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo("di-value");

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task AdminClientResolvesFromDI_DescribeClusterSucceeds()
    {
        var services = new ServiceCollection();
        services.AddDekaf(dekaf =>
        {
            dekaf.AddAdminClient(a =>
                a.WithBootstrapServers(KafkaContainer.BootstrapServers));
        });

        await using var sp = services.BuildServiceProvider();
        var adminClient = sp.GetRequiredService<IAdminClient>();

        var cluster = await adminClient.DescribeClusterAsync();

        await Assert.That(cluster).IsNotNull();
        await Assert.That(cluster.Nodes.Count).IsGreaterThan(0);

        await adminClient.DisposeAsync().ConfigureAwait(false);
    }

    [Test]
    public async Task RoundTripViaDI_ProduceAndConsumeViaDIResolvedClients()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"di-roundtrip-{Guid.NewGuid():N}";

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddDekaf(dekaf =>
                {
                    dekaf.AddProducer<string, string>(p =>
                        p.WithBootstrapServers(KafkaContainer.BootstrapServers));
                    dekaf.AddConsumer<string, string>(c =>
                        c.WithBootstrapServers(KafkaContainer.BootstrapServers)
                         .WithGroupId(groupId)
                         .WithAutoOffsetReset(AutoOffsetReset.Earliest));
                });
            })
            .Build();

        await host.StartAsync();

        var producerFromDi = host.Services.GetRequiredService<IKafkaProducer<string, string>>();
        var consumer = host.Services.GetRequiredService<IKafkaConsumer<string, string>>();

        // Produce
        await producerFromDi.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "roundtrip-key",
            Value = "roundtrip-value"
        });

        // Consume
        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("roundtrip-key");
        await Assert.That(result.Value.Value).IsEqualTo("roundtrip-value");

        await host.StopAsync();
        host.Dispose();
    }
}
