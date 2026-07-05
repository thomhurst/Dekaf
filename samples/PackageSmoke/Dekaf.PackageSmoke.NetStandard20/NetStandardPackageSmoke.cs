using System;
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
}
