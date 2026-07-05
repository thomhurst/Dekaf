using Dekaf;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Extensions.HealthChecks;
using Dekaf.Extensions.Hosting;
using Dekaf.Producer;
using Dekaf.Testing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();

services.AddDekaf(dekaf =>
{
    dekaf.AddGlobalProducerInterceptor<string, string>(static _ => new AotProducerInterceptor());
    dekaf.AddGlobalConsumerInterceptor<string, string>(static _ => new AotConsumerInterceptor());

    dekaf.AddProducer<string, string>(
        new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "aot-producer"
        });

    dekaf.AddConsumer<string, string>(
        new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "aot-consumer",
            GroupId = "aot-group"
        },
        consumer => consumer.SubscribeTo("aot-topic"));

    dekaf.AddAdminClient(
        new AdminClientOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "aot-admin"
        });
});

await using var provider = services.BuildServiceProvider();
_ = provider.GetRequiredService<IKafkaProducer<string, string>>();
_ = provider.GetRequiredService<IKafkaConsumer<string, string>>();
_ = provider.GetRequiredService<IAdminClient>();

await RunTestingAndHealthChecksSmokeAsync();
RunHostingSmoke();
RunOpenTelemetrySmoke();

static async Task RunTestingAndHealthChecksSmokeAsync()
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddDekafInMemory(options => options.DefaultPartitionCount = 1);
    services.AddHealthChecks()
        .AddDekafProducerHealthCheck<string, string>()
        .AddDekafBrokerHealthCheck();

    await using var provider = services.BuildServiceProvider();
    provider.GetRequiredService<InMemoryKafkaCluster>().CreateTopic("aot-topic");

    var producer = provider.GetRequiredService<IKafkaProducer<string, string>>();
    await producer.ProduceAsync("aot-topic", "key", "value");

    var report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();
    if (report.Entries.Count != 2)
        throw new InvalidOperationException("Health check registration smoke failed.");
}

static void RunHostingSmoke()
{
    IHostBuilder hostBuilder = new HostBuilder();
    hostBuilder.UseKafkaConsumer<AotHostedConsumerService, string, string>();
    GC.KeepAlive(hostBuilder);
}

static void RunOpenTelemetrySmoke()
{
    GC.KeepAlive(typeof(Dekaf.OpenTelemetry.MeterProviderBuilderExtensions));
    GC.KeepAlive(typeof(Dekaf.OpenTelemetry.TracerProviderBuilderExtensions));
}

file sealed class AotProducerInterceptor : IProducerInterceptor<string, string>
{
    public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message) => message;

    public void OnAcknowledgement(RecordMetadata metadata, Exception? exception)
    {
    }
}

file sealed class AotHostedConsumerService(
    IKafkaConsumer<string, string> consumer,
    ILogger<AotHostedConsumerService> logger)
    : KafkaConsumerService<string, string>(consumer, logger)
{
    protected override IEnumerable<string> Topics => ["aot-topic"];

    protected override ValueTask ProcessAsync(
        ConsumeResult<string, string> result,
        CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}

file sealed class AotConsumerInterceptor : IConsumerInterceptor<string, string>
{
    public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result) => result;

    public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets)
    {
    }
}
