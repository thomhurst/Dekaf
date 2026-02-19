namespace Dekaf.Tests.Unit.Builder;

public class ProducerBuilderValidationTests
{
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

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Build_WithUnsupportedValueType_ThrowsInvalidOperationException()
    {
        var builder = Kafka.CreateProducer<string, DateTime>()
            .WithBootstrapServers("localhost:9092");

        var act = () => builder.Build();

        await Assert.That(act).Throws<InvalidOperationException>();
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
    public async Task WithTransactionalId_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithTransactionalId("txn-1");
        await Assert.That(result).IsSameReferenceAs(builder);
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
    public async Task WithIdempotence_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithIdempotence(false);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    #endregion
}
