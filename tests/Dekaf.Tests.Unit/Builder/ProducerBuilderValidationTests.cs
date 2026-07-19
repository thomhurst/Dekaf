using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Builder;

public class ProducerBuilderValidationTests
{
    private const int DefaultIdempotentMaxInFlight = 5;
    private const int DefaultNonIdempotentMaxInFlight = 100;

    #region Build Validation

    [Test]
    public async Task Build_WithoutBootstrapServers_ThrowsInvalidOperationException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.Build();

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Build_WithBootstrapServers_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task Build_WithUnsupportedKeyType_ThrowsInvalidOperationException()
    {
        var builder = Kafka.CreateProducer<DateTime, string>()
            .WithBootstrapServers("localhost:9092");

        var act = () => builder.Build();

        await Assert.That(act).Throws<InvalidOperationException>()
            .And.HasMessageContaining("key serializer")
            .And.HasMessageContaining("WithKeySerializer");
    }

    [Test]
    public async Task Build_WithUnsupportedValueType_ThrowsInvalidOperationException()
    {
        var builder = Kafka.CreateProducer<string, DateTime>()
            .WithBootstrapServers("localhost:9092");

        var act = () => builder.Build();

        await Assert.That(act).Throws<InvalidOperationException>()
            .And.HasMessageContaining("value serializer")
            .And.HasMessageContaining("WithValueSerializer");
    }

    [Test]
    public async Task Build_WithBrotliCompression_ThrowsNotSupportedException()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .UseCompression(Dekaf.Protocol.Records.CompressionType.Brotli);

        var act = () => builder.Build();

