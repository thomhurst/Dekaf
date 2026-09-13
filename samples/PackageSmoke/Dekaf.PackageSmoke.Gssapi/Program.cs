using System.Reflection;
using System.Runtime.Versioning;
using Dekaf;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Security.Sasl;

var expectedFramework = $".NETCoreApp,Version=v{Environment.Version.Major}.0";
foreach (var assembly in new[] { typeof(Kafka).Assembly, typeof(TopicPartition).Assembly })
{
    var actualFramework = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
    if (actualFramework != expectedFramework)
        throw new InvalidOperationException($"{assembly.GetName().Name} selected {actualFramework}; expected {expectedFramework}.");
}

if (args is ["--verify-assets"])
{
    Console.WriteLine($"Package assets verified: {expectedFramework}.");
    return;
}

var bootstrapServers = Environment.GetEnvironmentVariable("DEKAF_KERBEROS_BOOTSTRAP")
    ?? throw new InvalidOperationException("DEKAF_KERBEROS_BOOTSTRAP must identify the local Kerberos broker.");
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
var cancellationToken = timeout.Token;
var topic = $"kerberos-package-{Guid.NewGuid():N}";
await using var admin = new AdminClientBuilder().WithBootstrapServers(bootstrapServers)
    .WithGssapi(new GssapiConfig()).Build();
await admin.CreateTopicsAsync([new NewTopic { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }],
    cancellationToken: cancellationToken);
await using var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers(bootstrapServers).WithGssapi(new GssapiConfig()).BuildAsync(cancellationToken);
await producer.ProduceAsync(topic, "package-key", "package-value", cancellationToken);
await using var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers(bootstrapServers).WithGssapi(new GssapiConfig())
    .WithAutoOffsetReset(AutoOffsetReset.Earliest).BuildAsync(cancellationToken);
consumer.Assign(new TopicPartition(topic, 0));
var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), cancellationToken);
if (record is null || record.Value.Key != "package-key" || record.Value.Value != "package-value")
    throw new InvalidOperationException("The authenticated package round trip returned the wrong record.");

try
{
    await using var invalid = await Kafka.CreateProducer<string, string>()
        .WithBootstrapServers(bootstrapServers)
        .WithGssapi(new GssapiConfig { ServiceName = "missing-service" }).BuildAsync(cancellationToken);
}
catch (AuthenticationException)
{
    Console.WriteLine($"Kerberos package round trip and invalid service rejection passed: {expectedFramework}.");
    return;
}
throw new InvalidOperationException("An invalid Kerberos service identity unexpectedly authenticated.");
