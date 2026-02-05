using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Metadata;

/// <summary>
/// Tests for MetadataMaxAgeMs configuration across producer, consumer, and admin client.
/// Verifies defaults, builder methods, validation, and metadata age tracking.
/// </summary>
public sealed class MetadataMaxAgeTests
{
    #region ProducerOptions Defaults

    [Test]
    public async Task ProducerOptions_MetadataMaxAgeMs_DefaultsTo_300000()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"]
        };

        await Assert.That(options.MetadataMaxAgeMs).IsEqualTo(300000);
    }

    [Test]
    public async Task ProducerOptions_MetadataMaxAgeMs_CanBeCustomized()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            MetadataMaxAgeMs = 60000
        };

        await Assert.That(options.MetadataMaxAgeMs).IsEqualTo(60000);
    }

    #endregion

    #region ConsumerOptions Defaults

    [Test]
    public async Task ConsumerOptions_MetadataMaxAgeMs_DefaultsTo_300000()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"]
        };

        await Assert.That(options.MetadataMaxAgeMs).IsEqualTo(300000);
    }

    [Test]
    public async Task ConsumerOptions_MetadataMaxAgeMs_CanBeCustomized()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            MetadataMaxAgeMs = 120000
        };

        await Assert.That(options.MetadataMaxAgeMs).IsEqualTo(120000);
    }

    #endregion

    #region AdminClientOptions Defaults

    [Test]
    public async Task AdminClientOptions_MetadataMaxAgeMs_DefaultsTo_300000()
    {
        var options = new AdminClientOptions
        {
            BootstrapServers = ["localhost:9092"]
        };

        await Assert.That(options.MetadataMaxAgeMs).IsEqualTo(300000);
    }

    [Test]
    public async Task AdminClientOptions_MetadataMaxAgeMs_CanBeCustomized()
    {
        var options = new AdminClientOptions
        {
            BootstrapServers = ["localhost:9092"],
            MetadataMaxAgeMs = 600000
        };

        await Assert.That(options.MetadataMaxAgeMs).IsEqualTo(600000);
    }

    #endregion

    #region MetadataOptions Defaults

    [Test]
    public async Task MetadataOptions_MetadataRefreshInterval_DefaultsTo_5Minutes()
    {
        var options = new MetadataOptions();

        await Assert.That(options.MetadataRefreshInterval).IsEqualTo(TimeSpan.FromMilliseconds(300000));
    }

    [Test]
    public async Task MetadataOptions_EnableBackgroundRefresh_DefaultsTo_True()
    {
        var options = new MetadataOptions();

        await Assert.That(options.EnableBackgroundRefresh).IsTrue();
    }

    #endregion

    #region ProducerBuilder Methods

    [Test]
    public async Task ProducerBuilder_WithMetadataMaxAgeMs_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithMetadataMaxAgeMs(60000);

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ProducerBuilder_WithMetadataMaxAge_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithMetadataMaxAge(TimeSpan.FromMinutes(1));

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ProducerBuilder_WithMetadataMaxAgeMs_ZeroOrNegative_Throws()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithMetadataMaxAgeMs(0);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ProducerBuilder_WithMetadataMaxAgeMs_Negative_Throws()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithMetadataMaxAgeMs(-1);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ProducerBuilder_WithMetadataMaxAge_ZeroOrNegative_Throws()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.Zero);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ProducerBuilder_WithMetadataMaxAge_Negative_Throws()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.FromMilliseconds(-100));

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ProducerBuilder_WithMetadataMaxAgeMs_ThenBuild_Succeeds()
    {
        var act = () => Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataMaxAgeMs(60000)
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task ProducerBuilder_WithMetadataMaxAge_ThenBuild_Succeeds()
    {
        var act = () => Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataMaxAge(TimeSpan.FromMinutes(1))
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region ConsumerBuilder Methods

    [Test]
    public async Task ConsumerBuilder_WithMetadataMaxAgeMs_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithMetadataMaxAgeMs(60000);

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ConsumerBuilder_WithMetadataMaxAge_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithMetadataMaxAge(TimeSpan.FromMinutes(1));

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ConsumerBuilder_WithMetadataMaxAgeMs_ZeroOrNegative_Throws()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithMetadataMaxAgeMs(0);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ConsumerBuilder_WithMetadataMaxAgeMs_Negative_Throws()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithMetadataMaxAgeMs(-1);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ConsumerBuilder_WithMetadataMaxAge_ZeroOrNegative_Throws()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.Zero);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ConsumerBuilder_WithMetadataMaxAge_Negative_Throws()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.FromMilliseconds(-100));

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ConsumerBuilder_WithMetadataMaxAgeMs_ThenBuild_Succeeds()
    {
        var act = () => Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataMaxAgeMs(60000)
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task ConsumerBuilder_WithMetadataMaxAge_ThenBuild_Succeeds()
    {
        var act = () => Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataMaxAge(TimeSpan.FromMinutes(1))
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region AdminClientBuilder Methods

    [Test]
    public async Task AdminClientBuilder_WithMetadataMaxAgeMs_ReturnsSameBuilder()
    {
        var builder = new AdminClientBuilder();
        var result = builder.WithMetadataMaxAgeMs(60000);

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task AdminClientBuilder_WithMetadataMaxAge_ReturnsSameBuilder()
    {
        var builder = new AdminClientBuilder();
        var result = builder.WithMetadataMaxAge(TimeSpan.FromMinutes(1));

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task AdminClientBuilder_WithMetadataMaxAgeMs_ZeroOrNegative_Throws()
    {
        var builder = new AdminClientBuilder();

        var act = () => builder.WithMetadataMaxAgeMs(0);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AdminClientBuilder_WithMetadataMaxAgeMs_Negative_Throws()
    {
        var builder = new AdminClientBuilder();

        var act = () => builder.WithMetadataMaxAgeMs(-1);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AdminClientBuilder_WithMetadataMaxAge_ZeroOrNegative_Throws()
    {
        var builder = new AdminClientBuilder();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.Zero);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AdminClientBuilder_WithMetadataMaxAge_Negative_Throws()
    {
        var builder = new AdminClientBuilder();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.FromMilliseconds(-100));

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AdminClientBuilder_WithMetadataMaxAgeMs_ThenBuild_Succeeds()
    {
        var act = () => new AdminClientBuilder()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataMaxAgeMs(60000)
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task AdminClientBuilder_WithMetadataMaxAge_ThenBuild_Succeeds()
    {
        var act = () => new AdminClientBuilder()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataMaxAge(TimeSpan.FromMinutes(1))
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region Metadata Age Tracking

    [Test]
    public async Task ClusterMetadata_LastRefreshed_DefaultsTo_Default()
    {
        var metadata = new ClusterMetadata();

        await Assert.That(metadata.LastRefreshed).IsEqualTo(default(DateTimeOffset));
    }

    [Test]
    public async Task ClusterMetadata_LastRefreshed_UpdatedOnMetadataRefresh()
    {
        var metadata = new ClusterMetadata();
        var beforeRefresh = DateTimeOffset.UtcNow;

        // Simulate a metadata update
        var response = new Dekaf.Protocol.Messages.MetadataResponse
        {
            Brokers = [new Dekaf.Protocol.Messages.BrokerMetadata { NodeId = 1, Host = "broker1", Port = 9092 }],
            Topics = []
        };

        metadata.Update(response);

        var afterRefresh = DateTimeOffset.UtcNow;

        await Assert.That(metadata.LastRefreshed).IsGreaterThanOrEqualTo(beforeRefresh);
        await Assert.That(metadata.LastRefreshed).IsLessThanOrEqualTo(afterRefresh);
    }

    [Test]
    public async Task MetadataOptions_CustomRefreshInterval_IsUsed()
    {
        var customInterval = TimeSpan.FromSeconds(30);
        var options = new MetadataOptions
        {
            MetadataRefreshInterval = customInterval
        };

        await Assert.That(options.MetadataRefreshInterval).IsEqualTo(customInterval);
    }

    [Test]
    public async Task MetadataOptions_FromProducerMetadataMaxAgeMs_IsCorrect()
    {
        // Simulates how the producer creates MetadataOptions from MetadataMaxAgeMs
        var producerOptions = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            MetadataMaxAgeMs = 120000
        };

        var metadataOptions = new MetadataOptions
        {
            MetadataRefreshInterval = TimeSpan.FromMilliseconds(producerOptions.MetadataMaxAgeMs)
        };

        await Assert.That(metadataOptions.MetadataRefreshInterval).IsEqualTo(TimeSpan.FromMilliseconds(120000));
    }

    [Test]
    public async Task MetadataOptions_FromConsumerMetadataMaxAgeMs_IsCorrect()
    {
        // Simulates how the consumer creates MetadataOptions from MetadataMaxAgeMs
        var consumerOptions = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            MetadataMaxAgeMs = 60000
        };

        var metadataOptions = new MetadataOptions
        {
            MetadataRefreshInterval = TimeSpan.FromMilliseconds(consumerOptions.MetadataMaxAgeMs)
        };

        await Assert.That(metadataOptions.MetadataRefreshInterval).IsEqualTo(TimeSpan.FromMinutes(1));
    }

    [Test]
    public async Task MetadataOptions_FromAdminClientMetadataMaxAgeMs_IsCorrect()
    {
        // Simulates how the admin client creates MetadataOptions from MetadataMaxAgeMs
        var adminOptions = new AdminClientOptions
        {
            BootstrapServers = ["localhost:9092"],
            MetadataMaxAgeMs = 30000
        };

        var metadataOptions = new MetadataOptions
        {
            MetadataRefreshInterval = TimeSpan.FromMilliseconds(adminOptions.MetadataMaxAgeMs)
        };

        await Assert.That(metadataOptions.MetadataRefreshInterval).IsEqualTo(TimeSpan.FromSeconds(30));
    }

    #endregion

    #region Staleness Detection

    [Test]
    public async Task ClusterMetadata_AfterUpdate_IsNotStale()
    {
        var metadata = new ClusterMetadata();
        var maxAge = TimeSpan.FromMinutes(5);

        // Update metadata
        var response = new Dekaf.Protocol.Messages.MetadataResponse
        {
            Brokers = [new Dekaf.Protocol.Messages.BrokerMetadata { NodeId = 1, Host = "broker1", Port = 9092 }],
            Topics = []
        };
        metadata.Update(response);

        // Check staleness
        var age = DateTimeOffset.UtcNow - metadata.LastRefreshed;
        var isStale = age > maxAge;

        await Assert.That(isStale).IsFalse();
    }

    [Test]
    public async Task ClusterMetadata_WithDefaultLastRefreshed_IsStale()
    {
        var metadata = new ClusterMetadata();
        var maxAge = TimeSpan.FromMinutes(5);

        // No metadata update, LastRefreshed is default
        var age = DateTimeOffset.UtcNow - metadata.LastRefreshed;
        var isStale = age > maxAge;

        await Assert.That(isStale).IsTrue();
    }

    #endregion

    #region Builder Chaining

    [Test]
    public async Task ProducerBuilder_MetadataMaxAge_CanBeChainedWithOtherMethods()
    {
        var act = () => Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithClientId("my-producer")
            .WithMetadataMaxAgeMs(60000)
            .WithAcks(Dekaf.Producer.Acks.All)
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task ConsumerBuilder_MetadataMaxAge_CanBeChainedWithOtherMethods()
    {
        var act = () => Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("my-group")
            .WithMetadataMaxAge(TimeSpan.FromMinutes(2))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task AdminClientBuilder_MetadataMaxAge_CanBeChainedWithOtherMethods()
    {
        var act = () => new AdminClientBuilder()
            .WithBootstrapServers("localhost:9092")
            .WithClientId("my-admin")
            .WithMetadataMaxAge(TimeSpan.FromMinutes(10))
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    #endregion
}
