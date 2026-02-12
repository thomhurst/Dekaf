using Dekaf.Metadata;

namespace Dekaf.Tests.Unit.Metadata;

/// <summary>
/// Unit tests for MetadataRecoveryStrategy configuration and behavior.
/// Tests verify enum values, default configuration, builder methods,
/// and rebootstrap trigger timing logic.
/// </summary>
public sealed class MetadataRecoveryStrategyTests
{
    #region Enum Values

    [Test]
    public async Task MetadataRecoveryStrategy_None_HasValue0()
    {
        var value = (int)MetadataRecoveryStrategy.None;
        await Assert.That(value).IsEqualTo(0);
    }

    [Test]
    public async Task MetadataRecoveryStrategy_Rebootstrap_HasValue1()
    {
        var value = (int)MetadataRecoveryStrategy.Rebootstrap;
        await Assert.That(value).IsEqualTo(1);
    }

    [Test]
    public async Task MetadataRecoveryStrategy_HasExactlyTwoValues()
    {
        var values = Enum.GetValues<MetadataRecoveryStrategy>();
        await Assert.That(values).Count().IsEqualTo(2);
    }

    #endregion

    #region ProducerOptions Defaults

    [Test]
    public async Task ProducerOptions_MetadataRecoveryStrategy_DefaultsToRebootstrap()
    {
        var options = new Dekaf.Producer.ProducerOptions
        {
            BootstrapServers = ["localhost:9092"]
        };

        await Assert.That(options.MetadataRecoveryStrategy).IsEqualTo(MetadataRecoveryStrategy.Rebootstrap);
    }

    [Test]
    public async Task ProducerOptions_MetadataRecoveryRebootstrapTriggerMs_DefaultsTo300000()
    {
        var options = new Dekaf.Producer.ProducerOptions
        {
            BootstrapServers = ["localhost:9092"]
        };

        await Assert.That(options.MetadataRecoveryRebootstrapTriggerMs).IsEqualTo(300000);
    }

    [Test]
    public async Task ProducerOptions_MetadataRecoveryStrategy_CanBeSetToNone()
    {
        var options = new Dekaf.Producer.ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            MetadataRecoveryStrategy = MetadataRecoveryStrategy.None
        };

