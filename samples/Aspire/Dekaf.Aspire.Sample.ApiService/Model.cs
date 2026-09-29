using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Extensions.Hosting;
using Dekaf.ShareConsumer;

namespace Dekaf.Aspire.Sample.ApiService;

/// <summary>An order published with JSON Schema serialization.</summary>
/// <param name="Id">The order identifier, also the message key.</param>
/// <param name="Total">The order total.</param>
public sealed record Order(string Id, decimal Total)
{
    internal const string JsonSchema = """
        {"type":"object","properties":{"Id":{"type":"string"},"Total":{"type":"number"}},"required":["Id","Total"]}
        """;
}

/// <summary>Counts the tasks processed by <see cref="TaskWorker"/>.</summary>
public sealed class TaskCounter
{
    private int _processed;

    /// <summary>Gets the number of processed tasks.</summary>
    public int Processed => Volatile.Read(ref _processed);

    /// <summary>Records one processed task.</summary>
    public void Increment() => Interlocked.Increment(ref _processed);
}

/// <summary>Processes queued tasks. Records are accepted after <see cref="ProcessAsync"/> returns.</summary>
public sealed partial class TaskWorker(IKafkaShareConsumer<string, string> consumer, ILogger<TaskWorker> logger, TaskCounter counter)
    : KafkaShareConsumerService<string, string>(consumer, logger)
{
    /// <inheritdoc />
    protected override IEnumerable<string> Topics => ["tasks"];

    /// <inheritdoc />
    protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> result, CancellationToken cancellationToken)
    {
        LogProcessed(logger, result.Value);
        counter.Increment();
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Processed task {Task}")]
    private static partial void LogProcessed(ILogger logger, string task);
}

/// <summary>Creates the sample's topics at startup, before the workers subscribe.</summary>
public sealed class TopicInitializer(IAdminClient admin) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await admin.CreateTopicsAsync(
                [new NewTopic { Name = "orders" }, new NewTopic { Name = "tasks" }],
                cancellationToken: cancellationToken);
        }
        catch (KafkaException ex) when (ex.ErrorCode == Dekaf.Protocol.ErrorCode.TopicAlreadyExists)
        {
            // Topics survive restarts when the broker has a data volume.
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
