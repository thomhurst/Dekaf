using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public sealed class MaxBlockMsTests
{
    #region ProducerOptions

    [Test]
    public async Task MaxBlockMs_DefaultValue_Is60000()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"]
        };

        await Assert.That(options.MaxBlockMs).IsEqualTo(60000);
    }

    [Test]
    public async Task MaxBlockMs_CanBeSetToCustomValue()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            MaxBlockMs = 5000
        };

        await Assert.That(options.MaxBlockMs).IsEqualTo(5000);
    }

    [Test]
    public async Task MaxBlockMs_CanBeSetToLargeValue()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            MaxBlockMs = 300000
        };

        await Assert.That(options.MaxBlockMs).IsEqualTo(300000);
    }

    [Test]
    public async Task MaxBlockMs_CanBeSetToOne()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            MaxBlockMs = 1
        };

        await Assert.That(options.MaxBlockMs).IsEqualTo(1);
    }

    #endregion

    #region Builder - WithMaxBlockMs

    [Test]
    public async Task WithMaxBlockMs_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithMaxBlockMs(5000);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithMaxBlockMs_BuildSucceeds()
    {
        var act = () => Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMaxBlockMs(5000)
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task WithMaxBlockMs_Zero_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithMaxBlockMs(0);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task WithMaxBlockMs_Negative_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithMaxBlockMs(-1);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task WithMaxBlockMs_NegativeLargeValue_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithMaxBlockMs(-60000);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Builder - WithMaxBlock (TimeSpan)

    [Test]
    public async Task WithMaxBlock_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithMaxBlock(TimeSpan.FromSeconds(5));
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithMaxBlock_BuildSucceeds()
    {
        var act = () => Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMaxBlock(TimeSpan.FromSeconds(30))
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task WithMaxBlock_Zero_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithMaxBlock(TimeSpan.Zero);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task WithMaxBlock_Negative_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithMaxBlock(TimeSpan.FromSeconds(-1));

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Builder chaining

    [Test]
    public async Task WithMaxBlockMs_ChainsWithOtherBuilderMethods()
    {
        var act = () => Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMaxBlockMs(10000)
            .WithBufferMemory(1024 * 1024)
            .WithLingerMs(5)
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task WithMaxBlock_ChainsWithOtherBuilderMethods()
    {
        var act = () => Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMaxBlock(TimeSpan.FromSeconds(10))
            .WithBufferMemory(1024 * 1024)
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region RecordAccumulator timeout uses MaxBlockMs

    [Test]
    public async Task RecordAccumulator_UsesMaxBlockMs_ForBufferTimeout()
    {
        // Create options with a very small buffer and very short MaxBlockMs
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BufferMemory = 1, // 1 byte - effectively always full
            MaxBlockMs = 50   // 50ms timeout
        };

        await using var accumulator = new RecordAccumulator(options);

        // Try to reserve more memory than available - should timeout with MaxBlockMs
        var act = async () => await accumulator.ReserveMemoryAsyncForBackpressure(1024, CancellationToken.None)
            .ConfigureAwait(false);

        await Assert.That(act).Throws<TimeoutException>();
    }

    [Test]
    public async Task RecordAccumulator_TimeoutMessage_ContainsMaxBlockMs()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BufferMemory = 1,
            MaxBlockMs = 50
        };

        await using var accumulator = new RecordAccumulator(options);

        try
        {
            await accumulator.ReserveMemoryAsyncForBackpressure(1024, CancellationToken.None)
                .ConfigureAwait(false);

            // Should not reach here
            throw new InvalidOperationException("Expected TimeoutException was not thrown");
        }
        catch (TimeoutException ex)
        {
            await Assert.That(ex.Message).Contains("max.block.ms");
            await Assert.That(ex.Message).Contains("50");
        }
    }

    #endregion
}
