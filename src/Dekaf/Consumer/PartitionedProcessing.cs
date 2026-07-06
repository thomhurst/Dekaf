using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
#if NETSTANDARD2_0
using TopicPartitionSet = System.Collections.Generic.IReadOnlyCollection<Dekaf.TopicPartition>;
#else
using TopicPartitionSet = System.Collections.Generic.IReadOnlySet<Dekaf.TopicPartition>;
#endif

namespace Dekaf.Consumer;

/// <summary>
/// Extension methods for high-level partitioned consumer processing.
/// </summary>
public static class PartitionedConsumerExtensions
{
    /// <summary>
    /// Runs one ordered asynchronous processor lane per assigned partition.
    /// </summary>
    public static async ValueTask RunPartitionedAsync<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        PartitionProcessor<TKey, TValue> processor,
        PartitionedProcessingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        ArgumentNullException.ThrowIfNull(processor);

        options ??= new PartitionedProcessingOptions();
        options.Validate();
        options.ValidatePartitionProcessor();

        var logger = consumer is IConsumerLoggerFactorySource loggerSource
            ? loggerSource.LoggerFactory?.CreateLogger<PartitionedConsumerRuntime<TKey, TValue>>()
            : null;
        var runtime = new PartitionedConsumerRuntime<TKey, TValue>(
            consumer,
            processor,
            options,
            logger);
        await runtime.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a record handler for assigned partitions.
    /// </summary>
    public static async ValueTask RunPartitionedAsync<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        PartitionRecordProcessor<TKey, TValue> processor,
        PartitionedProcessingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        ArgumentNullException.ThrowIfNull(processor);

        options ??= new PartitionedProcessingOptions();
        options.Validate();

        var logger = consumer is IConsumerLoggerFactorySource loggerSource
            ? loggerSource.LoggerFactory?.CreateLogger<PartitionedConsumerRuntime<TKey, TValue>>()
            : null;
        var runtime = new PartitionedConsumerRuntime<TKey, TValue>(
            consumer,
            CreateRecordProcessor(processor, options),
            options,
            logger);
        await runtime.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a batch handler for assigned partitions.
    /// </summary>
    public static async ValueTask RunPartitionedBatchesAsync<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        PartitionBatchProcessor<TKey, TValue> processor,
        PartitionedProcessingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        ArgumentNullException.ThrowIfNull(processor);

        options ??= new PartitionedProcessingOptions();
        options.Validate();

        var logger = consumer is IConsumerLoggerFactorySource loggerSource
            ? loggerSource.LoggerFactory?.CreateLogger<PartitionedConsumerRuntime<TKey, TValue>>()
            : null;
        var runtime = new PartitionedConsumerRuntime<TKey, TValue>(
            consumer,
            CreateBatchProcessor(processor, options),
            options,
            logger);
        await runtime.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    private static PartitionProcessor<TKey, TValue> CreateRecordProcessor<TKey, TValue>(
        PartitionRecordProcessor<TKey, TValue> processor,
        PartitionedProcessingOptions options)
    {
        return options.Ordering == PartitionedProcessingOrder.Key
            ? (context, cancellationToken) => RunKeyOrderedRecordsAsync(context, processor, options, cancellationToken)
            : (context, cancellationToken) => RunPartitionOrderedRecordsAsync(context, processor, cancellationToken);
    }

    private static PartitionProcessor<TKey, TValue> CreateBatchProcessor<TKey, TValue>(
        PartitionBatchProcessor<TKey, TValue> processor,
        PartitionedProcessingOptions options)
    {
        return options.Ordering == PartitionedProcessingOrder.Key
            ? (context, cancellationToken) => RunKeyOrderedBatchesAsync(context, processor, options, cancellationToken)
            : (context, cancellationToken) => RunPartitionOrderedBatchesAsync(context, processor, options, cancellationToken);
    }

    private static async ValueTask RunPartitionOrderedRecordsAsync<TKey, TValue>(
        PartitionProcessorContext<TKey, TValue> context,
        PartitionRecordProcessor<TKey, TValue> processor,
        CancellationToken cancellationToken)
    {
        var handlerContext = new PartitionRecordProcessorContext<TKey, TValue>(context);

        while (await context.WaitToReadMessageAsync(cancellationToken).ConfigureAwait(false))
        {
            while (context.TryReadMessage(out var message))
            {
                await processor(handlerContext, message, cancellationToken).ConfigureAwait(false);
                context.MarkProcessed(message);
            }
        }
    }

    private static async ValueTask RunPartitionOrderedBatchesAsync<TKey, TValue>(
        PartitionProcessorContext<TKey, TValue> context,
        PartitionBatchProcessor<TKey, TValue> processor,
        PartitionedProcessingOptions options,
        CancellationToken cancellationToken)
    {
        var handlerContext = new PartitionBatchProcessorContext<TKey, TValue>(context);
        var batch = new List<ConsumeResult<TKey, TValue>>(options.MaxHandlerBatchSize);

        while (await context.WaitToReadMessageAsync(cancellationToken).ConfigureAwait(false))
        {
            batch.Clear();
            while (batch.Count < options.MaxHandlerBatchSize && context.TryReadMessage(out var message))
                batch.Add(message);

            if (batch.Count == 0)
                continue;

            var records = batch.ToArray();
            await processor(handlerContext, records, cancellationToken).ConfigureAwait(false);

            for (var i = 0; i < records.Length; i++)
                context.MarkProcessed(records[i]);
        }
    }

    private static ValueTask RunKeyOrderedRecordsAsync<TKey, TValue>(
        PartitionProcessorContext<TKey, TValue> context,
        PartitionRecordProcessor<TKey, TValue> processor,
        PartitionedProcessingOptions options,
        CancellationToken cancellationToken)
    {
        var handlerContext = new PartitionRecordProcessorContext<TKey, TValue>(context);
        var dispatcher = new KeyOrderedPartitionDispatcher<TKey, TValue>(
            context,
            maxBatchSize: 1,
            options.MaxConcurrentHandlersPerPartition,
            options.MaxBufferedRecordsPerPartition,
            async (records, token) =>
            {
                var message = records[0];
                await processor(handlerContext, message, token).ConfigureAwait(false);
                context.MarkProcessed(message);
            });

        return dispatcher.RunAsync(cancellationToken);
    }

    private static ValueTask RunKeyOrderedBatchesAsync<TKey, TValue>(
        PartitionProcessorContext<TKey, TValue> context,
        PartitionBatchProcessor<TKey, TValue> processor,
        PartitionedProcessingOptions options,
        CancellationToken cancellationToken)
    {
        var handlerContext = new PartitionBatchProcessorContext<TKey, TValue>(context);
        var dispatcher = new KeyOrderedPartitionDispatcher<TKey, TValue>(
            context,
            options.MaxHandlerBatchSize,
            options.MaxConcurrentHandlersPerPartition,
            options.MaxBufferedRecordsPerPartition,
            async (records, token) =>
            {
                await processor(handlerContext, records, token).ConfigureAwait(false);

                for (var i = 0; i < records.Count; i++)
                    context.MarkProcessed(records[i]);
            });

        return dispatcher.RunAsync(cancellationToken);
    }
}

internal interface IConsumerLoggerFactorySource
{
    ILoggerFactory? LoggerFactory { get; }
}

/// <summary>
/// Processes all messages for one assigned partition.
/// </summary>
public delegate ValueTask PartitionProcessor<TKey, TValue>(
    PartitionProcessorContext<TKey, TValue> context,
    CancellationToken cancellationToken);

