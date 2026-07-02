using Dekaf;
using Dekaf.Admin;
using Dekaf.Compression.Lz4;
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Producer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dekaf.Tests.Unit.Extensions;

public class DependencyInjectionTests
{
    #region AddDekaf Tests

    [Test]
    public async Task AddDekaf_RegistersServices()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddProducer<string, string>(p => p.WithBootstrapServers("localhost:9092"));
        });

        // Verify producer registration exists
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKafkaProducer<string, string>));
        await Assert.That(descriptor).IsNotNull();
    }

    [Test]
    public async Task AddDekaf_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddDekaf(_ => { });

        await Assert.That(result).IsSameReferenceAs(services);
    }

    #endregion

    #region AddProducer Tests

    [Test]
    public async Task AddProducer_RegistersAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddProducer<string, string>(p => p.WithBootstrapServers("localhost:9092"));
        });

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKafkaProducer<string, string>));
        await Assert.That(descriptor).IsNotNull();
        await Assert.That(descriptor!.Lifetime).IsEqualTo(ServiceLifetime.Singleton);
    }

    [Test]
    public async Task AddProducer_MultipleTimes_BothRegistered()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddProducer<string, string>(p => p.WithBootstrapServers("localhost:9092"));
            builder.AddProducer<string, int>(p => p
                .WithBootstrapServers("localhost:9092")
                .WithValueSerializer(Dekaf.Serialization.Serializers.Int32));
        });

        var stringDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKafkaProducer<string, string>));
        var intDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKafkaProducer<string, int>));
        await Assert.That(stringDescriptor).IsNotNull();
        await Assert.That(intDescriptor).IsNotNull();
    }

    #endregion

    #region AddConsumer Tests

    [Test]
    public async Task AddConsumer_RegistersAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddConsumer<string, string>(c => c
                .WithBootstrapServers("localhost:9092")
                .WithGroupId("test-group"));
        });

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKafkaConsumer<string, string>));
        await Assert.That(descriptor).IsNotNull();
        await Assert.That(descriptor!.Lifetime).IsEqualTo(ServiceLifetime.Singleton);
    }

    #endregion

    #region AddAdminClient Tests

    [Test]
    public async Task AddAdminClient_RegistersAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddAdminClient(a => a.WithBootstrapServers("localhost:9092"));
        });

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAdminClient));
        await Assert.That(descriptor).IsNotNull();
        await Assert.That(descriptor!.Lifetime).IsEqualTo(ServiceLifetime.Singleton);
    }

    #endregion

    #region Full Builder Surface Tests

    [Test]
    public async Task AddProducer_CallbackExposesCoreProducerBuilder()
    {
        var services = new ServiceCollection();
        ProducerBuilder<string, string>? capturedBuilder = null;

        services.AddDekaf(builder =>
        {
            builder.AddProducer<string, string>(p =>
            {
                capturedBuilder = p;
                p.WithBootstrapServers("localhost:9092")
                    .WithLinger(TimeSpan.FromMilliseconds(5))
                    .WithBatchSize(64 * 1024)
                    .WithSaslScramSha512("user", "password")
                    .UseLz4Compression();
            });
        });

        await Assert.That(capturedBuilder).IsNotNull();
        await Assert.That(capturedBuilder).IsTypeOf<ProducerBuilder<string, string>>();
    }

    [Test]
    public async Task AddConsumer_CallbackExposesCoreConsumerBuilder()
    {
        var services = new ServiceCollection();
        ConsumerBuilder<string, string>? capturedBuilder = null;

        services.AddDekaf(builder =>
        {
            builder.AddConsumer<string, string>(c =>
            {
                capturedBuilder = c;
                c.WithBootstrapServers("localhost:9092")
                    .WithGroupId("test-group")
                    .WithGroupRemoteAssignor("uniform")
                    .WithFetchMinBytes(1024)
                    .WithAdaptiveConnections(maxConnections: 6)
                    .SubscribeTo("orders");
            });
        });

        await Assert.That(capturedBuilder).IsNotNull();
        await Assert.That(capturedBuilder).IsTypeOf<ConsumerBuilder<string, string>>();
    }

    [Test]
    public async Task AddConsumer_WithDeadLetterQueue_RegistersOptions()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddConsumer<string, string>(
                c => c
                    .WithBootstrapServers("localhost:9092")
                    .WithGroupId("test-group"),
                dlq => dlq
                    .WithTopicSuffix(".dead")
                    .WithMaxFailures(3));
        });

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<DeadLetterOptions>();

        await Assert.That(options.TopicSuffix).IsEqualTo(".dead");
        await Assert.That(options.MaxFailures).IsEqualTo(3);
        await Assert.That(options.BootstrapServers).IsEqualTo("localhost:9092");
    }

    #endregion

    #region AdminClientServiceBuilder Chaining Tests

    [Test]
    public async Task AdminClientServiceBuilder_WithBootstrapServers_ReturnsSelf()
    {
        var builder = new AdminClientServiceBuilder();
        var result = builder.WithBootstrapServers("localhost:9092");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task AdminClientServiceBuilder_WithClientId_ReturnsSelf()
    {
        var builder = new AdminClientServiceBuilder();
        var result = builder.WithClientId("admin");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task AdminClientServiceBuilder_UseTls_ReturnsSelf()
    {
        var builder = new AdminClientServiceBuilder();
        var result = builder.UseTls();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    #endregion

    #region IInitializableKafkaClient Registration Tests

    [Test]
    public async Task AddProducer_RegistersAsInitializableKafkaClient()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddProducer<string, string>(p => p.WithBootstrapServers("localhost:9092"));
        });

        var descriptors = services.Where(d => d.ServiceType == typeof(IInitializableKafkaClient)).ToList();
        await Assert.That(descriptors.Count).IsEqualTo(1);
        await Assert.That(descriptors[0].Lifetime).IsEqualTo(ServiceLifetime.Singleton);
    }

    [Test]
    public async Task AddConsumer_RegistersAsInitializableKafkaClient()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddConsumer<string, string>(c => c
                .WithBootstrapServers("localhost:9092")
                .WithGroupId("test-group"));
        });

        var descriptors = services.Where(d => d.ServiceType == typeof(IInitializableKafkaClient)).ToList();
        await Assert.That(descriptors.Count).IsEqualTo(1);
        await Assert.That(descriptors[0].Lifetime).IsEqualTo(ServiceLifetime.Singleton);
    }

    [Test]
    public async Task AddProducerAndConsumer_RegistersBothAsInitializableKafkaClient()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddProducer<string, string>(p => p.WithBootstrapServers("localhost:9092"));
            builder.AddConsumer<string, string>(c => c
                .WithBootstrapServers("localhost:9092")
                .WithGroupId("test-group"));
        });

        var descriptors = services.Where(d => d.ServiceType == typeof(IInitializableKafkaClient)).ToList();
        await Assert.That(descriptors.Count).IsEqualTo(2);
    }

    #endregion

    #region IHostedService Registration Tests

    [Test]
    public async Task AddDekaf_RegistersHostedService()
    {
        var services = new ServiceCollection();

        services.AddDekaf(_ => { });

        var descriptors = services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();
        await Assert.That(descriptors.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AddDekaf_CalledMultipleTimes_RegistersHostedServiceOnce()
    {
        var services = new ServiceCollection();

        services.AddDekaf(_ => { });
        services.AddDekaf(_ => { });

        var descriptors = services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();
        await Assert.That(descriptors.Count).IsEqualTo(1);
    }

    #endregion

    #region DekafBuilder Chaining Tests

    [Test]
    public async Task DekafBuilder_AddProducer_ReturnsSelf()
    {
        var services = new ServiceCollection();
        DekafBuilder? capturedBuilder = null;
        DekafBuilder? capturedResult = null;

        services.AddDekaf(builder =>
        {
            capturedBuilder = builder;
            capturedResult = builder.AddProducer<string, string>(p => p.WithBootstrapServers("localhost:9092"));
        });

        await Assert.That(capturedResult).IsSameReferenceAs(capturedBuilder);
    }

    [Test]
    public async Task DekafBuilder_AddConsumer_ReturnsSelf()
    {
        var services = new ServiceCollection();
        DekafBuilder? capturedBuilder = null;
        DekafBuilder? capturedResult = null;

        services.AddDekaf(builder =>
        {
            capturedBuilder = builder;
            capturedResult = builder.AddConsumer<string, string>(c => c
                .WithBootstrapServers("localhost:9092")
                .WithGroupId("test"));
        });

        await Assert.That(capturedResult).IsSameReferenceAs(capturedBuilder);
    }

    [Test]
    public async Task DekafBuilder_AddAdminClient_ReturnsSelf()
    {
        var services = new ServiceCollection();
        DekafBuilder? capturedBuilder = null;
        DekafBuilder? capturedResult = null;

        services.AddDekaf(builder =>
        {
            capturedBuilder = builder;
            capturedResult = builder.AddAdminClient(a => a.WithBootstrapServers("localhost:9092"));
        });

        await Assert.That(capturedResult).IsSameReferenceAs(capturedBuilder);
    }

    #endregion

    #region Global Interceptor Registration Tests

    [Test]
    public async Task DekafBuilder_AddGlobalProducerInterceptor_ReturnsSelf()
    {
        var services = new ServiceCollection();
        DekafBuilder? capturedBuilder = null;
        DekafBuilder? capturedResult = null;

        services.AddDekaf(builder =>
        {
            capturedBuilder = builder;
            capturedResult = builder.AddGlobalProducerInterceptor<TestProducerInterceptor>();
        });

        await Assert.That(capturedResult).IsSameReferenceAs(capturedBuilder);
    }

    [Test]
    public async Task DekafBuilder_AddGlobalConsumerInterceptor_ReturnsSelf()
    {
        var services = new ServiceCollection();
        DekafBuilder? capturedBuilder = null;
        DekafBuilder? capturedResult = null;

        services.AddDekaf(builder =>
        {
            capturedBuilder = builder;
            capturedResult = builder.AddGlobalConsumerInterceptor<TestConsumerInterceptor>();
        });

        await Assert.That(capturedResult).IsSameReferenceAs(capturedBuilder);
    }

    [Test]
    public async Task DekafBuilder_AddGlobalProducerInterceptor_NullType_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        await Assert.That(() =>
        {
            services.AddDekaf(builder =>
            {
                builder.AddGlobalProducerInterceptor(null!);
            });
        }).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task DekafBuilder_AddGlobalConsumerInterceptor_NullType_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        await Assert.That(() =>
        {
            services.AddDekaf(builder =>
            {
                builder.AddGlobalConsumerInterceptor(null!);
            });
        }).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task DekafBuilder_GlobalAndPerInstanceInterceptors_ProducerRegistered()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddGlobalProducerInterceptor<TestProducerInterceptor>();
            builder.AddProducer<string, string>(p =>
            {
                p.WithBootstrapServers("localhost:9092");
                p.AddInterceptor(new TestProducerInterceptor());
            });
        });

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKafkaProducer<string, string>));
        await Assert.That(descriptor).IsNotNull();
    }

    [Test]
    public async Task DekafBuilder_GlobalAndPerInstanceInterceptors_ConsumerRegistered()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddGlobalConsumerInterceptor<TestConsumerInterceptor>();
            builder.AddConsumer<string, string>(c =>
            {
                c.WithBootstrapServers("localhost:9092")
                    .WithGroupId("test-group");
                c.AddInterceptor(new TestConsumerInterceptor());
            });
        });

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKafkaConsumer<string, string>));
        await Assert.That(descriptor).IsNotNull();
    }

    #endregion

    #region Test Interceptor Implementations

    private sealed class TestProducerInterceptor : IProducerInterceptor<string, string>
    {
        public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message) => message;
        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception) { }
    }

    private sealed class TestConsumerInterceptor : IConsumerInterceptor<string, string>
    {
        public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result) => result;
        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
    }

    #endregion
}
