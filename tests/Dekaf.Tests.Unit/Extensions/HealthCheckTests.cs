using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Extensions.HealthChecks;
using Dekaf.Metadata;
using Dekaf.Producer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Dekaf.Tests.Unit.Extensions;

public class HealthCheckTests
{
    #region Consumer Health Check Tests

    [Test]
    public async Task ConsumerHealthCheck_NoAssignment_ReturnsUnhealthy()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        consumer.Assignment.Returns(new HashSet<TopicPartition>());

        var healthCheck = new DekafConsumerHealthCheck<string, string>(
            consumer, new DekafConsumerHealthCheckOptions());

        var result = await healthCheck.CheckHealthAsync(CreateContext());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Description).Contains("no partition assignment");
    }

    [Test]
    public async Task ConsumerHealthCheck_LowLag_ReturnsHealthy()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        var tp = new TopicPartition("test-topic", 0);
        consumer.Assignment.Returns(new HashSet<TopicPartition> { tp });
        consumer.GetPosition(tp).Returns(90L);
        consumer.QueryWatermarkOffsetsAsync(tp, Arg.Any<CancellationToken>()).Returns(new WatermarkOffsets(0, 100));

        var healthCheck = new DekafConsumerHealthCheck<string, string>(
            consumer, new DekafConsumerHealthCheckOptions());

        var result = await healthCheck.CheckHealthAsync(CreateContext());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Data["MaxLag"]).IsEqualTo(10L);
    }

    [Test]
    public async Task ConsumerHealthCheck_ModerateLag_ReturnsDegraded()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        var tp = new TopicPartition("test-topic", 0);
        consumer.Assignment.Returns(new HashSet<TopicPartition> { tp });
        consumer.GetPosition(tp).Returns(0L);
        consumer.QueryWatermarkOffsetsAsync(tp, Arg.Any<CancellationToken>()).Returns(new WatermarkOffsets(0, 5000));

        var options = new DekafConsumerHealthCheckOptions
        {
            DegradedThreshold = 1000,
            UnhealthyThreshold = 10000
        };

        var healthCheck = new DekafConsumerHealthCheck<string, string>(consumer, options);

        var result = await healthCheck.CheckHealthAsync(CreateContext());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That(result.Data["MaxLag"]).IsEqualTo(5000L);
    }

    [Test]
    public async Task ConsumerHealthCheck_HighLag_ReturnsUnhealthy()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        var tp = new TopicPartition("test-topic", 0);
        consumer.Assignment.Returns(new HashSet<TopicPartition> { tp });
        consumer.GetPosition(tp).Returns(0L);
        consumer.QueryWatermarkOffsetsAsync(tp, Arg.Any<CancellationToken>()).Returns(new WatermarkOffsets(0, 50000));

        var options = new DekafConsumerHealthCheckOptions
        {
            DegradedThreshold = 1000,
            UnhealthyThreshold = 10000
        };

        var healthCheck = new DekafConsumerHealthCheck<string, string>(consumer, options);

        var result = await healthCheck.CheckHealthAsync(CreateContext());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Data["MaxLag"]).IsEqualTo(50000L);
    }

    [Test]
    public async Task ConsumerHealthCheck_MultiplePartitions_ReportsMaxLag()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        var tp0 = new TopicPartition("test-topic", 0);
        var tp1 = new TopicPartition("test-topic", 1);
        consumer.Assignment.Returns(new HashSet<TopicPartition> { tp0, tp1 });
        consumer.GetPosition(tp0).Returns(90L);
        consumer.QueryWatermarkOffsetsAsync(tp0, Arg.Any<CancellationToken>()).Returns(new WatermarkOffsets(0, 100));
        consumer.GetPosition(tp1).Returns(0L);
        consumer.QueryWatermarkOffsetsAsync(tp1, Arg.Any<CancellationToken>()).Returns(new WatermarkOffsets(0, 2000));

        var options = new DekafConsumerHealthCheckOptions
        {
            DegradedThreshold = 1000,
            UnhealthyThreshold = 10000
        };

        var healthCheck = new DekafConsumerHealthCheck<string, string>(consumer, options);

        var result = await healthCheck.CheckHealthAsync(CreateContext());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That(result.Data["MaxLag"]).IsEqualTo(2000L);
    }

    [Test]
    public async Task ConsumerHealthCheck_NullPosition_SkipsPartition()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        var tp0 = new TopicPartition("test-topic", 0);
        var tp1 = new TopicPartition("test-topic", 1);
        consumer.Assignment.Returns(new HashSet<TopicPartition> { tp0, tp1 });
        consumer.GetPosition(tp0).Returns((long?)null);
        consumer.GetPosition(tp1).Returns(95L);
        consumer.QueryWatermarkOffsetsAsync(tp1, Arg.Any<CancellationToken>()).Returns(new WatermarkOffsets(0, 100));

        var healthCheck = new DekafConsumerHealthCheck<string, string>(
            consumer, new DekafConsumerHealthCheckOptions());

        var result = await healthCheck.CheckHealthAsync(CreateContext());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Data["MaxLag"]).IsEqualTo(5L);
    }

    [Test]
    public async Task ConsumerHealthCheck_ExceptionThrown_ReturnsUnhealthy()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        consumer.Assignment.Throws(new InvalidOperationException("Consumer disposed"));

        var healthCheck = new DekafConsumerHealthCheck<string, string>(
            consumer, new DekafConsumerHealthCheckOptions());

        var result = await healthCheck.CheckHealthAsync(CreateContext());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Exception).IsNotNull();
    }

    [Test]
    public async Task ConsumerHealthCheck_NegativeLag_ClampsToZero()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        var tp = new TopicPartition("test-topic", 0);
        consumer.Assignment.Returns(new HashSet<TopicPartition> { tp });
        consumer.GetPosition(tp).Returns(200L);
        consumer.QueryWatermarkOffsetsAsync(tp, Arg.Any<CancellationToken>()).Returns(new WatermarkOffsets(0, 100));

        var healthCheck = new DekafConsumerHealthCheck<string, string>(
            consumer, new DekafConsumerHealthCheckOptions());

        var result = await healthCheck.CheckHealthAsync(CreateContext());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Data["MaxLag"]).IsEqualTo(0L);
    }

    [Test]
    public async Task ConsumerHealthCheck_NullConsumer_ThrowsArgumentNullException()
    {
        await Assert.That(() => new DekafConsumerHealthCheck<string, string>(
            null!, new DekafConsumerHealthCheckOptions()))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ConsumerHealthCheck_NullOptions_ThrowsArgumentNullException()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();

        await Assert.That(() => new DekafConsumerHealthCheck<string, string>(
            consumer, null!))
            .Throws<ArgumentNullException>();
    }

    #endregion

    #region Producer Health Check Tests

    [Test]
    public async Task ProducerHealthCheck_FlushSucceeds_ReturnsHealthy()
    {
        var producer = Substitute.For<IKafkaProducer<string, string>>();
        producer.FlushAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.CompletedTask);

        var healthCheck = new DekafProducerHealthCheck<string, string>(
            producer, new DekafProducerHealthCheckOptions());

        var result = await healthCheck.CheckHealthAsync(CreateContext());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
    }

    [Test]
    public async Task ProducerHealthCheck_FlushThrows_ReturnsUnhealthy()
    {
        var producer = Substitute.For<IKafkaProducer<string, string>>();
        producer.FlushAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask(Task.FromException(new InvalidOperationException("Connection lost"))));

        var healthCheck = new DekafProducerHealthCheck<string, string>(
            producer, new DekafProducerHealthCheckOptions());

        var result = await healthCheck.CheckHealthAsync(CreateContext());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Exception).IsNotNull();
    }

    [Test]
    public async Task ProducerHealthCheck_FlushTimesOut_ReturnsUnhealthy()
    {
        var producer = Substitute.For<IKafkaProducer<string, string>>();
        producer.FlushAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var token = callInfo.Arg<CancellationToken>();
                return new ValueTask(Task.Delay(TimeSpan.FromSeconds(30), token));
            });

        var options = new DekafProducerHealthCheckOptions
        {
            Timeout = TimeSpan.FromMilliseconds(50)
        };

        var healthCheck = new DekafProducerHealthCheck<string, string>(producer, options);

        var result = await healthCheck.CheckHealthAsync(CreateContext());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Description).Contains("timed out");
    }

    [Test]
    public async Task ProducerHealthCheck_NullProducer_ThrowsArgumentNullException()
    {
        await Assert.That(() => new DekafProducerHealthCheck<string, string>(
            null!, new DekafProducerHealthCheckOptions()))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ProducerHealthCheck_NullOptions_ThrowsArgumentNullException()
    {
        var producer = Substitute.For<IKafkaProducer<string, string>>();

        await Assert.That(() => new DekafProducerHealthCheck<string, string>(
            producer, null!))
            .Throws<ArgumentNullException>();
    }

    #endregion

    #region Broker Health Check Tests

    [Test]
    public async Task BrokerHealthCheck_ClusterReachable_ReturnsHealthy()
    {
        var adminClient = Substitute.For<IAdminClient>();
        adminClient.DescribeClusterAsync(Arg.Any<CancellationToken>())
            .Returns(new ClusterDescription
            {
                ClusterId = "test-cluster",
                ControllerId = 1,
                Nodes =
                [
                    new BrokerNode { NodeId = 1, Host = "broker1", Port = 9092 },
                    new BrokerNode { NodeId = 2, Host = "broker2", Port = 9092 }
                ]
            });

        var options = new DekafBrokerHealthCheckOptions();

        var healthCheck = new DekafBrokerHealthCheck(adminClient, options);

        var result = await healthCheck.CheckHealthAsync(CreateContext());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Data["BrokerCount"]).IsEqualTo(2);
        await Assert.That(result.Data["ClusterId"]).IsEqualTo("test-cluster");
    }

    [Test]
    public async Task BrokerHealthCheck_NoBrokers_ReturnsUnhealthy()
    {
        var adminClient = Substitute.For<IAdminClient>();
        adminClient.DescribeClusterAsync(Arg.Any<CancellationToken>())
            .Returns(new ClusterDescription
            {
                ClusterId = "test-cluster",
                ControllerId = -1,
                Nodes = []
            });

        var options = new DekafBrokerHealthCheckOptions();

        var healthCheck = new DekafBrokerHealthCheck(adminClient, options);

        var result = await healthCheck.CheckHealthAsync(CreateContext());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Description).Contains("no brokers");
    }

    [Test]
    public async Task BrokerHealthCheck_ConnectionFails_ReturnsUnhealthy()
    {
        var adminClient = Substitute.For<IAdminClient>();
        adminClient.DescribeClusterAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<ClusterDescription>(
                Task.FromException<ClusterDescription>(new InvalidOperationException("Connection refused"))));

        var options = new DekafBrokerHealthCheckOptions();

        var healthCheck = new DekafBrokerHealthCheck(adminClient, options);

        var result = await healthCheck.CheckHealthAsync(CreateContext());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Exception).IsNotNull();
    }

    [Test]
    public async Task BrokerHealthCheck_TimesOut_ReturnsUnhealthy()
    {
        var adminClient = Substitute.For<IAdminClient>();
        adminClient.DescribeClusterAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var token = callInfo.Arg<CancellationToken>();
                var tcs = new TaskCompletionSource<ClusterDescription>();
                token.Register(() => tcs.TrySetCanceled(token));
                return new ValueTask<ClusterDescription>(tcs.Task);
            });

        var options = new DekafBrokerHealthCheckOptions
        {
            Timeout = TimeSpan.FromMilliseconds(50)
        };

        var healthCheck = new DekafBrokerHealthCheck(adminClient, options);

        var result = await healthCheck.CheckHealthAsync(CreateContext());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Description).Contains("timed out");
    }

    [Test]
    public async Task BrokerHealthCheck_NullAdminClient_ThrowsArgumentNullException()
    {
        var options = new DekafBrokerHealthCheckOptions();

        await Assert.That(() => new DekafBrokerHealthCheck(null!, options))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task BrokerHealthCheck_NullOptions_ThrowsArgumentNullException()
    {
        var adminClient = Substitute.For<IAdminClient>();

        await Assert.That(() => new DekafBrokerHealthCheck(adminClient, null!))
            .Throws<ArgumentNullException>();
    }

    #endregion

    #region Extension Method Registration Tests

    [Test]
    public async Task AddDekafConsumerHealthCheck_RegistersHealthCheck()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IKafkaConsumer<string, string>>());

        services.AddHealthChecks()
            .AddDekafConsumerHealthCheck<string, string>();

        var registration = services
            .Where(d => d.ServiceType == typeof(IHealthCheck) ||
                        d.ImplementationType?.Name.Contains("HealthCheck") == true)
            .ToList();

        // Health check registrations are stored internally in IOptions<HealthCheckServiceOptions>
        // Verify by building the service provider and resolving
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();

        await Assert.That(options.Value.Registrations.Count).IsEqualTo(1);
        await Assert.That(options.Value.Registrations.First().Name).IsEqualTo("dekaf-consumer");
    }

    [Test]
    public async Task AddDekafConsumerHealthCheck_CustomName_UsesProvidedName()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IKafkaConsumer<string, string>>());

        services.AddHealthChecks()
            .AddDekafConsumerHealthCheck<string, string>(name: "my-consumer-check");

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();

        await Assert.That(options.Value.Registrations.First().Name).IsEqualTo("my-consumer-check");
    }

    [Test]
    public async Task AddDekafProducerHealthCheck_RegistersHealthCheck()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IKafkaProducer<string, string>>());

        services.AddHealthChecks()
            .AddDekafProducerHealthCheck<string, string>();

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();

        await Assert.That(options.Value.Registrations.Count).IsEqualTo(1);
        await Assert.That(options.Value.Registrations.First().Name).IsEqualTo("dekaf-producer");
    }

    [Test]
    public async Task AddDekafBrokerHealthCheck_RegistersHealthCheck()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IAdminClient>());

        services.AddHealthChecks()
            .AddDekafBrokerHealthCheck();

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();

        await Assert.That(options.Value.Registrations.Count).IsEqualTo(1);
        await Assert.That(options.Value.Registrations.First().Name).IsEqualTo("dekaf-broker");
    }

    [Test]
    public async Task AddDekafBrokerHealthCheck_CustomName_UsesProvidedName()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IAdminClient>());

        services.AddHealthChecks()
            .AddDekafBrokerHealthCheck(name: "my-broker-check");

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();

        await Assert.That(options.Value.Registrations.First().Name).IsEqualTo("my-broker-check");
    }

    [Test]
    public async Task AllHealthChecks_CanBeRegisteredTogether()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IKafkaConsumer<string, string>>());
        services.AddSingleton(Substitute.For<IKafkaProducer<string, string>>());
        services.AddSingleton(Substitute.For<IAdminClient>());

        services.AddHealthChecks()
            .AddDekafConsumerHealthCheck<string, string>()
            .AddDekafProducerHealthCheck<string, string>()
            .AddDekafBrokerHealthCheck();

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();

        await Assert.That(options.Value.Registrations.Count).IsEqualTo(3);
    }

    #endregion

    #region Options Default Value Tests

    [Test]
    public async Task ConsumerHealthCheckOptions_DefaultValues()
    {
        var options = new DekafConsumerHealthCheckOptions();

        await Assert.That(options.DegradedThreshold).IsEqualTo(1000L);
        await Assert.That(options.UnhealthyThreshold).IsEqualTo(10000L);
    }

    [Test]
    public async Task ProducerHealthCheckOptions_DefaultValues()
    {
        var options = new DekafProducerHealthCheckOptions();

        await Assert.That(options.Timeout).IsEqualTo(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task BrokerHealthCheckOptions_DefaultTimeout()
    {
        var options = new DekafBrokerHealthCheckOptions();

        await Assert.That(options.Timeout).IsEqualTo(TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Helpers

    private static HealthCheckContext CreateContext()
    {
        return new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                "test",
                Substitute.For<IHealthCheck>(),
                null,
                null)
        };
    }

    #endregion
}