/// <summary>
/// Processes one consumed record from a partitioned runtime lane.
/// </summary>
public delegate ValueTask PartitionRecordProcessor<TKey, TValue>(
    PartitionRecordProcessorContext<TKey, TValue> context,
    ConsumeResult<TKey, TValue> message,
    CancellationToken cancellationToken);

/// <summary>
/// Processes a batch of consumed records from a partitioned runtime lane.
/// </summary>
public delegate ValueTask PartitionBatchProcessor<TKey, TValue>(
    PartitionBatchProcessorContext<TKey, TValue> context,
    IReadOnlyList<ConsumeResult<TKey, TValue>> messages,
    CancellationToken cancellationToken);

/// <summary>
/// Partition-scoped processing context.
/// </summary>
public sealed class PartitionProcessorContext<TKey, TValue>
{
    private readonly PartitionLane<TKey, TValue> _lane;

    internal PartitionProcessorContext(PartitionLane<TKey, TValue> lane)
    {
        _lane = lane;
    }

    /// <summary>
    /// Gets the partition owned by this processor.
    /// </summary>
    public TopicPartition TopicPartition => _lane.TopicPartition;

    /// <summary>
    /// Gets the ordered message stream for this partition.
    /// </summary>
    public IAsyncEnumerable<ConsumeResult<TKey, TValue>> Messages => _lane.Messages;

    /// <summary>
    /// Gets the token signaled when this partition processor should stop.
    /// </summary>
    public CancellationToken StoppingToken => _lane.StoppingToken;

    /// <summary>
    /// Marks a message as processed and eligible for commit.
    /// </summary>
    public void MarkProcessed(ConsumeResult<TKey, TValue> message)
    {
        _lane.MarkProcessed(message);
    }

    /// <summary>
    /// Commits the last offset marked as processed for this partition.
    /// </summary>
    public ValueTask CommitProcessedAsync(CancellationToken cancellationToken = default)
    {
        return _lane.CommitProcessedAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the last message offset marked as processed for this partition.
    /// </summary>
    public long? LastProcessedOffset => _lane.LastProcessedOffset;

    internal ValueTask<bool> WaitToReadMessageAsync(CancellationToken cancellationToken)
    {
        return _lane.WaitToReadMessageAsync(cancellationToken);
    }

    internal bool TryReadMessage(out ConsumeResult<TKey, TValue> message)
    {
        return _lane.TryReadMessage(out message);
    }
}

/// <summary>
/// Partition-scoped context for record handlers.
/// </summary>
public sealed class PartitionRecordProcessorContext<TKey, TValue>
{
    private readonly PartitionProcessorContext<TKey, TValue> _partition;

    internal PartitionRecordProcessorContext(PartitionProcessorContext<TKey, TValue> partition)
    {
        _partition = partition;
    }

    /// <summary>
    /// Gets the partition owned by this handler.
    /// </summary>
    public TopicPartition TopicPartition => _partition.TopicPartition;

    /// <summary>
    /// Gets the token signaled when this partition handler should stop.
    /// </summary>
    public CancellationToken StoppingToken => _partition.StoppingToken;

    /// <summary>
    /// Commits the last contiguous offset completed for this partition.
    /// </summary>
    public ValueTask CommitProcessedAsync(CancellationToken cancellationToken = default)
    {
        return _partition.CommitProcessedAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the highest message offset marked as processed for this partition.
    /// </summary>
    public long? LastProcessedOffset => _partition.LastProcessedOffset;
}

/// <summary>
/// Partition-scoped context for batch handlers.
/// </summary>
public sealed class PartitionBatchProcessorContext<TKey, TValue>
{
    private readonly PartitionProcessorContext<TKey, TValue> _partition;

    internal PartitionBatchProcessorContext(PartitionProcessorContext<TKey, TValue> partition)
    {
        _partition = partition;
    }

    /// <summary>
    /// Gets the partition owned by this handler.
    /// </summary>
    public TopicPartition TopicPartition => _partition.TopicPartition;

    /// <summary>
    /// Gets the token signaled when this partition handler should stop.
    /// </summary>
    public CancellationToken StoppingToken => _partition.StoppingToken;

    /// <summary>
    /// Commits the last contiguous offset completed for this partition.
    /// </summary>
    public ValueTask CommitProcessedAsync(CancellationToken cancellationToken = default)
    {
        return _partition.CommitProcessedAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the highest message offset marked as processed for this partition.
    /// </summary>
    public long? LastProcessedOffset => _partition.LastProcessedOffset;
}

/// <summary>
/// Options for partitioned consumer processing.
/// </summary>
public sealed class PartitionedProcessingOptions
{
    /// <summary>
    /// Gets the maximum queued records per partition lane.
    /// </summary>
    public int MaxBufferedRecordsPerPartition { get; init; } = 256;

    /// <summary>
    /// Gets the ordering scope used by record and batch handler overloads.
    /// </summary>
    public PartitionedProcessingOrder Ordering { get; init; } = PartitionedProcessingOrder.Partition;

    /// <summary>
    /// Gets the maximum concurrent record or batch handlers per partition when <see cref="Ordering"/> is <see cref="PartitionedProcessingOrder.Key"/>.
    /// </summary>
    public int MaxConcurrentHandlersPerPartition { get; init; } = 1;

    /// <summary>
    /// Gets the maximum number of records passed to a batch handler invocation.
    /// </summary>
    public int MaxHandlerBatchSize { get; init; } = 1;

    /// <summary>
    /// Gets how the runtime applies backpressure when a partition lane is full.
    /// </summary>
    public PartitionBackpressureMode BackpressureMode { get; init; } = PartitionBackpressureMode.PauseResume;

    /// <summary>
    /// Gets how partition processors stop during revoke, lost assignment, or shutdown.
    /// </summary>
    public PartitionStopPolicy StopPolicy { get; init; } = PartitionStopPolicy.Drain;

    /// <summary>
    /// Gets the maximum time to wait for a partition processor to stop.
    /// </summary>
    public TimeSpan StopTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets how processor failures affect the partitioned run.
    /// </summary>
    public PartitionWorkerErrorPolicy ErrorPolicy { get; init; } = PartitionWorkerErrorPolicy.StopConsumer;

    /// <summary>
    /// Gets the first delay before restarting a failed lane when <see cref="ErrorPolicy"/> is <see cref="PartitionWorkerErrorPolicy.Ignore"/>.
    /// The value must be greater than <see cref="TimeSpan.Zero"/>.
    /// </summary>
    public TimeSpan IgnoreRestartBackoff { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets the maximum exponential restart delay for failed lanes when <see cref="ErrorPolicy"/> is <see cref="PartitionWorkerErrorPolicy.Ignore"/>.
    /// </summary>
    public TimeSpan IgnoreRestartBackoffMax { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets how completed partition offsets are committed.
    /// </summary>
    public PartitionCommitPolicy CommitPolicy { get; init; } = PartitionCommitPolicy.CommitCompletedOnRevoke;

    /// <summary>
    /// Gets the interval for periodic completed-offset commits.
    /// </summary>
    public TimeSpan CommitInterval { get; init; } = TimeSpan.FromSeconds(5);

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxBufferedRecordsPerPartition, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxConcurrentHandlersPerPartition, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxHandlerBatchSize, 1);

        if (StopTimeout < TimeSpan.Zero && StopTimeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(StopTimeout));

        if (IgnoreRestartBackoff <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(IgnoreRestartBackoff));

        if (IgnoreRestartBackoffMax <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(IgnoreRestartBackoffMax));

        if (IgnoreRestartBackoffMax < IgnoreRestartBackoff)
            throw new ArgumentOutOfRangeException(nameof(IgnoreRestartBackoffMax));

        if (CommitInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(CommitInterval));
    }

