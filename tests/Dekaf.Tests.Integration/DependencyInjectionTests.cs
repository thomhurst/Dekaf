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
    [Arguments(false)]
    [Arguments(true)]
    public async Task ReusedServiceCollection_DeliversWithOnlyCurrentProviderInterceptors(bool keyed)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var services = new ServiceCollection();
        services.AddSingleton<ProviderInterceptor>();
        services.AddDekaf(builder =>
        {
            builder.AddGlobalProducerInterceptor<string, string, ProviderInterceptor>();
            builder.AddGlobalConsumerInterceptor<string, string, ProviderInterceptor>();
            void ConfigureProducer(ProducerBuilder<string, string> producer) =>
                producer.WithBootstrapServers(KafkaContainer.BootstrapServers);
            void ConfigureConsumer(ConsumerBuilder<string, string> consumer) =>
                consumer.WithBootstrapServers(KafkaContainer.BootstrapServers)
                    .WithGroupId($"di-isolation-{Guid.NewGuid():N}")
                    .WithAutoOffsetReset(AutoOffsetReset.Earliest);
            if (keyed)
            {
                builder.AddProducer<string, string>("client", ConfigureProducer);
                builder.AddConsumer<string, string>("client", ConfigureConsumer);
            }
            else
            {
                builder.AddProducer<string, string>(ConfigureProducer);
                builder.AddConsumer<string, string>(ConfigureConsumer);
            }
        });

        ProviderInterceptor disposedInterceptor;
        await using (var first = services.BuildServiceProvider())
        {
            ResolveProducer(first);
            ResolveConsumer(first);
            disposedInterceptor = first.GetRequiredService<ProviderInterceptor>();
        }
        await Assert.That(disposedInterceptor.Disposed).IsTrue();

        await using var second = services.BuildServiceProvider();
        var producer = ResolveProducer(second);
        var consumer = ResolveConsumer(second);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await producer.InitializeAsync(timeout.Token);
        await consumer.InitializeAsync(timeout.Token);
        await producer.ProduceAsync(topic, "key", "value", timeout.Token);
        consumer.Subscribe(topic);
        var message = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), timeout.Token);
        var interceptor = second.GetRequiredService<ProviderInterceptor>();

        await Assert.That(message).IsNotNull();
        await Assert.That(message!.Value.Value).IsEqualTo("value");
        await Assert.That(interceptor.SendCount).IsEqualTo(1);
        await Assert.That(interceptor.ConsumeCount).IsEqualTo(1);
        await Assert.That(disposedInterceptor.SendCount).IsEqualTo(0);
        await Assert.That(disposedInterceptor.ConsumeCount).IsEqualTo(0);

        IKafkaProducer<string, string> ResolveProducer(ServiceProvider provider) => keyed
            ? provider.GetRequiredKeyedService<IKafkaProducer<string, string>>("client")
            : provider.GetRequiredService<IKafkaProducer<string, string>>();
        IKafkaConsumer<string, string> ResolveConsumer(ServiceProvider provider) => keyed
            ? provider.GetRequiredKeyedService<IKafkaConsumer<string, string>>("client")
            : provider.GetRequiredService<IKafkaConsumer<string, string>>();
    }

    private sealed class ProviderInterceptor
        : IProducerInterceptor<string, string>, IConsumerInterceptor<string, string>, IDisposable
    {
        private int _sendCount;
        private int _consumeCount;
        public int SendCount => Volatile.Read(ref _sendCount);
        public int ConsumeCount => Volatile.Read(ref _consumeCount);
        public bool Disposed { get; private set; }
        public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            Interlocked.Increment(ref _sendCount);
            return message;
        }
        public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            Interlocked.Increment(ref _consumeCount);
            return result;
        }
        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception) { }
        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
        public void Dispose() => Disposed = true;
    }

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

        // Warm up to ensure broker has initialized partition state
        await producer.FireAsync(new ProducerMessage<string, string>
        { Topic = topic, Key = "warmup", Value = "warmup" });

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "di-key",
            Value = "di-value"
        }, CancellationToken.None);

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
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        // Warm up to ensure broker has initialized partition state
        await producer.FireAsync(new ProducerMessage<string, string>
        { Topic = topic, Key = "warmup", Value = "warmup" });

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "di-key",
            Value = "di-value"
        }, CancellationToken.None);

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
        ConsumeResult<string, string>? result = null;
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            if (msg.Key != "warmup")
            {
                result = msg;
                break;
            }
        }

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

        // Warm up to ensure broker has initialized partition state
        await producerFromDi.FireAsync(new ProducerMessage<string, string>
        { Topic = topic, Key = "warmup", Value = "warmup" });

        // Produce
        await producerFromDi.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "roundtrip-key",
            Value = "roundtrip-value"
        }, CancellationToken.None);

        // Consume
        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        ConsumeResult<string, string>? result = null;
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            if (msg.Key != "warmup")
            {
                result = msg;
                break;
            }
        }

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("roundtrip-key");
        await Assert.That(result!.Value.Value).IsEqualTo("roundtrip-value");

        await host.StopAsync();
        host.Dispose();
    }
}
