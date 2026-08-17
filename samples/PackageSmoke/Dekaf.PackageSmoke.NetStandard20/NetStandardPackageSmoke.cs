using System;
using System.Buffers;
using System.Text;
using Dekaf;
using Dekaf.Consumer;
using Dekaf.Retry;
using Dekaf.Serialization;

namespace Dekaf.PackageSmoke.NetStandard20;

public static class NetStandardPackageSmoke
{
    public static string Run()
    {
        var headers = Headers
            .Create("source", "netstandard2.0")
            .AddIfNotNull("package", "Dekaf");

        var context = new SerializationContext
        {
            Topic = "package-smoke",
            Component = SerializationComponent.Value,
            Headers = headers
        };

        var payload = Encoding.UTF8.GetBytes("dekaf");
        var decoded = Serializers.String.Deserialize(new ReadOnlyMemory<byte>(payload), context);
        using var memoryManager = new NonArrayMemoryManager(payload);
        var nativeHeader = new Header("native", memoryManager.Memory);
        if (nativeHeader.GetValueAsString() != "dekaf")
            throw new InvalidOperationException("MemoryManager-backed header did not decode correctly.");

        var producerBuilder = new ProducerBuilder<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithClientId("package-smoke-producer")
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .WithKeySerializer(Serializers.String)
            .WithValueSerializer(Serializers.String)
            .WithRetryPolicy(NoRetryPolicy.Instance);

        var consumerBuilder = new ConsumerBuilder<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithClientId("package-smoke-consumer")
            .WithGroupId("package-smoke")
            .SubscribeTo("package-smoke")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithKeyDeserializer(Serializers.String)
            .WithValueDeserializer(Serializers.String)
            .WithRetryPolicy(NoRetryPolicy.Instance);

        return $"{decoded}:{headers.Count}:{producerBuilder.GetType().Name}:{consumerBuilder.GetType().Name}";
    }

    private sealed class NonArrayMemoryManager(byte[] bytes) : MemoryManager<byte>
    {
        public override Span<byte> GetSpan() => bytes;

        public override MemoryHandle Pin(int elementIndex = 0) =>
            throw new NotSupportedException();

        public override void Unpin()
        {
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