    internal void ValidatePartitionProcessor()
    {
        if (Ordering != PartitionedProcessingOrder.Partition)
            throw new InvalidOperationException("Key-ordered partitioned processing requires a record or batch handler overload.");
    }
}

/// <summary>
/// Ordering behavior for partitioned record and batch handlers.
/// </summary>
public enum PartitionedProcessingOrder
{
    /// <summary>
    /// Process each partition in Kafka offset order with one active handler at a time.
    /// </summary>
    Partition,

    /// <summary>
    /// Process different keys from one partition concurrently while preserving order for each key.
    /// </summary>
    Key
}

/// <summary>
/// Backpressure behavior for a full partition lane.
/// </summary>
public enum PartitionBackpressureMode
{
    /// <summary>
    /// Pause the full partition and resume it when queue capacity returns.
    /// </summary>
    PauseResume,

    /// <summary>
    /// Await queue capacity without changing the consumer pause state.
    /// </summary>
    AwaitCapacity
}

/// <summary>
/// Stop behavior for partition processors.
/// </summary>
public enum PartitionStopPolicy
{
    /// <summary>
    /// Complete the message stream and let the processor drain queued records.
    /// </summary>
    Drain,

    /// <summary>
    /// Cancel the processor immediately.
    /// </summary>
    Cancel
}

/// <summary>
/// Failure behavior for partition processors.
/// </summary>
public enum PartitionWorkerErrorPolicy
{
    /// <summary>
    /// Stop the whole partitioned run and propagate the exception.
    /// </summary>
    StopConsumer,

    /// <summary>
    /// Stop the failed partition and pause it.
    /// </summary>
    StopPartition,

    /// <summary>
    /// Restart the failed partition lane and keep the rest of the run active.
    /// </summary>
    Ignore
}

/// <summary>
/// Offset commit behavior for completed partition records.
/// </summary>
public enum PartitionCommitPolicy
{
    /// <summary>
    /// The application owns all offset commits.
    /// </summary>
    UserManaged,

    /// <summary>
    /// Commit completed offsets when partitions stop or are revoked.
    /// </summary>
    CommitCompletedOnRevoke,

    /// <summary>
    /// Commit completed offsets periodically and when partitions stop or are revoked.
    /// </summary>
    CommitCompletedPeriodically
}

internal enum PartitionStopReason
{
    Revoke,
    Lost,
    Shutdown,
    Failure
}

internal sealed class PartitionedConsumerRuntime<TKey, TValue>
{
    private static readonly TimeSpan ConsumePollTimeout = TimeSpan.FromMilliseconds(100);

    private readonly IKafkaConsumer<TKey, TValue> _consumer;
    private readonly PartitionProcessor<TKey, TValue> _processor;
    private readonly PartitionedProcessingOptions _options;
    private readonly ILogger _logger;
    private readonly Dictionary<TopicPartition, PartitionLane<TKey, TValue>> _lanes = [];
    private readonly HashSet<TopicPartition> _pausedByRuntime = [];
    private readonly HashSet<TopicPartition> _stoppedByFailure = [];
    private readonly HashSet<TopicPartition> _pendingIgnoreRestarts = [];
    private readonly Dictionary<TopicPartition, int> _ignoreRestartFailures = [];
    private readonly ConcurrentDictionary<Task, byte> _ignoreRestartTasks = [];
    private readonly List<TopicPartition> _partitionsToStop = [];
    private readonly Channel<RuntimeCommand<TKey, TValue>> _commands;
    private readonly CancellationTokenSource _failureCancellation = new();
    private readonly CancellationTokenSource _restartCancellation = new();
    private readonly object _failureGate = new();
    private ExceptionDispatchInfo? _failure;
    private DateTimeOffset _nextPeriodicCommit;

    public PartitionedConsumerRuntime(
        IKafkaConsumer<TKey, TValue> consumer,
        PartitionProcessor<TKey, TValue> processor,
        PartitionedProcessingOptions options,
        ILogger? logger)
    {
        _consumer = consumer;
        _processor = processor;
        _options = options;
        _logger = logger ?? NullLogger.Instance;
        _commands = Channel.CreateUnbounded<RuntimeCommand<TKey, TValue>>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = false
        });
        _nextPeriodicCommit = DateTimeOffset.UtcNow.Add(_options.CommitInterval);
    }

    public async ValueTask RunAsync(CancellationToken cancellationToken)
    {
        using var rebalanceRegistration = RegisterRebalanceListener();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _failureCancellation.Token);

        try
        {
            await SyncAssignmentAsync(linkedCancellation.Token).ConfigureAwait(false);
            using var consumeCancellation = CancellationTokenSource.CreateLinkedTokenSource(linkedCancellation.Token);
            var batchEnumerator = _consumer
                .ConsumeBatchAsync(consumeCancellation.Token)
                .GetAsyncEnumerator(consumeCancellation.Token);
            Task<bool>? pendingBatchMove = null;
            Task<bool>? pendingCommandWait = null;

            try
            {
                while (true)
                {
                    linkedCancellation.Token.ThrowIfCancellationRequested();

                    await DrainCommandsAsync(linkedCancellation.Token).ConfigureAwait(false);
                    ThrowIfFailed();
                    await CommitPeriodicallyAsync(linkedCancellation.Token).ConfigureAwait(false);

                    if (pendingBatchMove is null)
                    {
                        var moveNext = batchEnumerator.MoveNextAsync();
                        if (moveNext.IsCompleted)
                        {
                            if (!await moveNext.ConfigureAwait(false))
                                break;

                            await DrainCommandsAsync(linkedCancellation.Token).ConfigureAwait(false);
                            ThrowIfFailed();
                            await RouteBatchAsync(batchEnumerator.Current, linkedCancellation.Token).ConfigureAwait(false);
                            continue;
                        }

                        pendingBatchMove = moveNext.AsTask();
                    }

                    if (pendingBatchMove.IsCompleted)
                    {
                        if (!await pendingBatchMove.ConfigureAwait(false))
                            break;

                        pendingBatchMove = null;
                        await DrainCommandsAsync(linkedCancellation.Token).ConfigureAwait(false);
                        ThrowIfFailed();
                        await RouteBatchAsync(batchEnumerator.Current, linkedCancellation.Token).ConfigureAwait(false);
                        continue;
                    }

                    if (pendingCommandWait is null)
                    {
                        var waitToRead = _commands.Reader.WaitToReadAsync(linkedCancellation.Token);
                        if (waitToRead.IsCompleted)
                        {
                            if (!await waitToRead.ConfigureAwait(false))
                                break;

                            continue;
                        }

                        pendingCommandWait = waitToRead.AsTask();
                    }

                    if (pendingCommandWait.IsCompleted)
                    {
                        if (!await pendingCommandWait.ConfigureAwait(false))
                            break;

                        pendingCommandWait = null;
                        continue;
                    }

                    var delay = Task.Delay(ConsumePollTimeout, linkedCancellation.Token);
                    var completed = await Task.WhenAny(
                        pendingBatchMove,
                        pendingCommandWait,
                        delay).ConfigureAwait(false);

                    if (completed == pendingBatchMove || completed == pendingCommandWait)
                        continue;

                    await SyncAssignmentAsync(linkedCancellation.Token).ConfigureAwait(false);
                }
            }
            finally
            {
                consumeCancellation.Cancel();

                await ObserveAbandonedTaskAsync(pendingBatchMove).ConfigureAwait(false);
                if (pendingCommandWait is { IsCompleted: true })
                    await ObserveAbandonedTaskAsync(pendingCommandWait).ConfigureAwait(false);

                await batchEnumerator.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_failure is not null)
        {
        }
        finally
        {
            try
            {
                await StopAllBoundedAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_failure is not null)
            {
            }
            finally
            {
                await StopScheduledRestartsAsync().ConfigureAwait(false);
                _commands.Writer.TryComplete();
                CompletePendingCommands();
            }
        }

        ThrowIfFailed();
    }

