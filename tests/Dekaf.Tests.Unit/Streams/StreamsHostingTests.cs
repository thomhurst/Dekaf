using Dekaf.Serialization;
using Dekaf.Streams;
using Dekaf.Streams.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dekaf.Tests.Unit.Streams;

public sealed class StreamsHostingTests
{
    [Test]
    public async Task AddDekafStreams_RegistersEachApplicationAndOneHostedService()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Marker("orders"));
        services.AddDekafStreams(
            "orders-app",
            (provider, topology) => topology
                .Stream(provider.GetRequiredService<Marker>().Topic, Serializers.String, Serializers.String)
                .ToTopic("orders-out", Serializers.String, Serializers.String),
            static (_, streams) => streams.WithBootstrapServers("localhost:9092"));
        services.AddDekafStreams(
            "payments-app",
            static topology => topology.Stream("payments", Serializers.String, Serializers.String)
                .ToTopic("payments-out", Serializers.String, Serializers.String),
            static streams => streams.WithBootstrapServers("localhost:9092").WithThreads(2));

        await using var provider = services.BuildServiceProvider();
        var applications = provider.GetServices<KafkaStreams>().ToArray();
        await Assert.That(applications.Length).IsEqualTo(2);
        await Assert.That(applications.All(static application => application.State == KafkaStreamsState.Created)).IsTrue();
        await Assert.That(provider.GetServices<IHostedService>().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task KafkaStreamsBuilder_ValidatesItsSettings()
    {
        var builder = new StreamsBuilder("app");
        builder.Stream("in", Serializers.String, Serializers.String).ToTopic("out", Serializers.String, Serializers.String);
        var topology = builder.Build();

        await Assert.That(() => KafkaStreams.Create(topology).Build()).Throws<InvalidOperationException>();
        await Assert.That(() => KafkaStreams.Create(topology).WithThreads(0)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => KafkaStreams.Create(topology).WithCommitInterval(TimeSpan.Zero)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => KafkaStreams.Create(topology).WithTransactionTimeout(TimeSpan.Zero)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => KafkaStreams.Create(topology).WithPersistentState("state", null!)).Throws<ArgumentNullException>();
        await Assert.That(() => KafkaStreams.Create(null!)).Throws<ArgumentNullException>();

        var streams = KafkaStreams.Create(topology)
            .WithBootstrapServers("localhost:9092")
            .WithProcessingGuarantee(ProcessingGuarantee.AtLeastOnce)
            .WithInstanceId("instance-1")
            .Build();
        await Assert.That(streams.State).IsEqualTo(KafkaStreamsState.Created);
    }

    private sealed record Marker(string Topic);
}
