using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Producer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

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

    #region ProducerServiceBuilder Chaining Tests

    [Test]
    public async Task ProducerServiceBuilder_WithBootstrapServers_ReturnsSelf()
    {
        var builder = new ProducerServiceBuilder<string, string>();
        var result = builder.WithBootstrapServers("localhost:9092");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ProducerServiceBuilder_WithClientId_ReturnsSelf()
    {
        var builder = new ProducerServiceBuilder<string, string>();
        var result = builder.WithClientId("client");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ProducerServiceBuilder_WithAcks_ReturnsSelf()
    {
        var builder = new ProducerServiceBuilder<string, string>();
        var result = builder.WithAcks(Acks.All);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ProducerServiceBuilder_EnableIdempotence_ReturnsSelf()
    {
        var builder = new ProducerServiceBuilder<string, string>();
        var result = builder.EnableIdempotence();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ProducerServiceBuilder_UseZstdCompression_ReturnsSelf()
    {
        var builder = new ProducerServiceBuilder<string, string>();
        var result = builder.UseZstdCompression();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ProducerServiceBuilder_UseGzipCompression_ReturnsSelf()
    {
        var builder = new ProducerServiceBuilder<string, string>();
        var result = builder.UseGzipCompression();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ProducerServiceBuilder_UseLz4Compression_ReturnsSelf()
    {
        var builder = new ProducerServiceBuilder<string, string>();
        var result = builder.UseLz4Compression();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ProducerServiceBuilder_UseTls_ReturnsSelf()
    {
        var builder = new ProducerServiceBuilder<string, string>();
        var result = builder.UseTls();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    #endregion

    #region ConsumerServiceBuilder Chaining Tests

    [Test]
    public async Task ConsumerServiceBuilder_WithBootstrapServers_ReturnsSelf()
    {
        var builder = new ConsumerServiceBuilder<string, string>();
        var result = builder.WithBootstrapServers("localhost:9092");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ConsumerServiceBuilder_WithClientId_ReturnsSelf()
    {
        var builder = new ConsumerServiceBuilder<string, string>();
        var result = builder.WithClientId("client");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ConsumerServiceBuilder_WithGroupId_ReturnsSelf()
    {
        var builder = new ConsumerServiceBuilder<string, string>();
        var result = builder.WithGroupId("group");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ConsumerServiceBuilder_WithGroupInstanceId_ReturnsSelf()
    {
        var builder = new ConsumerServiceBuilder<string, string>();
        var result = builder.WithGroupInstanceId("instance");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ConsumerServiceBuilder_WithOffsetCommitMode_ReturnsSelf()
    {
        var builder = new ConsumerServiceBuilder<string, string>();
        var result = builder.WithOffsetCommitMode(OffsetCommitMode.Manual);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ConsumerServiceBuilder_WithAutoOffsetReset_ReturnsSelf()
    {
        var builder = new ConsumerServiceBuilder<string, string>();
        var result = builder.WithAutoOffsetReset(AutoOffsetReset.Earliest);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ConsumerServiceBuilder_UseTls_ReturnsSelf()
    {
        var builder = new ConsumerServiceBuilder<string, string>();
        var result = builder.UseTls();
        await Assert.That(result).IsSameReferenceAs(builder);
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

    #region ProducerServiceBuilder.AddInterceptor Tests

    [Test]
    public async Task ProducerServiceBuilder_AddInterceptor_ReturnsSelf()
    {
        var builder = new ProducerServiceBuilder<string, string>();
        var interceptor = Substitute.For<IProducerInterceptor<string, string>>();
        var result = builder.AddInterceptor(interceptor);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ProducerServiceBuilder_AddInterceptor_NullInterceptor_ThrowsArgumentNullException()
    {
        var builder = new ProducerServiceBuilder<string, string>();

        await Assert.That(() => builder.AddInterceptor(null!)).Throws<ArgumentNullException>();
    }

    #endregion

    #region ConsumerServiceBuilder.AddInterceptor Tests

    [Test]
    public async Task ConsumerServiceBuilder_AddInterceptor_ReturnsSelf()
    {
        var builder = new ConsumerServiceBuilder<string, string>();
        var interceptor = Substitute.For<IConsumerInterceptor<string, string>>();
        var result = builder.AddInterceptor(interceptor);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ConsumerServiceBuilder_AddInterceptor_NullInterceptor_ThrowsArgumentNullException()
    {
        var builder = new ConsumerServiceBuilder<string, string>();

        await Assert.That(() => builder.AddInterceptor(null!)).Throws<ArgumentNullException>();
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