    private static async ValueTask ObserveAbandonedTaskAsync(Task<bool>? task)
    {
        if (task is null)
            return;

        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // Cleanup only observes abandoned work; preserve the exception already leaving RunAsync.
        }
    }

    private IDisposable? RegisterRebalanceListener()
    {
        return _consumer is IConsumerRebalanceEventSource eventSource
            ? eventSource.RegisterRuntimeRebalanceListener(new RuntimeRebalanceListener(this))
            : null;
    }

    private void QueueAssignedPartitions(IEnumerable<TopicPartition> partitions)
    {
        var partitionArray = partitions as TopicPartition[] ?? partitions.ToArray();
        if (partitionArray.Length > 0)
            _commands.Writer.TryWrite(RuntimeCommand<TKey, TValue>.AssignPartitions(partitionArray));
    }

    private void QueueStoppedPartitions(
        IEnumerable<TopicPartition> partitions,
        PartitionStopReason stopReason)
    {
        var partitionArray = partitions as TopicPartition[] ?? partitions.ToArray();
        if (partitionArray.Length > 0)
            _commands.Writer.TryWrite(RuntimeCommand<TKey, TValue>.StopPartitions(partitionArray, stopReason));
    }

    private sealed class RuntimeRebalanceListener(
        PartitionedConsumerRuntime<TKey, TValue> runtime) : IRebalanceListener
    {
        public ValueTask OnPartitionsAssignedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            runtime.QueueAssignedPartitions(partitions);
            return default;
        }