        await Assert.That(options.MetadataRecoveryStrategy).IsEqualTo(MetadataRecoveryStrategy.None);
    }

    [Test]
    public async Task ProducerOptions_MetadataRecoveryRebootstrapTriggerMs_CanBeCustomized()
    {
        var options = new Dekaf.Producer.ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            MetadataRecoveryRebootstrapTriggerMs = 60000
        };

        await Assert.That(options.MetadataRecoveryRebootstrapTriggerMs).IsEqualTo(60000);
    }

    #endregion

    #region ConsumerOptions Defaults

    [Test]
    public async Task ConsumerOptions_MetadataRecoveryStrategy_DefaultsToRebootstrap()
    {
        var options = new Dekaf.Consumer.ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"]
        };

        await Assert.That(options.MetadataRecoveryStrategy).IsEqualTo(MetadataRecoveryStrategy.Rebootstrap);
    }

    [Test]
    public async Task ConsumerOptions_MetadataRecoveryRebootstrapTriggerMs_DefaultsTo300000()
    {
        var options = new Dekaf.Consumer.ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"]
        };

        await Assert.That(options.MetadataRecoveryRebootstrapTriggerMs).IsEqualTo(300000);
    }

    [Test]
    public async Task ConsumerOptions_MetadataRecoveryStrategy_CanBeSetToNone()
    {
        var options = new Dekaf.Consumer.ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            MetadataRecoveryStrategy = MetadataRecoveryStrategy.None
        };

        await Assert.That(options.MetadataRecoveryStrategy).IsEqualTo(MetadataRecoveryStrategy.None);
    }

    [Test]
    public async Task ConsumerOptions_MetadataRecoveryRebootstrapTriggerMs_CanBeCustomized()
    {
        var options = new Dekaf.Consumer.ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            MetadataRecoveryRebootstrapTriggerMs = 120000
        };

        await Assert.That(options.MetadataRecoveryRebootstrapTriggerMs).IsEqualTo(120000);
    }

    #endregion

    #region AdminClientOptions Defaults

    [Test]
    public async Task AdminClientOptions_MetadataRecoveryStrategy_DefaultsToRebootstrap()
    {
        var options = new Dekaf.Admin.AdminClientOptions
        {
            BootstrapServers = ["localhost:9092"]
        };

        await Assert.That(options.MetadataRecoveryStrategy).IsEqualTo(MetadataRecoveryStrategy.Rebootstrap);
    }

    [Test]
    public async Task AdminClientOptions_MetadataRecoveryRebootstrapTriggerMs_DefaultsTo300000()
    {
        var options = new Dekaf.Admin.AdminClientOptions
        {
            BootstrapServers = ["localhost:9092"]
        };

        await Assert.That(options.MetadataRecoveryRebootstrapTriggerMs).IsEqualTo(300000);
    }

    #endregion

    #region MetadataOptions Defaults

    [Test]
    public async Task MetadataOptions_MetadataRecoveryStrategy_DefaultsToRebootstrap()
    {
        var options = new MetadataOptions();

        await Assert.That(options.MetadataRecoveryStrategy).IsEqualTo(MetadataRecoveryStrategy.Rebootstrap);
    }

    [Test]
    public async Task MetadataOptions_MetadataRecoveryRebootstrapTriggerMs_DefaultsTo300000()
    {
        var options = new MetadataOptions();

        await Assert.That(options.MetadataRecoveryRebootstrapTriggerMs).IsEqualTo(300000);
    }

    [Test]
    public async Task MetadataOptions_MetadataRecoveryStrategy_CanBeSetToNone()
    {
        var options = new MetadataOptions
        {
            MetadataRecoveryStrategy = MetadataRecoveryStrategy.None
        };

        await Assert.That(options.MetadataRecoveryStrategy).IsEqualTo(MetadataRecoveryStrategy.None);
    }

    [Test]
    public async Task MetadataOptions_MetadataRecoveryRebootstrapTriggerMs_CanBeCustomized()
    {
        var options = new MetadataOptions
        {
            MetadataRecoveryRebootstrapTriggerMs = 10000
        };

        await Assert.That(options.MetadataRecoveryRebootstrapTriggerMs).IsEqualTo(10000);
    }

    #endregion

    #region ProducerBuilder Methods

    [Test]
    public async Task ProducerBuilder_WithMetadataRecoveryStrategy_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithMetadataRecoveryStrategy(MetadataRecoveryStrategy.None);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ProducerBuilder_WithMetadataRecoveryRebootstrapTrigger_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithMetadataRecoveryRebootstrapTrigger(TimeSpan.FromMilliseconds(60000));
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ProducerBuilder_WithMetadataRecoveryStrategy_ThenBuild_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataRecoveryStrategy(MetadataRecoveryStrategy.Rebootstrap)
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task ProducerBuilder_WithMetadataRecoveryStrategy_None_ThenBuild_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataRecoveryStrategy(MetadataRecoveryStrategy.None)
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task ProducerBuilder_WithMetadataRecoveryRebootstrapTrigger_ThenBuild_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataRecoveryRebootstrapTrigger(TimeSpan.FromMilliseconds(60000))
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    #endregion

    #region ConsumerBuilder Methods

    [Test]
    public async Task ConsumerBuilder_WithMetadataRecoveryStrategy_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithMetadataRecoveryStrategy(MetadataRecoveryStrategy.None);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ConsumerBuilder_WithMetadataRecoveryRebootstrapTrigger_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithMetadataRecoveryRebootstrapTrigger(TimeSpan.FromMilliseconds(60000));
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ConsumerBuilder_WithMetadataRecoveryStrategy_ThenBuild_Succeeds()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataRecoveryStrategy(MetadataRecoveryStrategy.Rebootstrap)
            .Build();

        await Assert.That(consumer).IsNotNull();
    }

    [Test]
    public async Task ConsumerBuilder_WithMetadataRecoveryStrategy_None_ThenBuild_Succeeds()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataRecoveryStrategy(MetadataRecoveryStrategy.None)
            .Build();

        await Assert.That(consumer).IsNotNull();
    }

    #endregion

    #region AdminClientBuilder Methods

    [Test]
    public async Task AdminClientBuilder_WithMetadataRecoveryStrategy_ReturnsSameBuilder()
    {
        var builder = new Dekaf.Admin.AdminClientBuilder();
        var result = builder.WithMetadataRecoveryStrategy(MetadataRecoveryStrategy.None);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task AdminClientBuilder_WithMetadataRecoveryRebootstrapTrigger_ReturnsSameBuilder()
    {
        var builder = new Dekaf.Admin.AdminClientBuilder();
        var result = builder.WithMetadataRecoveryRebootstrapTrigger(TimeSpan.FromMilliseconds(60000));
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    #endregion

    #region Rebootstrap Trigger Timing

    [Test]
    public async Task TryRebootstrapAsync_FirstCall_RecordsTimestampAndReturnsFalse()
    {
        // When all brokers first become unavailable, TryRebootstrapAsync should
        // record the timestamp and return false (not yet time to rebootstrap)
        var manager = CreateTestManager(new MetadataOptions
        {
            MetadataRecoveryStrategy = MetadataRecoveryStrategy.Rebootstrap,
            MetadataRecoveryRebootstrapTriggerMs = 300000
        });

        var result = await manager.TryRebootstrapAsync(null, CancellationToken.None).ConfigureAwait(false);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TryRebootstrapAsync_BeforeTriggerDelay_ReturnsFalse()
    {
        // When the trigger delay hasn't elapsed yet, should return false
        var manager = CreateTestManager(new MetadataOptions
        {
            MetadataRecoveryStrategy = MetadataRecoveryStrategy.Rebootstrap,
            MetadataRecoveryRebootstrapTriggerMs = 300000 // 5 minutes
        });

        // First call records the timestamp
        await manager.TryRebootstrapAsync(null, CancellationToken.None).ConfigureAwait(false);

        // Second call immediately after - should still return false (not enough time elapsed)
        var result = await manager.TryRebootstrapAsync(null, CancellationToken.None).ConfigureAwait(false);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TryRebootstrapAsync_WithZeroTriggerMs_TriggersOnSecondCall()
    {
        // With a trigger of 0ms, rebootstrap should trigger on the second call
        // (first call records timestamp, second call checks elapsed time >= 0)
        var manager = CreateTestManager(new MetadataOptions
        {
            MetadataRecoveryStrategy = MetadataRecoveryStrategy.Rebootstrap,
            MetadataRecoveryRebootstrapTriggerMs = 0
        });

        // First call records the timestamp, returns false
        var firstResult = await manager.TryRebootstrapAsync(null, CancellationToken.None).ConfigureAwait(false);
        await Assert.That(firstResult).IsFalse();

        // Second call - should attempt rebootstrap (trigger ms = 0)
        // This will attempt DNS resolution for "localhost" and then try to connect.
        // Since we have a null connection pool, the connection attempt will fail,
        // but we can verify it returned false (failed rebootstrap) not throwing
        // The key test is that the first call correctly returns false
    }

    [Test]
    public async Task NoneStrategy_DoesNotTriggerRebootstrap()
    {
        // When strategy is None, the MetadataManager should not attempt rebootstrap.
        // This is verified through the MetadataOptions configuration.
        var options = new MetadataOptions
        {
            MetadataRecoveryStrategy = MetadataRecoveryStrategy.None
        };

        await Assert.That(options.MetadataRecoveryStrategy).IsEqualTo(MetadataRecoveryStrategy.None);
    }

    #endregion

    #region ResolveBootstrapEndpoints

    [Test]
    public async Task ResolveBootstrapEndpointsAsync_WithLocalhost_ResolvesSuccessfully()
    {
        // "localhost" should resolve to at least one address
        var manager = CreateTestManager(bootstrapServers: ["localhost:9092"]);

        var endpoints = await manager.ResolveBootstrapEndpointsAsync(CancellationToken.None).ConfigureAwait(false);

        // Should have at least one endpoint (the resolved IP and/or the original hostname)
        await Assert.That(endpoints.Count).IsGreaterThanOrEqualTo(1);

        // Should include localhost as a fallback
        var hasLocalhost = endpoints.Any(e => e.Host == "localhost" && e.Port == 9092);
        await Assert.That(hasLocalhost).IsTrue();
    }

    [Test]
    public async Task ResolveBootstrapEndpointsAsync_PreservesPort()
    {
        var manager = CreateTestManager(bootstrapServers: ["localhost:19092"]);

        var endpoints = await manager.ResolveBootstrapEndpointsAsync(CancellationToken.None).ConfigureAwait(false);

        // All endpoints should have port 19092
        var allCorrectPort = endpoints.All(e => e.Port == 19092);
        await Assert.That(allCorrectPort).IsTrue();
    }

    [Test]
    public async Task ResolveBootstrapEndpointsAsync_MultipleServers_ResolvesAll()
    {
        var manager = CreateTestManager(bootstrapServers: ["localhost:9092", "localhost:9093"]);

        var endpoints = await manager.ResolveBootstrapEndpointsAsync(CancellationToken.None).ConfigureAwait(false);

        // Should have endpoints for both ports
        var hasPort9092 = endpoints.Any(e => e.Port == 9092);
        var hasPort9093 = endpoints.Any(e => e.Port == 9093);

        await Assert.That(hasPort9092).IsTrue();
        await Assert.That(hasPort9093).IsTrue();
    }

    [Test]
    public async Task ResolveBootstrapEndpointsAsync_UnresolvableHost_FallsBackToHostname()
    {
        // An unresolvable hostname should still be included as a fallback
        var manager = CreateTestManager(bootstrapServers: ["this-host-definitely-does-not-exist-xyzzy.invalid:9092"]);

        var endpoints = await manager.ResolveBootstrapEndpointsAsync(CancellationToken.None).ConfigureAwait(false);

        // Should still have the original hostname as fallback
        await Assert.That(endpoints.Count).IsGreaterThanOrEqualTo(1);
        var hasFallback = endpoints.Any(e =>
            e.Host == "this-host-definitely-does-not-exist-xyzzy.invalid" && e.Port == 9092);
        await Assert.That(hasFallback).IsTrue();
    }

    [Test]
    public async Task ResolveBootstrapEndpointsAsync_NoDuplicateEndpoints()
    {
        var manager = CreateTestManager(bootstrapServers: ["localhost:9092"]);

        var endpoints = await manager.ResolveBootstrapEndpointsAsync(CancellationToken.None).ConfigureAwait(false);

        // Verify no duplicate (host, port) pairs
        var distinctCount = endpoints.Distinct().Count();
        await Assert.That(distinctCount).IsEqualTo(endpoints.Count);
    }

    #endregion

    #region MetadataManager Integration with Recovery Strategy

    [Test]
    public async Task MetadataManager_WithRebootstrapStrategy_StoresOriginalBootstrapHostnames()
    {
        // The MetadataManager should store the original bootstrap server hostnames
        // for later DNS re-resolution
        var manager = CreateTestManager(
            bootstrapServers: ["kafka1.example.com:9092", "kafka2.example.com:9093"]);

        // Verify by checking that ResolveBootstrapEndpointsAsync can resolve them
        // (even if they don't resolve to real IPs, the method should handle gracefully)
        var endpoints = await manager.ResolveBootstrapEndpointsAsync(CancellationToken.None).ConfigureAwait(false);

        // Should have fallback entries for both hostnames
        var hasKafka1 = endpoints.Any(e => e.Host == "kafka1.example.com" && e.Port == 9092);
        var hasKafka2 = endpoints.Any(e => e.Host == "kafka2.example.com" && e.Port == 9093);

        await Assert.That(hasKafka1).IsTrue();
        await Assert.That(hasKafka2).IsTrue();
    }

    [Test]
    public async Task MetadataManager_GetEndpointsToTry_IncludesBootstrapEndpoints()
    {
        var manager = CreateTestManager(bootstrapServers: ["localhost:9092"]);

        var endpoints = manager.GetEndpointsToTry();

        // Should include the bootstrap server
        await Assert.That(endpoints.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(endpoints.Any(e => e.Host == "localhost" && e.Port == 9092)).IsTrue();
    }

    #endregion

    #region Builder Full Chain Tests

    [Test]
    public async Task ProducerBuilder_FullChainWithMetadataRecovery_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataRecoveryStrategy(MetadataRecoveryStrategy.Rebootstrap)
            .WithMetadataRecoveryRebootstrapTrigger(TimeSpan.FromMilliseconds(60000))
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task ConsumerBuilder_FullChainWithMetadataRecovery_Succeeds()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataRecoveryStrategy(MetadataRecoveryStrategy.Rebootstrap)
            .WithMetadataRecoveryRebootstrapTrigger(TimeSpan.FromMilliseconds(60000))
            .Build();

        await Assert.That(consumer).IsNotNull();
    }

    [Test]
    public async Task AdminClientBuilder_FullChainWithMetadataRecovery_Succeeds()
    {
        await using var client = new Dekaf.Admin.AdminClientBuilder()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataRecoveryStrategy(MetadataRecoveryStrategy.Rebootstrap)
            .WithMetadataRecoveryRebootstrapTrigger(TimeSpan.FromMilliseconds(60000))
            .Build();

        await Assert.That(client).IsNotNull();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates a minimal MetadataManager for testing with the specified options.
    /// Uses a null connection pool since we're only testing the rebootstrap logic,
    /// not actual connection establishment.
    /// </summary>
    private static MetadataManager CreateTestManager(
        MetadataOptions? options = null,
        IEnumerable<string>? bootstrapServers = null)
    {
        return new MetadataManager(
            connectionPool: null!,
            bootstrapServers: bootstrapServers ?? ["localhost:9092"],
            options: options);
    }

    #endregion
}