        await Assert.That(act).Throws<NotSupportedException>()
            .And.HasMessageContaining("Brotli");
    }

    [Test]
    public async Task Build_WithArenaCapacityBelowBatchMinimum_ThrowsInvalidOperationException()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithBatchSize(1024)
            .WithArenaCapacity(1024);

        var act = () => builder.Build();

        await Assert.That(act).Throws<InvalidOperationException>()
            .And.HasMessageContaining("ArenaCapacity")
            .And.HasMessageContaining("BatchSize");
    }

    [Test]
    public async Task WithPartitionerAvailabilityTimeout_WhenNegative_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithPartitionerAvailabilityTimeout(TimeSpan.FromMilliseconds(-1));

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task BuildForTopic_WithNullTopic_ThrowsArgumentNullException()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092");

        var act = () => builder.BuildForTopic(null!);

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    #endregion

    #region Chaining Tests

    [Test]
    public async Task WithBootstrapServers_String_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithBootstrapServers("localhost:9092");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithBootstrapServers_Array_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithBootstrapServers("host1:9092", "host2:9092");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithClientId_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithClientId("my-client");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithAcks_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithAcks(Dekaf.Producer.Acks.Leader);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithLinger_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithLinger(TimeSpan.FromMilliseconds(5));
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task Build_DeliveryTimeoutEqualToRequestTimeoutPlusLinger_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithRequestTimeout(TimeSpan.FromMilliseconds(30_000))
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .WithDeliveryTimeout(TimeSpan.FromMilliseconds(30_005))
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task Build_DeliveryTimeoutGreaterThanRequestTimeoutPlusLinger_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithRequestTimeout(TimeSpan.FromMilliseconds(30_000))
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .WithDeliveryTimeout(TimeSpan.FromMilliseconds(30_006))
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task Build_DeliveryTimeoutBelowRequestTimeoutPlusLinger_ThrowsWithEffectiveValues()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithRequestTimeout(TimeSpan.FromMilliseconds(30_000))
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .WithDeliveryTimeout(TimeSpan.FromMilliseconds(30_004));

        var act = () => builder.Build();

        await Assert.That(act).Throws<InvalidOperationException>()
            .And.HasMessageContaining("DeliveryTimeoutMs (30004)")
            .And.HasMessageContaining("RequestTimeoutMs (30000)")
            .And.HasMessageContaining("LingerMs (5)")
            .And.HasMessageContaining("30005");
    }

    [Test]
    public async Task Build_HighThroughputProfileUsesEffectiveLingerForDeliveryTimeoutValidation()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithRequestTimeout(TimeSpan.FromMilliseconds(30_000))
            .WithDeliveryTimeout(TimeSpan.FromMilliseconds(30_099))
            .ForHighThroughput();

        var act = () => builder.Build();

        await Assert.That(act).Throws<InvalidOperationException>()
            .And.HasMessageContaining("LingerMs (100)")
            .And.HasMessageContaining("30100");
    }

    [Test]
    public async Task Build_DefaultTimeoutsRemainUnchanged()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        var options = GetOptions(producer);

        await Assert.That(options.DeliveryTimeoutMs).IsEqualTo(120_000);
        await Assert.That(options.RequestTimeoutMs).IsEqualTo(30_000);
        await Assert.That(options.LingerMs).IsEqualTo(0);
    }

    [Test]
    public async Task Build_LargeTimeoutsAtValidBoundary_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithRequestTimeout(TimeSpan.FromMilliseconds(int.MaxValue - 1L))
            .WithLinger(TimeSpan.FromMilliseconds(1))
            .WithDeliveryTimeout(TimeSpan.FromMilliseconds(int.MaxValue))
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task Build_LargeTimeoutSum_DoesNotOverflow()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithRequestTimeout(TimeSpan.FromMilliseconds(int.MaxValue))
            .WithLinger(TimeSpan.FromMilliseconds(int.MaxValue))
            .WithDeliveryTimeout(TimeSpan.FromMilliseconds(int.MaxValue));

        var act = () => builder.Build();

        await Assert.That(act).Throws<InvalidOperationException>()
            .And.HasMessageContaining("4294967294");
    }

    [Test]
    public async Task WithBatchSize_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithBatchSize(2048);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithBufferMemory_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithBufferMemory(1024);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithBufferMemoryAllocationStrategy_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithBufferMemoryAllocationStrategy(
            BufferMemoryAllocationStrategy.Incremental);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithBufferMemoryAllocationStrategy_InvalidValue_Throws()
    {
        var builder = Kafka.CreateProducer<string, string>();
        await Assert.That(() => builder.WithBufferMemoryAllocationStrategy(
                (BufferMemoryAllocationStrategy)42))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task WithTransactionalId_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithTransactionalId("txn-1");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithInlineTransactionCompletions_Disabled_WiresProducerOption()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTransactionalId("txn-1")
            .WithInlineTransactionCompletions(false)
            .Build();

        var options = GetOptions(producer);

        await Assert.That(options.InlineTransactionCompletions).IsFalse();
    }

    [Test]
    public async Task UseGzipCompression_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.UseGzipCompression();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task UseCompression_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.UseCompression(Dekaf.Protocol.Records.CompressionType.Zstd);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithPartitioner_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithPartitioner(Dekaf.Producer.PartitionerType.RoundRobin);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithAdaptivePartitioning_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithAdaptivePartitioning(false);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithPartitionerAvailabilityTimeout_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithPartitionerAvailabilityTimeout(TimeSpan.FromMilliseconds(5));
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithPartitionerIgnoreKeys_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithPartitionerIgnoreKeys();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task UseTls_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.UseTls();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithSaslPlain_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithSaslPlain("user", "pass");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithSaslScramSha256_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithSaslScramSha256("user", "pass");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithSaslScramSha512_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithSaslScramSha512("user", "pass");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    #endregion

    #region Preset Methods

    [Test]
    public async Task ForHighThroughput_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.ForHighThroughput();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ForLowLatency_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.ForLowLatency();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ForReliability_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.ForReliability();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ForHighThroughput_ThenBuild_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .ForHighThroughput()
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task ForLowLatency_ThenBuild_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .ForLowLatency()
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task ForReliability_ThenBuild_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .ForReliability()
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    #endregion

    #region Idempotence Validation

    [Test]
    public async Task Build_WithTransactionalIdAndIdempotenceDisabled_ThrowsInvalidOperationException()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTransactionalId("txn-1")
            .WithIdempotence(false);

        var act = () => builder.Build();

        await Assert.That(act).Throws<InvalidOperationException>()
            .And.HasMessageContaining("Idempotence cannot be disabled when TransactionalId is set");
    }

    [Test]
    public async Task Build_WithIdempotenceEnabledAndAcksNone_ThrowsInvalidOperationException()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithIdempotence(true)
            .WithAcks(Dekaf.Producer.Acks.None);

        var act = () => builder.Build();

        await Assert.That(act).Throws<InvalidOperationException>()
            .And.HasMessageContaining("Acks.None is incompatible");
    }

    [Test]
    public async Task Build_WithDefaultIdempotenceAndAcksNone_ThrowsInvalidOperationException()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithAcks(Dekaf.Producer.Acks.None);

        var act = () => builder.Build();

        await Assert.That(act).Throws<InvalidOperationException>()
            .And.HasMessageContaining("Acks.None is incompatible");
    }

    [Test]
    public async Task Build_WithIdempotenceDisabledAndAcksNone_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithIdempotence(false)
            .WithAcks(Dekaf.Producer.Acks.None)
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task Build_WithIdempotenceDisabled_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithIdempotence(false)
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task Build_WithIdempotenceEnabled_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithIdempotence(true)
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task Build_WithDefaultIdempotence_DefaultsMaxInFlightTo5()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        var options = GetOptions(producer);

        await Assert.That(options.MaxInFlightRequestsPerConnection).IsEqualTo(DefaultIdempotentMaxInFlight);
    }

    [Test]
    public async Task Build_WithIdempotenceDisabled_DefaultsMaxInFlightTo100()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithIdempotence(false)
            .Build();

        var options = GetOptions(producer);

        await Assert.That(options.MaxInFlightRequestsPerConnection).IsEqualTo(DefaultNonIdempotentMaxInFlight);
    }

    [Test]
    public async Task Build_WithIdempotenceDisabledAndExplicitMaxInFlight_PreservesValue()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithIdempotence(false)
            .WithMaxInFlightRequestsPerConnection(12)
            .Build();

        var options = GetOptions(producer);

        await Assert.That(options.MaxInFlightRequestsPerConnection).IsEqualTo(12);
    }

    [Test]
    public async Task Build_WithIdempotenceEnabledAndHighMaxInFlight_CapsAt5()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMaxInFlightRequestsPerConnection(100)
            .Build();

        var options = GetOptions(producer);

        await Assert.That(options.MaxInFlightRequestsPerConnection).IsEqualTo(DefaultIdempotentMaxInFlight);
    }

    [Test]
    public async Task WithIdempotence_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithIdempotence(false);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithDeliveryDiagnostics_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithDeliveryDiagnostics();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task Build_WithDeliveryDiagnostics_EnablesDiagnosticsOption()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithDeliveryDiagnostics()
            .Build();

        var options = GetOptions(producer);

        await Assert.That(options.EnableDeliveryDiagnostics).IsTrue();
    }

    #endregion

    #region ConnectionsPerBroker Validation

    [Test]
    public async Task Build_WithIdempotenceAndConnectionsPerBrokerGreaterThan1_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithIdempotence(true)
            .WithConnectionsPerBroker(4)
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task Build_WithDefaultIdempotenceAndConnectionsPerBrokerGreaterThan1_Succeeds()
    {
        // Default idempotence is true — partition affinity makes multi-connection safe
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithConnectionsPerBroker(2)
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task Build_WithIdempotenceDisabledAndConnectionsPerBrokerGreaterThan1_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithIdempotence(false)
            .WithConnectionsPerBroker(4)
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task Build_WithIdempotenceAndConnectionsPerBroker1_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithIdempotence(true)
            .WithConnectionsPerBroker(1)
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task WithConnectionsPerBroker_Zero_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithConnectionsPerBroker(0);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task WithConnectionsPerBroker_Negative_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithConnectionsPerBroker(-1);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task WithConnectionsPerBroker_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithConnectionsPerBroker(3);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    #endregion

    #region Multi-Connection Validation

    [Test]
    public async Task Build_IdempotentWithMultipleConnectionsPerBroker_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithIdempotence(true)
            .WithConnectionsPerBroker(3)
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task Build_TransactionalWithMultipleConnectionsPerBroker_ThrowsInvalidOperationException()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTransactionalId("test-txn")
            .WithConnectionsPerBroker(3);

        var act = () => builder.Build();

        await Assert.That(act).Throws<InvalidOperationException>()
            .And.HasMessageContaining("ConnectionsPerBroker");
    }

    [Test]
    public async Task Build_NonIdempotentWithMultipleConnectionsPerBroker_Succeeds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithIdempotence(false)
            .WithConnectionsPerBroker(3)
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    #endregion

    private static Dekaf.Producer.ProducerOptions GetOptions<TKey, TValue>(
        Dekaf.Producer.IKafkaProducer<TKey, TValue> producer)
    {
        var field = producer.GetType().GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find _options field");
        return (Dekaf.Producer.ProducerOptions)field.GetValue(producer)!;
    }
}