        public ValueTask OnPartitionsRevokedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            runtime.QueueStoppedPartitions(partitions, PartitionStopReason.Revoke);
            return default;
        }

        public ValueTask OnPartitionsLostAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            runtime.QueueStoppedPartitions(partitions, PartitionStopReason.Lost);
            return default;
        }
    }

    public ValueTask CommitProcessedAsync(
        PartitionLane<TKey, TValue> lane,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return new ValueTask(Task.FromCanceled(cancellationToken));

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = RuntimeCommand<TKey, TValue>.Commit(lane, completion, cancellationToken);

        if (!_commands.Writer.TryWrite(command))
        {
            var exception = new InvalidOperationException("Partitioned processing runtime is not accepting commit requests.");
            return new ValueTask(Task.FromException(exception));
        }

        return new ValueTask(completion.Task.WaitAsync(cancellationToken));
    }

    private async ValueTask SyncAssignmentAsync(CancellationToken cancellationToken)
    {
        var assignment = _consumer.Partitions.Assignment;

        RemoveUnassigned(_stoppedByFailure, assignment, removePause: true);
        RemoveUnassigned(_pendingIgnoreRestarts, assignment, removePause: false);

        _partitionsToStop.Clear();
        foreach (var partition in _lanes.Keys)
        {
            if (!assignment.Contains(partition))
                _partitionsToStop.Add(partition);
        }

        for (var i = 0; i < _partitionsToStop.Count; i++)
        {
            await StopPartitionAsync(
                _partitionsToStop[i],
                PartitionStopReason.Revoke,
                cancellationToken).ConfigureAwait(false);
        }
        _partitionsToStop.Clear();

        StartAssignedPartitions(assignment);
    }

    private void RemoveUnassigned(
        HashSet<TopicPartition> partitions,
        TopicPartitionSet assignment,
        bool removePause)
    {
        _partitionsToStop.Clear();
        foreach (var partition in partitions)
        {
            if (!assignment.Contains(partition))
                _partitionsToStop.Add(partition);
        }

        for (var i = 0; i < _partitionsToStop.Count; i++)
        {
            var partition = _partitionsToStop[i];
            partitions.Remove(partition);
            if (removePause)
                _pausedByRuntime.Remove(partition);
            _ignoreRestartFailures.Remove(partition);
        }
        _partitionsToStop.Clear();
    }

    private async ValueTask RouteBatchAsync(
        ConsumeBatch<TKey, TValue> batch,
        CancellationToken cancellationToken)
    {
        var partition = batch.TopicPartition;
        if (!_lanes.TryGetValue(partition, out var lane))
            return;

        foreach (var result in batch)
        {
            while (!lane.TryEnqueue(result))
            {
                PauseIfNeeded(lane);
                var canWrite = await lane.WaitToWriteAsync(cancellationToken).ConfigureAwait(false);
                await DrainCommandsAsync(cancellationToken).ConfigureAwait(false);
                ThrowIfFailed();

                if (!canWrite || !_lanes.TryGetValue(partition, out lane))
                    return;
            }

            PauseIfNeeded(lane);
        }
    }

    private void StartLane(TopicPartition partition)
    {
        var lane = new PartitionLane<TKey, TValue>(
            partition,
            _options.MaxBufferedRecordsPerPartition,
            CommitProcessedAsync,
            OnLaneCapacityAvailable,
            OnLaneFailed);

        _lanes.Add(partition, lane);
        lane.Start(_processor);
    }

    private void StartAssignedPartitions(IEnumerable<TopicPartition> partitions)
    {
        foreach (var partition in partitions)
        {
            if (_lanes.ContainsKey(partition) || _stoppedByFailure.Contains(partition))
                continue;

            if (_pendingIgnoreRestarts.Contains(partition))
                continue;

            StartLane(partition);
        }
    }

    private void PauseIfNeeded(PartitionLane<TKey, TValue> lane)
    {
        if (_options.BackpressureMode != PartitionBackpressureMode.PauseResume || !lane.IsFull)
            return;

        if (_pausedByRuntime.Add(lane.TopicPartition))
        {
            lane.MarkRuntimeBackpressurePaused();
            _consumer.Partitions.Pause(lane.TopicPartition);
        }
    }

    private void ResumeIfNeeded(PartitionLane<TKey, TValue> lane)
    {
        if (_options.BackpressureMode != PartitionBackpressureMode.PauseResume || !lane.HasCapacity)
            return;

        if (_pausedByRuntime.Remove(lane.TopicPartition))
        {
            lane.ClearRuntimeBackpressurePaused();
            _consumer.Partitions.Resume(lane.TopicPartition);
        }
    }

    private void OnLaneCapacityAvailable(PartitionLane<TKey, TValue> lane)
    {
        if (_options.BackpressureMode != PartitionBackpressureMode.PauseResume || !lane.IsRuntimeBackpressurePaused)
            return;

        _commands.Writer.TryWrite(RuntimeCommand<TKey, TValue>.Resume(lane));
    }

    private void OnLaneFailed(PartitionLane<TKey, TValue> lane, Exception exception)
    {
        if (_options.ErrorPolicy == PartitionWorkerErrorPolicy.StopConsumer)
        {
            LogPartitionProcessorFailureStoppingConsumer(
                lane.TopicPartition,
                exception);
            CaptureFailure(exception);
            _failureCancellation.Cancel();
            return;
        }

        _commands.Writer.TryWrite(RuntimeCommand<TKey, TValue>.StopFailed(lane, exception));
    }

    private async ValueTask DrainCommandsAsync(CancellationToken cancellationToken)
    {
        while (_commands.Reader.TryRead(out var command))
        {
            switch (command.Kind)
            {
                case RuntimeCommandKind.Resume:
                    if (_lanes.TryGetValue(command.Lane!.TopicPartition, out var lane))
                        ResumeIfNeeded(lane);

                    break;

                case RuntimeCommandKind.Commit:
                    await CompleteCommitCommandAsync(command, cancellationToken).ConfigureAwait(false);
                    break;

                case RuntimeCommandKind.StopFailed:
                    await StopFailedLaneAsync(command.Lane!, command.Exception!, cancellationToken).ConfigureAwait(false);
                    break;

                case RuntimeCommandKind.RestartLane:
                    RestartLaneIfStillAssigned(command.Partition);
                    break;

                case RuntimeCommandKind.AssignPartitions:
                    StartAssignedPartitions(command.Partitions!);
                    break;

                case RuntimeCommandKind.StopPartitions:
                    await StopPartitionsAsync(
                        command.Partitions!,
                        command.StopReason,
                        cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
    }

    private async ValueTask CompleteCommitCommandAsync(
        RuntimeCommand<TKey, TValue> command,
        CancellationToken cancellationToken)
    {
        try
        {
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                command.CancellationToken);

            await CommitLaneAsync(command.Lane!, linkedCancellation.Token).ConfigureAwait(false);
            command.Completion!.TrySetResult();
        }
        catch (OperationCanceledException ex)
        {
            command.Completion!.TrySetCanceled(ex.CancellationToken);
        }
        catch (Exception ex)
        {
            command.Completion!.TrySetException(ex);
        }
    }

    private async ValueTask StopFailedLaneAsync(
        PartitionLane<TKey, TValue> lane,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!_lanes.Remove(lane.TopicPartition))
            return;

        if (_options.ErrorPolicy == PartitionWorkerErrorPolicy.StopPartition)
        {
            _stoppedByFailure.Add(lane.TopicPartition);
            if (_pausedByRuntime.Add(lane.TopicPartition))
                _consumer.Partitions.Pause(lane.TopicPartition);

            LogPartitionProcessorFailureStoppingPartition(
                lane.TopicPartition,
                exception);
        }
        else if (_pausedByRuntime.Remove(lane.TopicPartition))
        {
            lane.ClearRuntimeBackpressurePaused();
            _consumer.Partitions.Resume(lane.TopicPartition);
        }

        await StopLaneAsync(
            lane,
            PartitionStopReason.Failure,
            commitProcessed: false,
            cancellationToken).ConfigureAwait(false);

        if (_options.ErrorPolicy == PartitionWorkerErrorPolicy.Ignore
            && !_failureCancellation.IsCancellationRequested
            && _consumer.Partitions.Assignment.Contains(lane.TopicPartition)
            && !_lanes.ContainsKey(lane.TopicPartition))
        {
            if (lane.LastProcessedOffset.HasValue)
                _ignoreRestartFailures.Remove(lane.TopicPartition);

            var restartAttempt = RecordIgnoreRestartFailure(lane.TopicPartition);
            var restartDelay = GetIgnoreRestartDelay(restartAttempt);
            LogPartitionProcessorFailureIgnored(
                lane.TopicPartition,
                restartAttempt,
                restartDelay,
                exception);

            _pendingIgnoreRestarts.Add(lane.TopicPartition);
            ScheduleIgnoreRestart(lane.TopicPartition, restartDelay);
        }
    }

    private void RestartLaneIfStillAssigned(TopicPartition partition)
    {
        _pendingIgnoreRestarts.Remove(partition);

        if (_failureCancellation.IsCancellationRequested
            || !_consumer.Partitions.Assignment.Contains(partition)
            || _stoppedByFailure.Contains(partition)
            || _lanes.ContainsKey(partition))
        {
            return;
        }

        StartLane(partition);
    }

    private void ScheduleIgnoreRestart(TopicPartition partition, TimeSpan delay)
    {
        var task = ScheduleIgnoreRestartAsync(partition, delay);
        _ignoreRestartTasks.TryAdd(task, 0);

        _ = task.ContinueWith(
            static (completedTask, state) =>
            {
                var runtime = (PartitionedConsumerRuntime<TKey, TValue>)state!;
                runtime.CompleteIgnoreRestartTask(completedTask);
            },
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task ScheduleIgnoreRestartAsync(
        TopicPartition partition,
        TimeSpan delay)
    {
        await Task.Delay(delay, _restartCancellation.Token).ConfigureAwait(false);
        _commands.Writer.TryWrite(RuntimeCommand<TKey, TValue>.RestartLane(partition));
    }

    private void CompleteIgnoreRestartTask(Task task)
    {
        _ignoreRestartTasks.TryRemove(task, out _);

        if (!task.IsFaulted || task.Exception is null)
            return;

        var exception = task.Exception.InnerExceptions.Count == 1
            ? task.Exception.InnerException!
            : task.Exception;
        CaptureFailure(exception);
        _failureCancellation.Cancel();
    }

    private async ValueTask StopLaneAsync(
        PartitionLane<TKey, TValue> lane,
        PartitionStopReason reason,
        bool commitProcessed,
        CancellationToken cancellationToken)
    {
        var exception = await lane.StopAsync(
            reason is PartitionStopReason.Failure or PartitionStopReason.Lost
                ? PartitionStopPolicy.Cancel
                : _options.StopPolicy,
            _options.StopTimeout).ConfigureAwait(false);

        if (commitProcessed)
            await CommitLaneAsync(lane, cancellationToken).ConfigureAwait(false);

        if (exception is not null
            && (_options.ErrorPolicy == PartitionWorkerErrorPolicy.StopConsumer || exception is TimeoutException))
        {
            CaptureFailure(exception);
            _failureCancellation.Cancel();
        }
    }

    private async ValueTask StopPartitionsAsync(
        TopicPartition[] partitions,
        PartitionStopReason reason,
        CancellationToken cancellationToken)
    {
        foreach (var partition in partitions)
        {
            _stoppedByFailure.Remove(partition);
            _pendingIgnoreRestarts.Remove(partition);
            _ignoreRestartFailures.Remove(partition);
            await StopPartitionAsync(partition, reason, cancellationToken).ConfigureAwait(false);
        }

        StartAssignedPartitions(_consumer.Partitions.Assignment);
    }

    private async ValueTask StopPartitionAsync(
        TopicPartition partition,
        PartitionStopReason reason,
        CancellationToken cancellationToken)
    {
        if (!_lanes.TryGetValue(partition, out var lane))
        {
            _pausedByRuntime.Remove(partition);
            _pendingIgnoreRestarts.Remove(partition);
            _ignoreRestartFailures.Remove(partition);
            return;
        }

        _lanes.Remove(partition);
        _pausedByRuntime.Remove(partition);
        lane.ClearRuntimeBackpressurePaused();
        _pendingIgnoreRestarts.Remove(partition);
        _ignoreRestartFailures.Remove(partition);

        await StopLaneAsync(
            lane,
            reason,
            commitProcessed: reason == PartitionStopReason.Revoke && ShouldCommitOnPartitionStop(),
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask StopAllAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var lane in _lanes.Values.ToArray())
            {
                _lanes.Remove(lane.TopicPartition);
                await StopLaneAsync(
                    lane,
                    PartitionStopReason.Shutdown,
                    commitProcessed: ShouldCommitOnPartitionStop(),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (_pausedByRuntime.Count > 0)
            {
                _consumer.Partitions.Resume(_pausedByRuntime.ToArray());
                _pausedByRuntime.Clear();
            }

            _pendingIgnoreRestarts.Clear();
        }
    }

    private async ValueTask StopAllBoundedAsync()
    {
        if (_options.StopTimeout == Timeout.InfiniteTimeSpan)
        {
            await StopAllAsync(CancellationToken.None).ConfigureAwait(false);
            return;
        }

        using var stopCancellation = new CancellationTokenSource(_options.StopTimeout);
        await StopAllAsync(stopCancellation.Token).ConfigureAwait(false);
    }

    private async ValueTask StopScheduledRestartsAsync()
    {
        await _restartCancellation.CancelAsync().ConfigureAwait(false);

        var tasks = _ignoreRestartTasks.Keys.ToArray();

        if (tasks.Length == 0)
            return;

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_restartCancellation.IsCancellationRequested)
        {
        }
    }

    private async ValueTask CommitPeriodicallyAsync(CancellationToken cancellationToken)
    {
        if (_options.CommitPolicy != PartitionCommitPolicy.CommitCompletedPeriodically)
            return;

        var now = DateTimeOffset.UtcNow;
        if (now < _nextPeriodicCommit)
            return;

        _nextPeriodicCommit = now.Add(_options.CommitInterval);

        var offsets = _lanes.Values
            .Select(static lane => lane.GetCommitOffset())
            .OfType<TopicPartitionOffset>()
            .ToArray();

        if (offsets.Length > 0)
            await _consumer.CommitAsync(offsets, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask CommitLaneAsync(
        PartitionLane<TKey, TValue> lane,
        CancellationToken cancellationToken)
    {
        var offset = lane.GetCommitOffset();
        if (offset.HasValue)
            await _consumer.CommitAsync([offset.Value], cancellationToken).ConfigureAwait(false);
    }

    private bool ShouldCommitOnPartitionStop()
    {
        return _options.CommitPolicy is PartitionCommitPolicy.CommitCompletedOnRevoke
            or PartitionCommitPolicy.CommitCompletedPeriodically;
    }

    private int RecordIgnoreRestartFailure(TopicPartition partition)
    {
        var nextAttempt = _ignoreRestartFailures.TryGetValue(partition, out var current)
            && current < int.MaxValue
                ? current + 1
                : 1;

        _ignoreRestartFailures[partition] = nextAttempt;
        return nextAttempt;
    }

    private TimeSpan GetIgnoreRestartDelay(int attempt)
    {
        var delay = _options.IgnoreRestartBackoff;
        var max = _options.IgnoreRestartBackoffMax;

        for (var i = 1; i < attempt && delay < max; i++)
        {
            var doubledTicks = delay.Ticks > max.Ticks / 2
                ? max.Ticks
                : delay.Ticks * 2;
            delay = TimeSpan.FromTicks(doubledTicks);
        }

        return delay;
    }

    private void ThrowIfFailed()
    {
        _failure?.Throw();
    }

    private void CaptureFailure(Exception exception)
    {
        lock (_failureGate)
            _failure ??= ExceptionDispatchInfo.Capture(exception);
    }

    private void CompletePendingCommands()
    {
        while (_commands.Reader.TryRead(out var command))
        {
            if (command.Kind == RuntimeCommandKind.Commit)
                command.Completion!.TrySetCanceled();
        }
    }

    private void LogPartitionProcessorFailureStoppingConsumer(
        TopicPartition partition,
        Exception exception)
    {
        _logger.LogError(
            exception,
            "Partition processor for {Topic}-{Partition} failed; stopping partitioned consumer.",
            partition.Topic,
            partition.Partition);
    }

    private void LogPartitionProcessorFailureStoppingPartition(
        TopicPartition partition,
        Exception exception)
    {
        _logger.LogWarning(
            exception,
            "Partition processor for {Topic}-{Partition} failed under StopPartition; pausing partition.",
            partition.Topic,
            partition.Partition);
    }

    private void LogPartitionProcessorFailureIgnored(
        TopicPartition partition,
        int restartAttempt,
        TimeSpan restartDelay,
        Exception exception)
    {
        _logger.LogWarning(
            exception,
            "Partition processor for {Topic}-{Partition} failed under Ignore; restarting after {RestartDelayMs}ms (attempt {RestartAttempt}).",
            partition.Topic,
            partition.Partition,
            restartDelay.TotalMilliseconds,
            restartAttempt);
    }
}

internal sealed class PartitionLane<TKey, TValue>
{
    private readonly Channel<ConsumeResult<TKey, TValue>> _channel;
    private readonly Func<PartitionLane<TKey, TValue>, CancellationToken, ValueTask> _commitProcessed;
    private readonly Action<PartitionLane<TKey, TValue>> _capacityAvailable;
    private readonly Action<PartitionLane<TKey, TValue>, Exception> _failed;
    private readonly CancellationTokenSource _stopping = new();
    private readonly PartitionProcessorContext<TKey, TValue> _context;
    private readonly object _offsetGate = new();
    private readonly int _capacity;
    private Task? _processorTask;
    private int _bufferedCount;
    private int _runtimeBackpressurePaused;
    private int _completed;
    private readonly SortedSet<long> _completedOffsets = [];
    private readonly Dictionary<long, int> _leaderEpochs = [];
    private long? _nextOffsetToCommit;
    private long? _completedOffset;
    private long? _lastProcessedOffset;
    private int _lastCommittedLeaderEpoch = -1;

    public PartitionLane(
        TopicPartition topicPartition,
        int capacity,
        Func<PartitionLane<TKey, TValue>, CancellationToken, ValueTask> commitProcessed,
        Action<PartitionLane<TKey, TValue>> capacityAvailable,
        Action<PartitionLane<TKey, TValue>, Exception> failed)
    {
        TopicPartition = topicPartition;
        _capacity = capacity;
        _commitProcessed = commitProcessed;
        _capacityAvailable = capacityAvailable;
        _failed = failed;
        _context = new PartitionProcessorContext<TKey, TValue>(this);
        _channel = Channel.CreateBounded<ConsumeResult<TKey, TValue>>(new BoundedChannelOptions(capacity)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });
    }

    public TopicPartition TopicPartition { get; }

    public CancellationToken StoppingToken => _stopping.Token;

    public IAsyncEnumerable<ConsumeResult<TKey, TValue>> Messages => ReadMessagesAsync(_stopping.Token);

    public long? LastProcessedOffset
    {
        get
        {
            lock (_offsetGate)
                return _lastProcessedOffset;
        }
    }

    public bool IsFull => Volatile.Read(ref _bufferedCount) >= _capacity;

    public bool HasCapacity => Volatile.Read(ref _bufferedCount) < _capacity;

    public bool IsRuntimeBackpressurePaused => Volatile.Read(ref _runtimeBackpressurePaused) != 0;

    public void MarkRuntimeBackpressurePaused()
    {
        Volatile.Write(ref _runtimeBackpressurePaused, 1);
    }

    public void ClearRuntimeBackpressurePaused()
    {
        Volatile.Write(ref _runtimeBackpressurePaused, 0);
    }

    public void Start(PartitionProcessor<TKey, TValue> processor)
    {
        _processorTask = Task.Run(async () =>
        {
            try
            {
                await processor(_context, _stopping.Token).ConfigureAwait(false);
                if (Volatile.Read(ref _completed) == 0)
                {
                    _failed(
                        this,
                        new InvalidOperationException(
                            $"Partition processor for {TopicPartition.Topic}:{TopicPartition.Partition} completed before the partition stopped."));
                }
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _failed(this, ex);
                throw;
            }
        });
    }

    public bool TryEnqueue(ConsumeResult<TKey, TValue> result)
    {
        if (Volatile.Read(ref _completed) != 0)
            return false;

        TrackPending(result);
        Interlocked.Increment(ref _bufferedCount);
        if (_channel.Writer.TryWrite(result))
            return true;

        Interlocked.Decrement(ref _bufferedCount);
        return false;
    }

    public ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken)
    {
        return _channel.Writer.WaitToWriteAsync(cancellationToken);
    }

    public ValueTask<bool> WaitToReadMessageAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.WaitToReadAsync(cancellationToken);
    }

    public bool TryReadMessage(out ConsumeResult<TKey, TValue> message)
    {
        if (!_channel.Reader.TryRead(out message))
            return false;

        var bufferedCount = Interlocked.Decrement(ref _bufferedCount);
        if (bufferedCount == _capacity - 1)
            _capacityAvailable(this);

        return true;
    }

    public void MarkProcessed(ConsumeResult<TKey, TValue> message)
    {
        var partition = new TopicPartition(message.Topic, message.Partition);
        if (partition != TopicPartition)
            throw new InvalidOperationException("Cannot mark a message from another partition as processed.");

        if (message.IsPartitionEof)
            return;

        lock (_offsetGate)
        {
            if (!_nextOffsetToCommit.HasValue)
                _nextOffsetToCommit = message.Offset;

            if (!_lastProcessedOffset.HasValue || message.Offset > _lastProcessedOffset.Value)
                _lastProcessedOffset = message.Offset;

            if (message.Offset < _nextOffsetToCommit.Value)
                return;

            _leaderEpochs[message.Offset] = message.LeaderEpoch ?? -1;
            _completedOffsets.Add(message.Offset);

            AdvanceCompletedOffset();
        }
    }

    private void TrackPending(ConsumeResult<TKey, TValue> message)
    {
        if (message.IsPartitionEof)
            return;

        lock (_offsetGate)
        {
            if (!_nextOffsetToCommit.HasValue)
                _nextOffsetToCommit = message.Offset;
        }
    }

    private void AdvanceCompletedOffset()
    {
        while (_nextOffsetToCommit.HasValue && _completedOffsets.Remove(_nextOffsetToCommit.Value))
        {
            var completedOffset = _nextOffsetToCommit.Value;

            if (_leaderEpochs.TryGetValue(completedOffset, out var leaderEpoch))
            {
                _lastCommittedLeaderEpoch = leaderEpoch;
                _leaderEpochs.Remove(completedOffset);
            }
            else
            {
                _lastCommittedLeaderEpoch = -1;
            }

            _completedOffset = completedOffset + 1;
            _nextOffsetToCommit = _completedOffset;
        }
    }

    public TopicPartitionOffset? GetCommitOffset()
    {
        lock (_offsetGate)
        {
            return _completedOffset.HasValue
                ? new TopicPartitionOffset(
                    TopicPartition.Topic,
                    TopicPartition.Partition,
                    _completedOffset.Value,
                    _lastCommittedLeaderEpoch)
                : null;
        }
    }

    public ValueTask CommitProcessedAsync(CancellationToken cancellationToken)
    {
        return _commitProcessed(this, cancellationToken);
    }

    public async ValueTask<Exception?> StopAsync(
        PartitionStopPolicy stopPolicy,
        TimeSpan timeout)
    {
        CompleteWriter();

        if (stopPolicy == PartitionStopPolicy.Cancel)
            await CancelAsync().ConfigureAwait(false);

        if (_processorTask is null)
            return null;

        try
        {
            if (timeout == Timeout.InfiniteTimeSpan)
            {
                await _processorTask.ConfigureAwait(false);
            }
            else
            {
                await _processorTask.WaitAsync(timeout).ConfigureAwait(false);
            }

            return null;
        }
        catch (TimeoutException)
        {
            await CancelAsync().ConfigureAwait(false);
            _ = _processorTask.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            return new TimeoutException(
                $"Partition processor for {TopicPartition.Topic}:{TopicPartition.Partition} did not stop within {timeout}.");
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private async IAsyncEnumerable<ConsumeResult<TKey, TValue>> ReadMessagesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await WaitToReadMessageAsync(cancellationToken).ConfigureAwait(false))
        {
            while (TryReadMessage(out var item))
                yield return item;
        }
    }

    private void CompleteWriter()
    {
        if (Interlocked.Exchange(ref _completed, 1) == 0)
            _channel.Writer.TryComplete();
    }

    private async ValueTask CancelAsync()
    {
        if (!_stopping.IsCancellationRequested)
            await _stopping.CancelAsync().ConfigureAwait(false);
    }
}

internal sealed class KeyOrderedPartitionDispatcher<TKey, TValue>
{
    private readonly PartitionProcessorContext<TKey, TValue> _context;
    private readonly int _maxBatchSize;
    private readonly Func<IReadOnlyList<ConsumeResult<TKey, TValue>>, CancellationToken, ValueTask> _processor;
    private readonly SemaphoreSlim _concurrency;
    private readonly SemaphoreSlim _inFlight;
    private readonly object _gate = new();
    private readonly object _failureGate = new();
    private readonly Dictionary<PartitionMessageKey<TKey>, KeyOrderedProcessingLane<TKey, TValue>> _lanes = [];
    private readonly ConcurrentDictionary<Task, byte> _tasks = new();
    private readonly CancellationTokenSource _failureCancellation = new();
    private CancellationToken _processingCancellationToken;
    private ExceptionDispatchInfo? _failure;

    public KeyOrderedPartitionDispatcher(
        PartitionProcessorContext<TKey, TValue> context,
        int maxBatchSize,
        int maxConcurrentHandlers,
        int maxBufferedRecords,
        Func<IReadOnlyList<ConsumeResult<TKey, TValue>>, CancellationToken, ValueTask> processor)
    {
        _context = context;
        _maxBatchSize = maxBatchSize;
        _processor = processor;
        _concurrency = new SemaphoreSlim(maxConcurrentHandlers);
        _inFlight = new SemaphoreSlim(maxBufferedRecords);
    }

    public async ValueTask RunAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _failureCancellation.Token);
        _processingCancellationToken = linkedCancellation.Token;

        try
        {
            while (await _context.WaitToReadMessageAsync(_processingCancellationToken).ConfigureAwait(false))
            {
                while (_context.TryReadMessage(out var message))
                {
                    await _inFlight.WaitAsync(_processingCancellationToken).ConfigureAwait(false);
                    Enqueue(message);
                    ThrowIfFailed();
                }
            }

            await WaitForActiveLanesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_failure is not null)
        {
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested || _failure is not null)
                await ObserveActiveLanesAsync().ConfigureAwait(false);
        }

        ThrowIfFailed();
    }

    private void Enqueue(ConsumeResult<TKey, TValue> message)
    {
        var key = PartitionMessageKey<TKey>.From(message.Key);
        KeyOrderedProcessingLane<TKey, TValue> lane;
        bool shouldStart;

        lock (_gate)
        {
            if (!_lanes.TryGetValue(key, out lane!))
            {
                lane = new KeyOrderedProcessingLane<TKey, TValue>(this, key);
                _lanes.Add(key, lane);
            }

            shouldStart = lane.Enqueue(message);
        }

        if (shouldStart)
            StartLane(lane);
    }

    private void StartLane(KeyOrderedProcessingLane<TKey, TValue> lane)
    {
        var task = lane.RunAsync().AsTask();
        _tasks.TryAdd(task, 0);

        _ = task.ContinueWith(
            static (completedTask, state) =>
            {
                var dispatcher = (KeyOrderedPartitionDispatcher<TKey, TValue>)state!;
                dispatcher.CompleteLaneTask(completedTask);
            },
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void CompleteLaneTask(Task task)
    {
        _tasks.TryRemove(task, out _);

        if (!task.IsFaulted || task.Exception is null)
            return;

        var exception = task.Exception.InnerExceptions.Count == 1
            ? task.Exception.InnerException!
            : task.Exception;
        CaptureFailure(exception);
    }

    internal async ValueTask ProcessBatchAsync(IReadOnlyList<ConsumeResult<TKey, TValue>> batch)
    {
        try
        {
            await _concurrency.WaitAsync(_processingCancellationToken).ConfigureAwait(false);
            try
            {
                await _processor(batch, _processingCancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _concurrency.Release();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !_processingCancellationToken.IsCancellationRequested)
        {
            CaptureFailure(ex);
            throw;
        }
        finally
        {
            _inFlight.Release(batch.Count);
        }
    }

    internal int MaxBatchSize => _maxBatchSize;

    internal CancellationToken ProcessingCancellationToken => _processingCancellationToken;

    internal int LaneCount
    {
        get
        {
            lock (_gate)
                return _lanes.Count;
        }
    }

    internal void RemoveIdleLane(
        PartitionMessageKey<TKey> key,
        KeyOrderedProcessingLane<TKey, TValue> lane)
    {
        lock (_gate)
        {
            if (_lanes.TryGetValue(key, out var currentLane)
                && ReferenceEquals(currentLane, lane)
                && lane.IsIdle)
            {
                _lanes.Remove(key);
            }
        }
    }

    private async ValueTask WaitForActiveLanesAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var tasks = _tasks.Keys.ToArray();
            if (tasks.Length == 0)
                return;

            try
            {
                await Task.WhenAll(tasks).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (_failure is not null)
            {
                return;
            }
        }
    }

    private async ValueTask ObserveActiveLanesAsync()
    {
        var tasks = _tasks.Keys.ToArray();
        if (tasks.Length == 0)
            return;

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch
        {
            // The original handler failure or cancellation is preserved by the dispatcher.
        }
    }

    private void CaptureFailure(Exception exception)
    {
        lock (_failureGate)
            _failure ??= ExceptionDispatchInfo.Capture(exception);

        _failureCancellation.Cancel();
    }

    private void ThrowIfFailed()
    {
        _failure?.Throw();
    }
}

internal sealed class KeyOrderedProcessingLane<TKey, TValue>(
    KeyOrderedPartitionDispatcher<TKey, TValue> dispatcher,
    PartitionMessageKey<TKey> key)
{
    private readonly object _gate = new();
    private readonly Queue<ConsumeResult<TKey, TValue>> _queue = [];
    private bool _running;

    public bool Enqueue(ConsumeResult<TKey, TValue> message)
    {
        lock (_gate)
        {
            _queue.Enqueue(message);
            if (_running)
                return false;

            _running = true;
            return true;
        }
    }

    internal bool IsIdle
    {
        get
        {
            lock (_gate)
                return !_running && _queue.Count == 0;
        }
    }

    public async ValueTask RunAsync()
    {
        var batch = new List<ConsumeResult<TKey, TValue>>(dispatcher.MaxBatchSize);

        while (true)
        {
            batch.Clear();
            var removeLane = false;
            lock (_gate)
            {
                while (batch.Count < dispatcher.MaxBatchSize && _queue.Count > 0)
                    batch.Add(_queue.Dequeue());

                if (batch.Count == 0)
                {
                    _running = false;
                    removeLane = true;
                }
            }

            if (removeLane)
            {
                dispatcher.RemoveIdleLane(key, this);
                return;
            }

            dispatcher.ProcessingCancellationToken.ThrowIfCancellationRequested();
            await dispatcher.ProcessBatchAsync(batch.ToArray()).ConfigureAwait(false);
        }
    }
}

internal readonly struct PartitionMessageKey<TKey> : IEquatable<PartitionMessageKey<TKey>>
{
    private readonly bool _hasValue;
    private readonly TKey? _value;

    private PartitionMessageKey(TKey? value, bool hasValue)
    {
        _value = value;
        _hasValue = hasValue;
    }

    public static PartitionMessageKey<TKey> From(TKey? value)
    {
        return value is null
            ? new PartitionMessageKey<TKey>(default, hasValue: false)
            : new PartitionMessageKey<TKey>(value, hasValue: true);
    }

    public bool Equals(PartitionMessageKey<TKey> other)
    {
        if (_hasValue != other._hasValue)
            return false;

        return !_hasValue || EqualityComparer<TKey>.Default.Equals(_value!, other._value!);
    }

    public override bool Equals(object? obj)
    {
        return obj is PartitionMessageKey<TKey> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _hasValue
            ? EqualityComparer<TKey>.Default.GetHashCode(_value!)
            : 0;
    }
}

internal enum RuntimeCommandKind
{
    Resume,
    Commit,
    StopFailed,
    RestartLane,
    AssignPartitions,
    StopPartitions
}

internal readonly record struct RuntimeCommand<TKey, TValue>(
    RuntimeCommandKind Kind,
    PartitionLane<TKey, TValue>? Lane,
    TopicPartition[]? Partitions,
    TopicPartition Partition,
    PartitionStopReason StopReason,
    TaskCompletionSource? Completion,
    Exception? Exception,
    CancellationToken CancellationToken)
{
    public static RuntimeCommand<TKey, TValue> Resume(PartitionLane<TKey, TValue> lane)
        => new(RuntimeCommandKind.Resume, lane, null, default, default, null, null, CancellationToken.None);

    public static RuntimeCommand<TKey, TValue> Commit(
        PartitionLane<TKey, TValue> lane,
        TaskCompletionSource completion,
        CancellationToken cancellationToken)
        => new(RuntimeCommandKind.Commit, lane, null, default, default, completion, null, cancellationToken);

    public static RuntimeCommand<TKey, TValue> StopFailed(
        PartitionLane<TKey, TValue> lane,
        Exception exception)
        => new(RuntimeCommandKind.StopFailed, lane, null, default, default, null, exception, CancellationToken.None);

    public static RuntimeCommand<TKey, TValue> RestartLane(TopicPartition partition)
        => new(RuntimeCommandKind.RestartLane, null, null, partition, default, null, null, CancellationToken.None);

    public static RuntimeCommand<TKey, TValue> AssignPartitions(TopicPartition[] partitions)
        => new(RuntimeCommandKind.AssignPartitions, null, partitions, default, default, null, null, CancellationToken.None);

    public static RuntimeCommand<TKey, TValue> StopPartitions(
        TopicPartition[] partitions,
        PartitionStopReason stopReason)
        => new(RuntimeCommandKind.StopPartitions, null, partitions, default, stopReason, null, null, CancellationToken.None);
}
