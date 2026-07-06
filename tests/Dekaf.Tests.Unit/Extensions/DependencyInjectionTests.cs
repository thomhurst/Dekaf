using Dekaf;
using Dekaf.Admin;
using Dekaf.Compression.Lz4;
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Producer;
using Dekaf.Security.Sasl;
using Microsoft.Extensions.Configuration;
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

    [Test]
    public async Task AddProducer_WithServiceKeys_RegistersDistinctKeyedProducers()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddProducer<string, string>("orders", p => p
                .WithBootstrapServers("localhost:9092")
                .WithClientId("orders-producer"));
            builder.AddProducer<string, string>("payments", p => p
                .WithBootstrapServers("localhost:9092")
                .WithClientId("payments-producer"));
        });

        var provider = services.BuildServiceProvider();
        var orders = provider.GetRequiredKeyedService<IKafkaProducer<string, string>>("orders");
        var payments = provider.GetRequiredKeyedService<IKafkaProducer<string, string>>("payments");

        await Assert.That(orders).IsNotSameReferenceAs(payments);
        await Assert.That(GetProducerOptions(orders).ClientId).IsEqualTo("orders-producer");
        await Assert.That(GetProducerOptions(payments).ClientId).IsEqualTo("payments-producer");
        await Assert.That(provider.GetServices<IKafkaProducer<string, string>>().Count()).IsEqualTo(0);
    }

    [Test]
    public async Task AddProducer_DefaultAndKeyed_CanCoexistForSameGenericType()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddProducer<string, string>(p => p
                .WithBootstrapServers("localhost:9092")
                .WithClientId("default-producer"));
            builder.AddProducer<string, string>("orders", p => p
                .WithBootstrapServers("localhost:9092")
                .WithClientId("orders-producer"));
        });

        var provider = services.BuildServiceProvider();
        var defaultProducer = provider.GetRequiredService<IKafkaProducer<string, string>>();
        var ordersProducer = provider.GetRequiredKeyedService<IKafkaProducer<string, string>>("orders");

        await Assert.That(defaultProducer).IsNotSameReferenceAs(ordersProducer);
        await Assert.That(GetProducerOptions(defaultProducer).ClientId).IsEqualTo("default-producer");
        await Assert.That(GetProducerOptions(ordersProducer).ClientId).IsEqualTo("orders-producer");
    }

    [Test]
    public async Task AddProducer_WithServiceKeyAndConfigurationSection_BindsOptions()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Kafka:Producers:Orders:BootstrapServers"] = "broker1:9092",
            ["Kafka:Producers:Orders:ClientId"] = "orders-producer"
        });

        services.AddDekaf(builder =>
        {
            builder.AddProducer<string, string>(
                "orders",
                configuration.GetSection("Kafka:Producers:Orders"));
        });

        var provider = services.BuildServiceProvider();
        var producer = provider.GetRequiredKeyedService<IKafkaProducer<string, string>>("orders");
        var options = GetProducerOptions(producer);

        await Assert.That(options.BootstrapServers[0]).IsEqualTo("broker1:9092");
        await Assert.That(options.ClientId).IsEqualTo("orders-producer");
    }

    [Test]
    public async Task AddProducer_WithTypedOptions_BindsOptions()
    {
        var services = new ServiceCollection();
        var options = new ProducerOptions
        {
            BootstrapServers = ["broker1:9092"],
            ClientId = "typed-producer",
            LingerMs = 7,
            EnableAdaptiveConnections = false,
            UseTls = true,
            SaslMechanism = SaslMechanism.ScramSha512,
            SaslUsername = "producer-user",
            SaslPassword = "producer-password",
            SaslScramTokenAuth = true
        };

        services.AddDekaf(builder =>
        {
            builder.AddProducer<string, string>(options, producer => producer.WithClientId("override-producer"));
        });

        var provider = services.BuildServiceProvider();
        var producer = provider.GetRequiredService<IKafkaProducer<string, string>>();
        var boundOptions = GetProducerOptions(producer);

        await Assert.That(boundOptions.BootstrapServers[0]).IsEqualTo("broker1:9092");
        await Assert.That(boundOptions.ClientId).IsEqualTo("override-producer");
        await Assert.That(boundOptions.LingerMs).IsEqualTo(7);
        await Assert.That(boundOptions.EnableAdaptiveConnections).IsFalse();
        await Assert.That(boundOptions.UseTls).IsTrue();
        await Assert.That(boundOptions.SaslMechanism).IsEqualTo(SaslMechanism.ScramSha512);
        await Assert.That(boundOptions.SaslUsername).IsEqualTo("producer-user");
        await Assert.That(boundOptions.SaslPassword).IsEqualTo("producer-password");
        await Assert.That(boundOptions.SaslScramTokenAuth).IsTrue();
    }

    [Test]
    public async Task AddProducer_WithTypedOptionsAndMismatchedInterceptor_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var options = new ProducerOptions
        {
            BootstrapServers = ["broker1:9092"],
            Interceptors = [new TestConsumerInterceptor()]
        };

        await Assert.That(() => services.AddDekaf(builder =>
            {
                builder.AddProducer<string, string>(options);
            }))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("does not implement IProducerInterceptor<String, String>");
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

    [Test]
    public async Task AddConsumer_WithServiceKeys_RegistersDistinctKeyedConsumers()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddConsumer<string, string>("orders", c => c
                .WithBootstrapServers("localhost:9092")
                .WithGroupId("orders-group"));
            builder.AddConsumer<string, string>("payments", c => c
                .WithBootstrapServers("localhost:9092")
                .WithGroupId("payments-group"));
        });

        var provider = services.BuildServiceProvider();
        var orders = provider.GetRequiredKeyedService<IKafkaConsumer<string, string>>("orders");
        var payments = provider.GetRequiredKeyedService<IKafkaConsumer<string, string>>("payments");

        await Assert.That(orders).IsNotSameReferenceAs(payments);
        await Assert.That(GetConsumerOptions(orders).GroupId).IsEqualTo("orders-group");
        await Assert.That(GetConsumerOptions(payments).GroupId).IsEqualTo("payments-group");
        await Assert.That(provider.GetServices<IKafkaConsumer<string, string>>().Count()).IsEqualTo(0);
    }

    [Test]
    public async Task AddConsumer_WithTypedOptions_BindsOptions()
    {
        var services = new ServiceCollection();
        var options = new ConsumerOptions
        {
            BootstrapServers = ["broker1:9092"],
            ClientId = "typed-consumer",
            GroupId = "typed-group",
            FetchMinBytes = 4096,
            EnableAdaptiveConnections = false,
            UseTls = true,
            SaslMechanism = SaslMechanism.ScramSha256,
            SaslUsername = "consumer-user",
            SaslPassword = "consumer-password",
            SaslScramTokenAuth = true
        };

        services.AddDekaf(builder =>
        {
            builder.AddConsumer<string, string>(options, consumer => consumer.WithGroupId("override-group"));
        });

        var provider = services.BuildServiceProvider();
        var consumer = provider.GetRequiredService<IKafkaConsumer<string, string>>();
        var boundOptions = GetConsumerOptions(consumer);

        await Assert.That(boundOptions.BootstrapServers[0]).IsEqualTo("broker1:9092");
        await Assert.That(boundOptions.ClientId).IsEqualTo("typed-consumer");
        await Assert.That(boundOptions.GroupId).IsEqualTo("override-group");
        await Assert.That(boundOptions.FetchMinBytes).IsEqualTo(4096);
        await Assert.That(boundOptions.EnableAdaptiveConnections).IsFalse();
        await Assert.That(boundOptions.UseTls).IsTrue();
        await Assert.That(boundOptions.SaslMechanism).IsEqualTo(SaslMechanism.ScramSha256);
        await Assert.That(boundOptions.SaslUsername).IsEqualTo("consumer-user");
        await Assert.That(boundOptions.SaslPassword).IsEqualTo("consumer-password");
        await Assert.That(boundOptions.SaslScramTokenAuth).IsTrue();
    }

    [Test]
    public async Task AddConsumer_WithTypedOptionsAndMismatchedInterceptor_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var options = new ConsumerOptions
        {
            BootstrapServers = ["broker1:9092"],
            GroupId = "typed-group",
            Interceptors = [new TestProducerInterceptor()]
        };

        await Assert.That(() => services.AddDekaf(builder =>
            {
                builder.AddConsumer<string, string>(options);
            }))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("does not implement IConsumerInterceptor<String, String>");
    }

    [Test]
    public async Task AddConsumer_WithServiceKeyAndConfigurationSection_BindsOptions()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Kafka:Consumers:Orders:BootstrapServers"] = "broker1:9092",
            ["Kafka:Consumers:Orders:GroupId"] = "orders-group"
        });

        services.AddDekaf(builder =>
        {
            builder.AddConsumer<string, string>(
                "orders",
                configuration.GetSection("Kafka:Consumers:Orders"));
        });

        var provider = services.BuildServiceProvider();
        var consumer = provider.GetRequiredKeyedService<IKafkaConsumer<string, string>>("orders");
        var options = GetConsumerOptions(consumer);

        await Assert.That(options.BootstrapServers[0]).IsEqualTo("broker1:9092");
        await Assert.That(options.GroupId).IsEqualTo("orders-group");
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

    [Test]
    public async Task AddAdminClient_WithTypedOptions_BindsOptions()
    {
        var services = new ServiceCollection();
        var options = new AdminClientOptions
        {
            BootstrapServers = ["broker1:9092"],
            ClientId = "typed-admin",
            RequestTimeoutMs = 12345,
            UseTls = true,
            SaslMechanism = SaslMechanism.ScramSha512,
            SaslUsername = "admin-user",
            SaslPassword = "admin-password",
            SaslScramTokenAuth = true
        };

        services.AddDekaf(builder =>
        {
            builder.AddAdminClient(options, admin => admin.WithClientId("override-admin"));
        });

        var provider = services.BuildServiceProvider();
        var admin = provider.GetRequiredService<IAdminClient>();
        var boundOptions = GetAdminOptions(admin);

        await Assert.That(boundOptions.BootstrapServers[0]).IsEqualTo("broker1:9092");
        await Assert.That(boundOptions.ClientId).IsEqualTo("override-admin");
        await Assert.That(boundOptions.RequestTimeoutMs).IsEqualTo(12345);
        await Assert.That(boundOptions.UseTls).IsTrue();
        await Assert.That(boundOptions.SaslMechanism).IsEqualTo(SaslMechanism.ScramSha512);
        await Assert.That(boundOptions.SaslUsername).IsEqualTo("admin-user");
        await Assert.That(boundOptions.SaslPassword).IsEqualTo("admin-password");
        await Assert.That(boundOptions.SaslScramTokenAuth).IsTrue();
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

    [Test]
    public async Task AddConsumer_WithRetryTopics_RegistersRetryTopicOptions()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddConsumer<string, string>(
                c => c
                    .WithBootstrapServers("localhost:9092")
                    .WithGroupId("test-group"),
                dlq => dlq
                    .WithRetryTopics(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)));
        });

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<DeadLetterOptions>();

        await Assert.That(options.RetryTopics).IsNotNull();
        await Assert.That(options.RetryTopics!.GetRetryTopic("orders", TimeSpan.FromSeconds(5)))
            .IsEqualTo("orders-retry-5s");
        await Assert.That(options.RetryTopics.GetRetryTopic("orders", TimeSpan.FromSeconds(30)))
            .IsEqualTo("orders-retry-30s");
    }

    #endregion

    #region Configuration Binding Tests

    [Test]
    public async Task AddProducer_WithConfigurationSection_BindsConfiguredOptions()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Kafka:Producers:Orders:BootstrapServers"] = "broker1:9092, broker2:9092",
            ["Kafka:Producers:Orders:ClientId"] = "orders-producer",
            ["Kafka:Producers:Orders:Acks"] = "Leader",
            ["Kafka:Producers:Orders:LingerMs"] = "5",
            ["Kafka:Producers:Orders:BatchSize"] = "65536",
            ["Kafka:Producers:Orders:BufferMemory"] = "1048576",
            ["Kafka:Producers:Orders:MaxInFlightRequestsPerConnection"] = "2",
            ["Kafka:Producers:Orders:Retries"] = "7",
            ["Kafka:Producers:Orders:RetryBackoffMs"] = "25",
            ["Kafka:Producers:Orders:RetryBackoffMaxMs"] = "250",
            ["Kafka:Producers:Orders:MaxBlockMs"] = "1000",
            ["Kafka:Producers:Orders:DeliveryTimeoutMs"] = "2000",
            ["Kafka:Producers:Orders:RequestTimeoutMs"] = "3000",
            ["Kafka:Producers:Orders:ReconnectBackoffMs"] = "75",
            ["Kafka:Producers:Orders:ReconnectBackoffMaxMs"] = "750",
            ["Kafka:Producers:Orders:EnableIdempotence"] = "false",
            ["Kafka:Producers:Orders:ConnectionsPerBroker"] = "3",
            ["Kafka:Producers:Orders:MaxConnectionsPerBroker"] = "6",
            ["Kafka:Producers:Orders:CompressionType"] = "Gzip",
            ["Kafka:Producers:Orders:CompressionLevel"] = "4",
            ["Kafka:Producers:Orders:Partitioner"] = "RoundRobin",
            ["Kafka:Producers:Orders:UseTls"] = "true",
            ["Kafka:Producers:Orders:SaslMechanism"] = "ScramSha512",
            ["Kafka:Producers:Orders:SaslUsername"] = "user",
            ["Kafka:Producers:Orders:SaslPassword"] = "password",
            ["Kafka:Producers:Orders:SaslScramTokenAuth"] = "true",
            ["Kafka:Producers:Orders:SocketSendBufferBytes"] = "1024",
            ["Kafka:Producers:Orders:SocketReceiveBufferBytes"] = "2048",
            ["Kafka:Producers:Orders:ValueTaskSourcePoolSize"] = "128",
            ["Kafka:Producers:Orders:ArenaCapacity"] = "131072",
            ["Kafka:Producers:Orders:InitialBatchRecordCapacity"] = "32",
            ["Kafka:Producers:Orders:MetadataRecoveryStrategy"] = "None",
            ["Kafka:Producers:Orders:MetadataRecoveryRebootstrapTriggerMs"] = "60000",
            ["Kafka:Producers:Orders:ClientDnsLookup"] = "ResolveCanonicalBootstrapServersOnly"
        });

        services.AddDekaf(builder =>
        {
            builder.AddProducer<string, string>(configuration.GetSection("Kafka:Producers:Orders"));
        });

        var provider = services.BuildServiceProvider();
        var producer = provider.GetRequiredService<IKafkaProducer<string, string>>();
        var options = GetProducerOptions(producer);

        await Assert.That(options.BootstrapServers.Count).IsEqualTo(2);
        await Assert.That(options.BootstrapServers[0]).IsEqualTo("broker1:9092");
        await Assert.That(options.BootstrapServers[1]).IsEqualTo("broker2:9092");
        await Assert.That(options.ClientId).IsEqualTo("orders-producer");
        await Assert.That(options.Acks).IsEqualTo(Acks.Leader);
        await Assert.That(options.LingerMs).IsEqualTo(5);
        await Assert.That(options.BatchSize).IsEqualTo(65536);
        await Assert.That(options.BufferMemory).IsEqualTo(1048576UL);
        await Assert.That(options.IsAutoTuned).IsFalse();
        await Assert.That(options.MaxInFlightRequestsPerConnection).IsEqualTo(2);
        await Assert.That(options.Retries).IsEqualTo(7);
        await Assert.That(options.RetryBackoffMs).IsEqualTo(25);
        await Assert.That(options.RetryBackoffMaxMs).IsEqualTo(250);
        await Assert.That(options.MaxBlockMs).IsEqualTo(1000);
        await Assert.That(options.DeliveryTimeoutMs).IsEqualTo(2000);
        await Assert.That(options.RequestTimeoutMs).IsEqualTo(3000);
        await Assert.That(options.ReconnectBackoffMs).IsEqualTo(75);
        await Assert.That(options.ReconnectBackoffMaxMs).IsEqualTo(750);
        await Assert.That(options.EnableIdempotence).IsFalse();
        await Assert.That(options.ConnectionsPerBroker).IsEqualTo(3);
        await Assert.That(options.MaxConnectionsPerBroker).IsEqualTo(6);
        await Assert.That(options.CompressionType).IsEqualTo(CompressionType.Gzip);
        await Assert.That(options.CompressionLevel).IsEqualTo(4);
        await Assert.That(options.Partitioner).IsEqualTo(PartitionerType.RoundRobin);
        await Assert.That(options.UseTls).IsTrue();
        await Assert.That(options.SaslMechanism).IsEqualTo(SaslMechanism.ScramSha512);
        await Assert.That(options.SaslUsername).IsEqualTo("user");
        await Assert.That(options.SaslPassword).IsEqualTo("password");
        await Assert.That(options.SaslScramTokenAuth).IsTrue();
        await Assert.That(options.SocketSendBufferBytes).IsEqualTo(1024);
        await Assert.That(options.SocketReceiveBufferBytes).IsEqualTo(2048);
        await Assert.That(options.ValueTaskSourcePoolSize).IsEqualTo(128);
        await Assert.That(options.ArenaCapacity).IsEqualTo(131072);
        await Assert.That(options.InitialBatchRecordCapacity).IsEqualTo(32);
        await Assert.That(options.MetadataRecoveryStrategy).IsEqualTo(MetadataRecoveryStrategy.None);
        await Assert.That(options.MetadataRecoveryRebootstrapTriggerMs).IsEqualTo(60000);
        await Assert.That(options.ClientDnsLookup).IsEqualTo(ClientDnsLookup.ResolveCanonicalBootstrapServersOnly);
    }

    [Test]
    public async Task AddProducer_WithAwsMskIamConfig_InferSaslMechanism()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Kafka:BootstrapServers"] = "broker1:9098",
            ["Kafka:UseTls"] = "true",
            ["Kafka:AwsMskIamConfig:Region"] = "us-east-1",
            ["Kafka:AwsMskIamConfig:ProfileName"] = "msk-prod"
        });

        services.AddDekaf(builder =>
        {
            builder.AddProducer<string, string>(configuration.GetSection("Kafka"));
        });

        var provider = services.BuildServiceProvider();
        var producer = provider.GetRequiredService<IKafkaProducer<string, string>>();
        var options = GetProducerOptions(producer);

        await Assert.That(options.SaslMechanism).IsEqualTo(SaslMechanism.AwsMskIam);
        await Assert.That(options.AwsMskIamConfig).IsNotNull();
        await Assert.That(options.AwsMskIamConfig!.Region).IsEqualTo("us-east-1");
        await Assert.That(options.AwsMskIamConfig.ProfileName).IsEqualTo("msk-prod");
    }

    [Test]
    public async Task AddProducer_WithSaslScramTokenAuthAndNonScramMechanism_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Kafka:BootstrapServers"] = "broker1:9092",
            ["Kafka:SaslMechanism"] = "Plain",
            ["Kafka:SaslUsername"] = "user",
            ["Kafka:SaslPassword"] = "password",
            ["Kafka:SaslScramTokenAuth"] = "true"
        });

        void Act() => services.AddDekaf(builder =>
        {
            builder.AddProducer<string, string>(configuration.GetSection("Kafka"));
        });

        await Assert.That(Act).Throws<InvalidOperationException>()
            .WithMessageContaining("SaslScramTokenAuth requires SaslMechanism ScramSha256 or ScramSha512");
    }

    [Test]
    public async Task AddProducer_WithConfigurationSection_FluentConfigurationOverridesBoundValues()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Kafka:BootstrapServers"] = "broker1:9092",
            ["Kafka:LingerMs"] = "50",
            ["Kafka:BatchSize"] = "65536"
        });

        services.AddDekaf(builder =>
        {
            builder.AddProducer<string, string>(
                configuration.GetSection("Kafka"),
                producer => producer
                    .WithLinger(TimeSpan.FromMilliseconds(2))
                    .WithBatchSize(1024));
        });

        var provider = services.BuildServiceProvider();
        var producer = provider.GetRequiredService<IKafkaProducer<string, string>>();
        var options = GetProducerOptions(producer);

        await Assert.That(options.LingerMs).IsEqualTo(2);
        await Assert.That(options.BatchSize).IsEqualTo(1024);
        await Assert.That(options.IsAutoTuned).IsTrue();
    }

    [Test]
    public async Task AddConsumer_WithConfigurationSection_BindsConfiguredOptions()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Kafka:Consumers:Orders:BootstrapServers:0"] = "broker1:9092",
            ["Kafka:Consumers:Orders:BootstrapServers:1"] = "broker2:9092",
            ["Kafka:Consumers:Orders:ClientId"] = "orders-consumer",
            ["Kafka:Consumers:Orders:GroupId"] = "orders",
            ["Kafka:Consumers:Orders:GroupInstanceId"] = "orders-1",
            ["Kafka:Consumers:Orders:GroupRemoteAssignor"] = "uniform",
            ["Kafka:Consumers:Orders:OffsetCommitMode"] = "Manual",
            ["Kafka:Consumers:Orders:AutoCommitIntervalMs"] = "2000",
            ["Kafka:Consumers:Orders:EnableAutoOffsetStore"] = "false",
            ["Kafka:Consumers:Orders:AutoOffsetReset"] = "Earliest",
            ["Kafka:Consumers:Orders:FetchMinBytes"] = "1024",
            ["Kafka:Consumers:Orders:FetchMaxBytes"] = "2097152",
            ["Kafka:Consumers:Orders:MaxPartitionFetchBytes"] = "1048576",
            ["Kafka:Consumers:Orders:FetchMaxWaitMs"] = "150",
            ["Kafka:Consumers:Orders:EnableFetchSessions"] = "false",
            ["Kafka:Consumers:Orders:MaxPollRecords"] = "250",
            ["Kafka:Consumers:Orders:MaxPollIntervalMs"] = "120000",
            ["Kafka:Consumers:Orders:SessionTimeoutMs"] = "30000",
            ["Kafka:Consumers:Orders:HeartbeatIntervalMs"] = "10000",
            ["Kafka:Consumers:Orders:RebalanceTimeoutMs"] = "45000",
            ["Kafka:Consumers:Orders:IsolationLevel"] = "ReadCommitted",
            ["Kafka:Consumers:Orders:RequestTimeoutMs"] = "15000",
            ["Kafka:Consumers:Orders:ReconnectBackoffMs"] = "80",
            ["Kafka:Consumers:Orders:ReconnectBackoffMaxMs"] = "800",
            ["Kafka:Consumers:Orders:CheckCrcs"] = "true",
            ["Kafka:Consumers:Orders:UseTls"] = "true",
            ["Kafka:Consumers:Orders:SaslMechanism"] = "ScramSha256",
            ["Kafka:Consumers:Orders:SaslUsername"] = "user",
            ["Kafka:Consumers:Orders:SaslPassword"] = "password",
            ["Kafka:Consumers:Orders:SaslScramTokenAuth"] = "true",
            ["Kafka:Consumers:Orders:EnablePartitionEof"] = "true",
            ["Kafka:Consumers:Orders:SocketSendBufferBytes"] = "4096",
            ["Kafka:Consumers:Orders:SocketReceiveBufferBytes"] = "8192",
            ["Kafka:Consumers:Orders:QueuedMinMessages"] = "1000",
            ["Kafka:Consumers:Orders:QueuedMaxMessagesKbytes"] = "32768",
            ["Kafka:Consumers:Orders:MetadataRecoveryStrategy"] = "None",
            ["Kafka:Consumers:Orders:MetadataRecoveryRebootstrapTriggerMs"] = "90000",
            ["Kafka:Consumers:Orders:ClientDnsLookup"] = "ResolveCanonicalBootstrapServersOnly",
            ["Kafka:Consumers:Orders:PrefetchPipelineDepth"] = "4",
            ["Kafka:Consumers:Orders:ConnectionsPerBroker"] = "2",
            ["Kafka:Consumers:Orders:EnableAdaptiveConnections"] = "false",
            ["Kafka:Consumers:Orders:EnableAdaptiveFetchSizing"] = "true",
            ["Kafka:Consumers:Orders:AdaptiveFetchSizingOptions:MaxFetchMaxBytes"] = "67108864"
        });

        services.AddDekaf(builder =>
        {
            builder.AddConsumer<string, string>(
                configuration.GetSection("Kafka:Consumers:Orders"),
                configureDeadLetterQueue: dlq => dlq.WithTopicSuffix(".dead"));
        });

        var provider = services.BuildServiceProvider();
        var consumer = provider.GetRequiredService<IKafkaConsumer<string, string>>();
        var options = GetConsumerOptions(consumer);
        var deadLetterOptions = provider.GetRequiredService<DeadLetterOptions>();

        await Assert.That(options.BootstrapServers.Count).IsEqualTo(2);
        await Assert.That(options.BootstrapServers[0]).IsEqualTo("broker1:9092");
        await Assert.That(options.BootstrapServers[1]).IsEqualTo("broker2:9092");
        await Assert.That(options.ClientId).IsEqualTo("orders-consumer");
        await Assert.That(options.GroupId).IsEqualTo("orders");
        await Assert.That(options.GroupInstanceId).IsEqualTo("orders-1");
        await Assert.That(options.GroupRemoteAssignor).IsEqualTo("uniform");
        await Assert.That(options.OffsetCommitMode).IsEqualTo(OffsetCommitMode.Manual);
        await Assert.That(options.AutoCommitIntervalMs).IsEqualTo(2000);
        await Assert.That(options.EnableAutoOffsetStore).IsFalse();
        await Assert.That(options.AutoOffsetReset).IsEqualTo(AutoOffsetReset.Earliest);
        await Assert.That(options.FetchMinBytes).IsEqualTo(1024);
        await Assert.That(options.FetchMaxBytes).IsEqualTo(2097152);
        await Assert.That(options.MaxPartitionFetchBytes).IsEqualTo(1048576);
        await Assert.That(options.FetchMaxWaitMs).IsEqualTo(150);
        await Assert.That(options.EnableFetchSessions).IsFalse();
        await Assert.That(options.MaxPollRecords).IsEqualTo(250);
        await Assert.That(options.MaxPollIntervalMs).IsEqualTo(120000);
        await Assert.That(options.SessionTimeoutMs).IsEqualTo(30000);
        await Assert.That(options.HeartbeatIntervalMs).IsEqualTo(10000);
        await Assert.That(options.RebalanceTimeoutMs).IsEqualTo(45000);
        await Assert.That(options.IsolationLevel).IsEqualTo(IsolationLevel.ReadCommitted);
        await Assert.That(options.RequestTimeoutMs).IsEqualTo(15000);
        await Assert.That(options.ReconnectBackoffMs).IsEqualTo(80);
        await Assert.That(options.ReconnectBackoffMaxMs).IsEqualTo(800);
        await Assert.That(options.CheckCrcs).IsTrue();
        await Assert.That(options.UseTls).IsTrue();
        await Assert.That(options.SaslMechanism).IsEqualTo(SaslMechanism.ScramSha256);
        await Assert.That(options.SaslUsername).IsEqualTo("user");
        await Assert.That(options.SaslPassword).IsEqualTo("password");
        await Assert.That(options.SaslScramTokenAuth).IsTrue();
        await Assert.That(options.EnablePartitionEof).IsTrue();
        await Assert.That(options.SocketSendBufferBytes).IsEqualTo(4096);
        await Assert.That(options.SocketReceiveBufferBytes).IsEqualTo(8192);
        await Assert.That(options.QueuedMinMessages).IsEqualTo(1000);
        await Assert.That(options.QueuedMaxMessagesKbytes).IsEqualTo(32768);
        await Assert.That(options.IsAutoTuned).IsFalse();
        await Assert.That(options.MetadataRecoveryStrategy).IsEqualTo(MetadataRecoveryStrategy.None);
        await Assert.That(options.MetadataRecoveryRebootstrapTriggerMs).IsEqualTo(90000);
        await Assert.That(options.ClientDnsLookup).IsEqualTo(ClientDnsLookup.ResolveCanonicalBootstrapServersOnly);
        await Assert.That(options.PrefetchPipelineDepth).IsEqualTo(4);
        await Assert.That(options.ConnectionsPerBroker).IsEqualTo(2);
        await Assert.That(options.EnableAdaptiveConnections).IsFalse();
        await Assert.That(options.EnableAdaptiveFetchSizing).IsTrue();
        await Assert.That(options.AdaptiveFetchSizingOptions).IsNotNull();
        await Assert.That(options.AdaptiveFetchSizingOptions!.MaxFetchMaxBytes).IsEqualTo(67108864);
        await Assert.That(deadLetterOptions.BootstrapServers).IsEqualTo("broker1:9092,broker2:9092");
        await Assert.That(deadLetterOptions.TopicSuffix).IsEqualTo(".dead");
    }

    [Test]
    public async Task AddConsumer_WithByDurationAutoOffsetReset_BindsIsoDuration()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Kafka:Consumers:Orders:BootstrapServers:0"] = "broker1:9092",
            ["Kafka:Consumers:Orders:AutoOffsetReset"] = "by_duration:PT24H"
        });

        services.AddDekaf(builder =>
        {
            builder.AddConsumer<string, string>(configuration.GetSection("Kafka:Consumers:Orders"));
        });

        var provider = services.BuildServiceProvider();
        var consumer = provider.GetRequiredService<IKafkaConsumer<string, string>>();
        var options = GetConsumerOptions(consumer);

        await Assert.That(options.AutoOffsetReset).IsEqualTo(AutoOffsetReset.ByDuration);
        await Assert.That(options.AutoOffsetResetDuration).IsEqualTo(TimeSpan.FromHours(24));
    }

    [Test]
    public async Task AddConsumer_WithByDurationAutoOffsetReset_BindsSplitDuration()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Kafka:Consumers:Orders:BootstrapServers:0"] = "broker1:9092",
            ["Kafka:Consumers:Orders:AutoOffsetReset"] = "ByDuration",
            ["Kafka:Consumers:Orders:AutoOffsetResetDuration"] = "24:00:00"
        });

        services.AddDekaf(builder =>
        {
            builder.AddConsumer<string, string>(configuration.GetSection("Kafka:Consumers:Orders"));
        });

        var provider = services.BuildServiceProvider();
        var consumer = provider.GetRequiredService<IKafkaConsumer<string, string>>();
        var options = GetConsumerOptions(consumer);

        await Assert.That(options.AutoOffsetReset).IsEqualTo(AutoOffsetReset.ByDuration);
        await Assert.That(options.AutoOffsetResetDuration).IsEqualTo(TimeSpan.FromHours(24));
    }

    [Test]
    public async Task AddConsumer_WithByDurationAutoOffsetResetWithoutDuration_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Kafka:Consumers:Orders:BootstrapServers:0"] = "broker1:9092",
            ["Kafka:Consumers:Orders:AutoOffsetReset"] = "ByDuration"
        });

        void Act() => services.AddDekaf(builder =>
        {
            builder.AddConsumer<string, string>(configuration.GetSection("Kafka:Consumers:Orders"));
        });

        await Assert.That(Act).Throws<InvalidOperationException>()
            .WithMessageContaining("AutoOffsetResetDuration is required");
    }

    [Test]
    public async Task AddAdminClient_WithConfigurationSection_BindsConfiguredOptions()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Kafka:Admin:BootstrapServers"] = "broker1:9092",
            ["Kafka:Admin:ClientId"] = "admin-client",
            ["Kafka:Admin:RequestTimeoutMs"] = "45000",
            ["Kafka:Admin:ReconnectBackoffMs"] = "90",
            ["Kafka:Admin:ReconnectBackoffMaxMs"] = "900",
            ["Kafka:Admin:UseTls"] = "true",
            ["Kafka:Admin:SaslMechanism"] = "ScramSha512",
            ["Kafka:Admin:SaslUsername"] = "admin",
            ["Kafka:Admin:SaslPassword"] = "secret",
            ["Kafka:Admin:SaslScramTokenAuth"] = "true",
            ["Kafka:Admin:MetadataRecoveryStrategy"] = "None",
            ["Kafka:Admin:MetadataRecoveryRebootstrapTriggerMs"] = "120000",
            ["Kafka:Admin:ClientDnsLookup"] = "ResolveCanonicalBootstrapServersOnly"
        });

        services.AddDekaf(builder =>
        {
            builder.AddAdminClient(configuration.GetSection("Kafka:Admin"));
        });

        var provider = services.BuildServiceProvider();
        var admin = provider.GetRequiredService<IAdminClient>();
        var options = GetAdminOptions(admin);

        await Assert.That(options.BootstrapServers.Count).IsEqualTo(1);
        await Assert.That(options.BootstrapServers[0]).IsEqualTo("broker1:9092");
        await Assert.That(options.ClientId).IsEqualTo("admin-client");
        await Assert.That(options.RequestTimeoutMs).IsEqualTo(45000);
        await Assert.That(options.ReconnectBackoffMs).IsEqualTo(90);
        await Assert.That(options.ReconnectBackoffMaxMs).IsEqualTo(900);
        await Assert.That(options.UseTls).IsTrue();
        await Assert.That(options.SaslMechanism).IsEqualTo(SaslMechanism.ScramSha512);
        await Assert.That(options.SaslUsername).IsEqualTo("admin");
        await Assert.That(options.SaslPassword).IsEqualTo("secret");
        await Assert.That(options.SaslScramTokenAuth).IsTrue();
        await Assert.That(options.MetadataRecoveryStrategy).IsEqualTo(MetadataRecoveryStrategy.None);
        await Assert.That(options.MetadataRecoveryRebootstrapTriggerMs).IsEqualTo(120000);
        await Assert.That(options.ClientDnsLookup).IsEqualTo(ClientDnsLookup.ResolveCanonicalBootstrapServersOnly);
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

    [Test]
    public async Task AddKeyedProducerAndConsumer_RegisterAsInitializableKafkaClients()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddProducer<string, string>("orders", p => p.WithBootstrapServers("localhost:9092"));
            builder.AddConsumer<string, string>("orders", c => c
                .WithBootstrapServers("localhost:9092")
                .WithGroupId("orders"));
        });

        var provider = services.BuildServiceProvider();
        var producer = provider.GetRequiredKeyedService<IKafkaProducer<string, string>>("orders");
        var consumer = provider.GetRequiredKeyedService<IKafkaConsumer<string, string>>("orders");
        var initializables = provider.GetServices<IInitializableKafkaClient>().ToList();

        await Assert.That(initializables.Count).IsEqualTo(2);
        await Assert.That(initializables).Contains(producer);
        await Assert.That(initializables).Contains(consumer);
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

    private static IConfigurationRoot BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private static ProducerOptions GetProducerOptions<TKey, TValue>(IKafkaProducer<TKey, TValue> producer)
    {
        var field = producer.GetType().GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find _options field");
        return (ProducerOptions)field.GetValue(producer)!;
    }

    private static ConsumerOptions GetConsumerOptions<TKey, TValue>(IKafkaConsumer<TKey, TValue> consumer)
    {
        var field = consumer.GetType().GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find _options field");
        return (ConsumerOptions)field.GetValue(consumer)!;
    }

    private static AdminClientOptions GetAdminOptions(IAdminClient admin)
    {
        var field = admin.GetType().GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find _options field");
        return (AdminClientOptions)field.GetValue(admin)!;
    }

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
