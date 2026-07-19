using System.Net;
using Dekaf.Consumer;
using Dekaf.Networking;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("NetworkPartition")]
[NotInParallel("DnsReresolutionKafkaContainer")]
[ClassDataSource<DnsReresolutionKafkaContainer>(Shared = SharedType.PerTestSession)]
public class DnsReresolutionTests(DnsReresolutionKafkaContainer kafka)
{
    [Test]
    public async Task Producer_Reconnect_ReresolvesStableBrokerHostnameAfterIpChange()
    {
        await kafka.ResetRelaysAsync();
        var lookup = new SwitchingDnsLookup(DnsReresolutionKafkaContainer.PrimaryAddress);
        var topic = NewTopic();

        await using var producer = await CreateProducerAsync(lookup);
        var first = await producer.ProduceAsync(topic, "key-0", "value-0");
        var callsBeforeIpChange = lookup.InvocationCount;

        lookup.Use(DnsReresolutionKafkaContainer.SecondaryAddress);
        await kafka.StopPrimaryRelayAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var second = await producer.ProduceAsync(topic, "key-1", "value-1", timeout.Token);

        await Assert.That(first.Offset).IsEqualTo(0);
        await Assert.That(second.Offset).IsEqualTo(1);
        await Assert.That(lookup.InvocationCount).IsGreaterThan(callsBeforeIpChange);
        await Assert.That(kafka.SecondaryAcceptedConnections).IsGreaterThan(0);
    }

    [Test]
    public async Task Consumer_Reconnect_ReresolvesWithoutSkippingOrDuplicatingRecords()
    {
        await kafka.ResetRelaysAsync();
        var producerLookup = new SwitchingDnsLookup(DnsReresolutionKafkaContainer.PrimaryAddress);
        var consumerLookup = new SwitchingDnsLookup(DnsReresolutionKafkaContainer.PrimaryAddress);
        var topic = NewTopic();

        await using var producer = await CreateProducerAsync(producerLookup);
        await producer.ProduceAsync(topic, "key-0", "value-0");

        await using var consumer = await CreateConsumerAsync(consumerLookup);
        consumer.Assign(new TopicPartition(topic, 0));

        var values = new List<string>();
        using (var firstTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
        {
            await foreach (var message in consumer.ConsumeAsync(firstTimeout.Token))
            {
                values.Add(message.Value!);
                break;
            }
        }

        var callsBeforeIpChange = consumerLookup.InvocationCount;
        producerLookup.Use(DnsReresolutionKafkaContainer.SecondaryAddress);
        consumerLookup.Use(DnsReresolutionKafkaContainer.SecondaryAddress);
        await kafka.StopPrimaryRelayAsync();

        for (var i = 1; i < 10; i++)
            await producer.ProduceAsync(topic, $"key-{i}", $"value-{i}");

        using (var remainingTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
        {
            await foreach (var message in consumer.ConsumeAsync(remainingTimeout.Token))
            {
                values.Add(message.Value!);
                if (values.Count == 10)
                    break;
            }
        }

        var expectedValues = Enumerable.Range(0, 10).Select(static i => $"value-{i}");
        await Assert.That(values.SequenceEqual(expectedValues)).IsTrue();
        await Assert.That(consumerLookup.InvocationCount).IsGreaterThan(callsBeforeIpChange);
        await Assert.That(kafka.SecondaryAcceptedConnections).IsGreaterThan(0);
    }

    [Test]
    public async Task BootstrapReresolution_RecoversWhenCachedIpIsDead()
    {
        await kafka.ResetRelaysAsync();
        var lookup = new SwitchingDnsLookup(DnsReresolutionKafkaContainer.PrimaryAddress);
        var topic = NewTopic();

        await using var producer = await CreateProducerAsync(lookup, rebootstrapImmediately: true);
        await producer.ProduceAsync(topic, "key-0", "value-0");
        var callsBeforeFailure = lookup.InvocationCount;

        lookup.Use(DnsReresolutionKafkaContainer.SecondaryAddress);
        await kafka.StopPrimaryRelayAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await producer.ProduceAsync(topic, "key-1", "value-1", timeout.Token);

        await Assert.That(result.Offset).IsEqualTo(1);
        await Assert.That(lookup.InvocationCount).IsGreaterThan(callsBeforeFailure);
    }

    [Test]
    public async Task MultipleARecords_ConnectsThroughNextAddressWhenFirstIsDead()
    {
        await kafka.ResetRelaysAsync();
        var lookup = new SwitchingDnsLookup(
            DnsReresolutionKafkaContainer.DeadAddress,
            DnsReresolutionKafkaContainer.SecondaryAddress);

        await using var producer = await CreateProducerAsync(lookup);
        var result = await producer.ProduceAsync(NewTopic(), "key", "value");

        await Assert.That(result.Offset).IsEqualTo(0);
        await Assert.That(lookup.InvocationCount).IsGreaterThan(0);
        await Assert.That(kafka.SecondaryAcceptedConnections).IsGreaterThan(0);
    }

    private async ValueTask<IKafkaProducer<string, string>> CreateProducerAsync(
        SwitchingDnsLookup lookup,
        bool rebootstrapImmediately = false)
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithDnsResolver(new ClientDnsEndpointResolver(lookup))
            .WithAcks(Acks.All)
            .WithRequestTimeout(TimeSpan.FromSeconds(3))
            .WithDeliveryTimeout(TimeSpan.FromSeconds(30))
            .WithReconnectBackoff(TimeSpan.FromMilliseconds(20))
            .WithReconnectBackoffMax(TimeSpan.FromMilliseconds(100))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory());

        if (rebootstrapImmediately)
            builder = builder.WithMetadataRecoveryRebootstrapTrigger(TimeSpan.Zero);

        return await builder.BuildAsync();
    }

    private async ValueTask<IKafkaConsumer<string, string>> CreateConsumerAsync(SwitchingDnsLookup lookup)
    {
        return await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithDnsResolver(new ClientDnsEndpointResolver(lookup))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithMaxPollRecords(1)
            .WithFetchMaxWait(TimeSpan.FromMilliseconds(100))
            .WithReconnectBackoff(TimeSpan.FromMilliseconds(20))
            .WithReconnectBackoffMax(TimeSpan.FromMilliseconds(100))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
    }

    private static string NewTopic() => $"dns-reresolution-{Guid.NewGuid():N}";
}
