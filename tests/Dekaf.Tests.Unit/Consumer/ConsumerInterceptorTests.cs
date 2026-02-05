using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerInterceptorTests
{
    #region OnConsume Tests

    [Test]
    public async Task OnConsume_InterceptorIsCalled_AndCanInspectResult()
    {
        var interceptor = new ConsumeTrackingInterceptor();

        var result = CreateTestConsumeResult("test-topic", 0, 42, "key", "value");

        var modified = interceptor.OnConsume(result);

        await Assert.That(interceptor.ConsumedResults).Count().IsEqualTo(1);
        await Assert.That(modified.Topic).IsEqualTo("test-topic");
        await Assert.That(modified.Offset).IsEqualTo(42);
    }

    [Test]
    public async Task OnConsume_MultipleInterceptors_ChainedInOrder()
    {
        var callOrder = new List<string>();
        var interceptor1 = new OrderTrackingConsumerInterceptor("first", callOrder);
        var interceptor2 = new OrderTrackingConsumerInterceptor("second", callOrder);
        var interceptor3 = new OrderTrackingConsumerInterceptor("third", callOrder);

        var result = CreateTestConsumeResult("test-topic", 0, 0, "key", "value");

        // Simulate what the consumer does internally
        result = interceptor1.OnConsume(result);
        result = interceptor2.OnConsume(result);
        result = interceptor3.OnConsume(result);

        await Assert.That(callOrder).Count().IsEqualTo(3);
        await Assert.That(callOrder[0]).IsEqualTo("first");
        await Assert.That(callOrder[1]).IsEqualTo("second");
        await Assert.That(callOrder[2]).IsEqualTo("third");
    }

    [Test]
    public async Task OnConsume_ExceptionInInterceptorDoesNotAffectOthers()
    {
        var throwingInterceptor = new ThrowingOnConsumeInterceptor();
        var trackingInterceptor = new ConsumeTrackingInterceptor();

        var result = CreateTestConsumeResult("test-topic", 0, 0, "key", "value");

        // First interceptor throws - consumer catches
        ConsumeResult<string, string> current = result;
        try
        {
            current = throwingInterceptor.OnConsume(current);
        }
        catch
        {
            // Consumer catches this - current stays as original result
        }

        // Second interceptor still runs
        current = trackingInterceptor.OnConsume(current);

        await Assert.That(trackingInterceptor.ConsumedResults).Count().IsEqualTo(1);
    }

    #endregion

    #region OnCommit Tests

    [Test]
    public async Task OnCommit_CalledWithCorrectOffsets()
    {
        var interceptor = new CommitTrackingInterceptor();

        var offsets = new List<TopicPartitionOffset>
        {
            new("topic-a", 0, 100),
            new("topic-a", 1, 200),
            new("topic-b", 0, 50)
        };

        interceptor.OnCommit(offsets);

        await Assert.That(interceptor.CommittedOffsets).Count().IsEqualTo(1);
        await Assert.That(interceptor.CommittedOffsets[0]).Count().IsEqualTo(3);
        await Assert.That(interceptor.CommittedOffsets[0][0].Topic).IsEqualTo("topic-a");
        await Assert.That(interceptor.CommittedOffsets[0][0].Partition).IsEqualTo(0);
        await Assert.That(interceptor.CommittedOffsets[0][0].Offset).IsEqualTo(100);
    }

    [Test]
    public async Task OnCommit_MultipleInterceptorsAllCalled()
    {
        var interceptor1 = new CommitTrackingInterceptor();
        var interceptor2 = new CommitTrackingInterceptor();

        var offsets = new List<TopicPartitionOffset>
        {
            new("test-topic", 0, 10)
        };

        // Simulate consumer calling all interceptors
        interceptor1.OnCommit(offsets);
        interceptor2.OnCommit(offsets);

        await Assert.That(interceptor1.CommittedOffsets).Count().IsEqualTo(1);
        await Assert.That(interceptor2.CommittedOffsets).Count().IsEqualTo(1);
    }

    [Test]
    public async Task OnCommit_ExceptionInInterceptorDoesNotAffectOthers()
    {
        var throwingInterceptor = new ThrowingOnCommitInterceptor();
        var trackingInterceptor = new CommitTrackingInterceptor();

        var offsets = new List<TopicPartitionOffset>
        {
            new("test-topic", 0, 10)
        };

        // First interceptor throws - consumer catches
        try
        {
            throwingInterceptor.OnCommit(offsets);
        }
        catch
        {
            // Consumer catches this
        }

        // Second interceptor still runs
        trackingInterceptor.OnCommit(offsets);

        await Assert.That(trackingInterceptor.CommittedOffsets).Count().IsEqualTo(1);
    }

    #endregion

    #region Builder Integration Tests

    [Test]
    public async Task AddInterceptor_ReturnsBuilderForChaining()
    {
        var interceptor = new NoOpConsumerInterceptor();

        var originalBuilder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092");

        var returnedBuilder = originalBuilder.AddInterceptor(interceptor);

        await Assert.That(returnedBuilder).IsSameReferenceAs(originalBuilder);
    }

    [Test]
    public async Task AddInterceptor_ThrowsOnNull()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092");

        var action = () => builder.AddInterceptor(null!);

        await Assert.That(action).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task MultipleInterceptors_CanBeAddedViaBuilder()
    {
        var interceptor1 = new NoOpConsumerInterceptor();
        var interceptor2 = new NoOpConsumerInterceptor();
        var interceptor3 = new NoOpConsumerInterceptor();

        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .AddInterceptor(interceptor1)
            .AddInterceptor(interceptor2)
            .AddInterceptor(interceptor3);

        await Assert.That(builder).IsNotNull();
    }

    #endregion

    #region Helpers

    private static ConsumeResult<string, string> CreateTestConsumeResult(
        string topic, int partition, long offset, string key, string value)
    {
        // Create a ConsumeResult using the partition EOF factory as base, then create a proper one
        // Since ConsumeResult is a readonly struct with eager deserialization, we create it directly
        // using the constructor with null deserializers (test scenario)
        return new ConsumeResult<string, string>(
            topic: topic,
            partition: partition,
            offset: offset,
            keyData: System.Text.Encoding.UTF8.GetBytes(key),
            isKeyNull: false,
            valueData: System.Text.Encoding.UTF8.GetBytes(value),
            isValueNull: false,
            headers: null,
            timestamp: DateTimeOffset.UtcNow,
            timestampType: TimestampType.CreateTime,
            leaderEpoch: null,
            keyDeserializer: Serializers.String,
            valueDeserializer: Serializers.String);
    }

    #endregion

    #region Test Interceptors

    private sealed class NoOpConsumerInterceptor : IConsumerInterceptor<string, string>
    {
        public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result) => result;
        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
    }

    private sealed class ConsumeTrackingInterceptor : IConsumerInterceptor<string, string>
    {
        public List<ConsumeResult<string, string>> ConsumedResults { get; } = [];

        public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result)
        {
            ConsumedResults.Add(result);
            return result;
        }

        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
    }

    private sealed class OrderTrackingConsumerInterceptor : IConsumerInterceptor<string, string>
    {
        private readonly string _name;
        private readonly List<string> _callOrder;

        public OrderTrackingConsumerInterceptor(string name, List<string> callOrder)
        {
            _name = name;
            _callOrder = callOrder;
        }

        public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result)
        {
            _callOrder.Add(_name);
            return result;
        }

        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
    }

    private sealed class ThrowingOnConsumeInterceptor : IConsumerInterceptor<string, string>
    {
        public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result)
        {
            throw new InvalidOperationException("Interceptor error on consume");
        }

        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
    }

    private sealed class CommitTrackingInterceptor : IConsumerInterceptor<string, string>
    {
        public List<IReadOnlyList<TopicPartitionOffset>> CommittedOffsets { get; } = [];

        public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result) => result;

        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets)
        {
            CommittedOffsets.Add(offsets);
        }
    }

    private sealed class ThrowingOnCommitInterceptor : IConsumerInterceptor<string, string>
    {
        public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result) => result;

        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets)
        {
            throw new InvalidOperationException("Interceptor error on commit");
        }
    }

    #endregion
}
