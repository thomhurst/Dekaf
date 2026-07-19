using System.Buffers;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Serialization;

public class AsyncSerdeTests
{
    private sealed class YieldingStringSerde : IAsyncSerde<string>
    {
        public async ValueTask SerializeAsync(
            string value,
            IBufferWriter<byte> destination,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            destination.Write(Encoding.UTF8.GetBytes(value));
        }

        public async ValueTask<string> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return Encoding.UTF8.GetString(data.Span);
        }
    }

    [Test]
    public async Task ProducerBuilder_SyncAndAsyncKeySerializer_Throws()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithKeySerializer(Serializers.String)
            .WithKeySerializer(new YieldingStringSerde());

        await Assert.That(() => builder.Build()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ProducerBuilder_SyncAndAsyncValueSerializer_Throws()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithValueSerializer(Serializers.String)
            .WithValueSerializer(new YieldingStringSerde());

        await Assert.That(() => builder.Build()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ConsumerBuilder_SyncAndAsyncKeyDeserializer_Throws()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithKeyDeserializer(Serializers.String)
            .WithKeyDeserializer(new YieldingStringSerde());

        await Assert.That(() => builder.Build()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ConsumerBuilder_SyncAndAsyncValueDeserializer_Throws()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithValueDeserializer(Serializers.String)
            .WithValueDeserializer(new YieldingStringSerde());

        await Assert.That(() => builder.Build()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ProducerBuilder_AsyncSerializers_Builds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithKeySerializer(new YieldingStringSerde())
            .WithValueSerializer(new YieldingStringSerde())
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task ConsumeBatchAsync_WithAsyncDeserializer_ThrowsNotSupported()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("async-serde-batch-guard")
            .WithValueDeserializer(new YieldingStringSerde())
            .Build();

        await Assert.That(async () =>
        {
            await foreach (var _ in consumer.ConsumeBatchAsync(CancellationToken.None))
            {
                break;
            }
        }).Throws<NotSupportedException>();
    }

    [Test]
    public async Task AsyncOnlySerializerPlaceholder_Throws()
    {
        await Assert.That(() =>
        {
            var buffer = new ArrayBufferWriter<byte>();
            var context = new SerializationContext { Topic = "t", Component = SerializationComponent.Value };
            AsyncOnlySerializerPlaceholder<string>.Instance.Serialize("x", ref buffer, context);
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task AsyncOnlyDeserializerPlaceholder_Throws()
    {
        var context = new SerializationContext { Topic = "t", Component = SerializationComponent.Value };

        await Assert.That(() =>
            AsyncOnlyDeserializerPlaceholder<string>.Instance.Deserialize(ReadOnlyMemory<byte>.Empty, context))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task AsyncSerializationBufferWriter_WriteAndDetach_RoundTrips()
    {
        var writer = AsyncSerializationBufferWriter.Rent();
        try
        {
            var payload = Encoding.UTF8.GetBytes("async serde payload");
            writer.Write(payload);

            var pooled = writer.DetachWrittenMemory();
            try
            {
                await Assert.That(pooled.IsNull).IsFalse();
                await Assert.That(pooled.Memory.ToArray().SequenceEqual(payload)).IsTrue();
            }
            finally
            {
                pooled.Return();
            }
        }
        finally
        {
            AsyncSerializationBufferWriter.Return(writer);
        }
    }

    [Test]
    public async Task AsyncSerializationBufferWriter_EmptyDetach_IsEmptyNonNull()
    {
        var writer = AsyncSerializationBufferWriter.Rent();
        try
        {
            var pooled = writer.DetachWrittenMemory();
            await Assert.That(pooled.IsNull).IsFalse();
            await Assert.That(pooled.Length).IsEqualTo(0);
            pooled.Return();
        }
        finally
        {
            AsyncSerializationBufferWriter.Return(writer);
        }
    }

    [Test]
    public async Task AsyncSerializationBufferWriter_GrowsPastInitialCapacity()
    {
        var writer = AsyncSerializationBufferWriter.Rent();
        try
        {
            var payload = new byte[16 * 1024];
            new Random(42).NextBytes(payload);
            writer.Write(payload);

            var pooled = writer.DetachWrittenMemory();
            try
            {
                await Assert.That(pooled.Length).IsEqualTo(payload.Length);
                await Assert.That(pooled.Memory.ToArray().SequenceEqual(payload)).IsTrue();
            }
            finally
            {
                pooled.Return();
            }
        }
        finally
        {
            AsyncSerializationBufferWriter.Return(writer);
        }
    }

    [Test]
    public async Task AsyncSerializationBufferWriter_AdvancePastEnd_Throws()
    {
        var writer = AsyncSerializationBufferWriter.Rent();
        try
        {
            writer.GetSpan(1);
            await Assert.That(() => writer.Advance(1024 * 1024)).Throws<InvalidOperationException>();
        }
        finally
        {
            AsyncSerializationBufferWriter.Return(writer);
        }
    }
}
