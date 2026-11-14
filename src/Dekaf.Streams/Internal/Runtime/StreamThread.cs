using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Streams.Internal.Runtime;

/// <summary>
/// One member of the streams group: it owns the tasks the coordinator assigns to it and runs them on a
/// single loop. Everything here except the group heartbeat runs on that loop, so tasks and stores need no locks.
/// </summary>
internal sealed partial class StreamThread
{
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RepartitionPurgeInterval = TimeSpan.FromSeconds(30);

    // The commit timer is read once per this many records of a batch, not per record.
    private const long CommitCheckMask = 0xFF;

    private readonly Topology _topology;
    private readonly StreamsConfig _config;
    private readonly KafkaClient _client;
    private readonly int _index;
    private readonly ILogger _logger;
    private readonly bool _exactlyOnce;
    private readonly StateDirectory? _stateDirectory;
    private readonly Dictionary<TaskId, ActiveTask> _tasks = [];
    private readonly Dictionary<TopicPartition, ActiveTask> _tasksByPartition = [];
    private readonly List<TopicPartitionOffset> _commitOffsets = [];

    private IStreamsGroupMember _member = null!;
    private IKafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _consumer = null!;
    private IKafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>?> _restoreConsumer = null!;
    private IKafkaProducer<ReadOnlyMemory<byte>?, ReadOnlyMemory<byte>?> _producer = null!;
    private IAdminClient _admin = null!;
    private ProducerCollector _collector = null!;

    private ITransaction<ReadOnlyMemory<byte>?, ReadOnlyMemory<byte>?>? _transaction;
    private StreamsGroupMemberSnapshot? _seenSnapshot;
    private string _reportedTasks = "";
    private int _generation;
    private long _lastPurge;
    private Task<bool>? _nextBatch;
    private bool _dirty;
    private long _lastCommit;
    private long _uncommittedRecords;
    private int _consecutiveFailures;
    private string _lastStatus = "";

    public StreamThread(Topology topology, StreamsConfig config, KafkaClient client, int index)
    {
        _topology = topology;
        _config = config;
        _client = client;
        _index = index;
        _exactlyOnce = config.Guarantee == ProcessingGuarantee.ExactlyOnce;
        _logger = (ILogger?)config.LoggerFactory?.CreateLogger<StreamThread>() ?? NullLogger.Instance;
        _stateDirectory = config.StateDirectory is null
            ? null
            : new StateDirectory(config.StateDirectory, topology.ApplicationId);
    }

    private string ThreadName => $"{_topology.ApplicationId}-{_index}";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Exception? failure = null;
        try
        {
            await StartAsync(cancellationToken).ConfigureAwait(false);
            await LoopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            failure = exception;
            LogThreadFailed(exception, ThreadName);
        }

        await StopAsync(clean: failure is null).ConfigureAwait(false);
        if (failure is not null)
        {
            ExceptionDispatchInfo.Throw(failure);
        }
    }

    private async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        var isolation = _exactlyOnce ? IsolationLevel.ReadCommitted : IsolationLevel.ReadUncommitted;
        _consumer = await _client.CreateConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>()
            .WithClientId($"{ThreadName}-consumer")
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithIsolationLevel(isolation)
            .WithKeyDeserializer(Serializers.RawBytes)
            .WithValueDeserializer(Serializers.RawBytes)
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);

        // Changelogs are read back only up to what was committed, whichever guarantee wrote them.
        _restoreConsumer = await _client.CreateConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>?>()
            .WithClientId($"{ThreadName}-restore-consumer")
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithKeyDeserializer(Serializers.RawBytes)
            .WithValueDeserializer(NullableBytesSerde.Instance)
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);

        var producer = _client.CreateProducer<ReadOnlyMemory<byte>?, ReadOnlyMemory<byte>?>()
            .WithClientId($"{ThreadName}-producer")
            .WithKeySerializer(NullableBytesSerde.Instance)
            .WithValueSerializer(NullableBytesSerde.Instance);
        if (_exactlyOnce)
        {
            // The id only has to be unique: a zombie is fenced by its member epoch when it commits offsets,
            // not by the transactional id.
            producer = producer
                .WithTransactionalId($"{_topology.ApplicationId}-{_config.ProcessId}-{_index}")
                .WithTransactionTimeout(_config.TransactionTimeout)
                .WithInlineTransactionCompletions(false);
        }

        _producer = await producer.BuildAsync(cancellationToken).ConfigureAwait(false);
        if (_exactlyOnce)
        {
            await _producer.InitTransactionsAsync(cancellationToken).ConfigureAwait(false);
        }

        _collector = new ProducerCollector(_producer);
        _admin = _client.CreateAdminClient().Build();

        _member = _client.CreateStreamsGroupMember(new StreamsGroupMemberOptions
        {
            GroupId = _topology.ApplicationId,
            InstanceId = _config.InstanceId is null ? null : $"{_config.InstanceId}-{_index}",
            RebalanceTimeout = _config.RebalanceTimeout
        });
        await _member.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await JoinAsync(cancellationToken).ConfigureAwait(false);
        _lastCommit = Stopwatch.GetTimestamp();
        _lastPurge = _lastCommit;
    }

    private async ValueTask JoinAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _member.JoinAsync(
                new StreamsGroupMemberUpdate
                {
                    Topology = _topology.ToStreamsGroupTopology(),
                    ActiveTasks = [],
                    StandbyTasks = [],
                    WarmupTasks = [],
                    ProcessId = _config.ProcessId,
                    ClientTags = []
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (GroupException exception) when (exception.ErrorCode == ErrorCode.InvalidRequest)
        {
            // What a coordinator answers when the group already runs a different topology.
            throw new StreamsException(
                $"The coordinator rejected the topology of application '{_topology.ApplicationId}': {exception.Message} " +
                "Kafka does not support changing the topology of an existing streams group. Use a new application id, " +
                "or stop every instance and delete the group and its internal topics first.",
                exception);
        }

        _reportedTasks = "";
        _generation = _member.Snapshot.MembershipGeneration;
        LogJoined(ThreadName, _member.Snapshot.MemberId, _member.Snapshot.MemberEpoch);
    }

    private async ValueTask LoopAsync(CancellationToken cancellationToken)
    {
        var batches = _consumer.ConsumeRawBatchAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await ReconcileAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (KafkaException exception) when (IsRecoverable(exception))
            {
                // A coordinator move, a broker restart or a lost membership: drop what we hold and converge again.
                LogReconcileFailed(exception, ThreadName);
                await AbortAndResetAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (_tasks.Count == 0)
            {
                await Task.Delay(_config.PollInterval, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // A fetched batch usually is ready, which completes synchronously with a cached task.
            _nextBatch ??= batches.MoveNextAsync().AsTask();
            if (!_nextBatch.IsCompleted)
            {
                await Task.WhenAny(_nextBatch, Task.Delay(_config.PollInterval, cancellationToken))
                    .ConfigureAwait(false);
            }

            if (_nextBatch.IsCompleted)
            {
                var hasBatch = await _nextBatch.ConfigureAwait(false);
                _nextBatch = null;
                if (!hasBatch)
                {
                    return;
                }

                await ProcessBatchAsync(batches.Current).ConfigureAwait(false);
            }

            if (_dirty && Stopwatch.GetElapsedTime(_lastCommit) >= _config.CommitInterval)
            {
                await CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask ProcessBatchAsync(ConsumeRawBatch batch)
    {
        // A batch fetched before its partition was revoked is left unread.
        if (!_tasksByPartition.TryGetValue(batch.TopicPartition, out var task))
        {
            return;
        }

        var topic = batch.Topic;
        var source = task.SourceIndex(topic);
        long processed = 0;
        foreach (var record in batch)
        {
            // After a reset the consumer can still hold records from before the position it was moved to.
            if (record.Offset < task.NextOffsets[source])
            {
                continue;
            }

            // Opened by the first record, so a batch with nothing to process leaves no transaction behind.
            if (_exactlyOnce && _transaction is null)
            {
                _transaction = _producer.BeginTransaction();
            }

            var raw = new RawRecord(
                record.Key,
                record.IsKeyNull,
                record.Value,
                record.IsValueNull,
                record.TimestampMs,
                record.Offset,
                CopyHeaders(record.Headers));
            task.Task.Process(topic, in raw);
            task.NextOffsets[source] = record.Offset + 1;
            task.Dirty = true;
            _dirty = true;
            processed++;

            if (_collector.HasPending)
            {
                await _collector.DrainAsync().ConfigureAwait(false);
            }

            // A long batch must not keep a transaction open past its timeout, so the commit timer is also
            // checked inside it. A failed commit dropped the task this batch belongs to.
            if ((processed & CommitCheckMask) == 0
                && Stopwatch.GetElapsedTime(_lastCommit) >= _config.CommitInterval)
            {
                _uncommittedRecords += processed;
                processed = 0;
                if (!await CommitAsync(CancellationToken.None).ConfigureAwait(false))
                {
                    return;
                }
            }
        }

        _uncommittedRecords += processed;
    }

    // Header values point into the fetch buffer, which is reused once the batch is done, while a record
    // forwarded to a sink is read by the producer later. Records without headers allocate nothing.
    private static Headers? CopyHeaders(ReadOnlyMemory<Header> headers)
    {
        if (headers.IsEmpty)
        {
            return null;
        }

        var copy = new Headers(headers.Length);
        foreach (ref readonly var header in headers.Span)
        {
            copy.Add(header.Key, header.IsValueNull ? null : header.Value.ToArray());
        }

        return copy;
    }

    /// <summary>
    /// Moves the owned tasks to the assignment the coordinator last sent, then reports what is owned.
    /// A revoked task is committed and closed before it is given up; a new task is restored before it runs.
    /// </summary>
    private async ValueTask ReconcileAsync(CancellationToken cancellationToken)
    {
        var snapshot = _member.Snapshot;
        if (ReferenceEquals(snapshot, _seenSnapshot))
        {
            return;
        }

        if (!snapshot.IsJoined)
        {
            // Fenced or expired: the tasks belong to someone else now, so nothing may be committed for them.
            LogMembershipLost(ThreadName);
            await AbortAndResetAsync(cancellationToken).ConfigureAwait(false);
            await JoinAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (snapshot.MembershipGeneration != _generation)
        {
            // The member was fenced and rejoined on its own. Its old tasks may already run elsewhere, so
            // their uncommitted work is discarded; whatever is assigned now is built from committed state.
            if (_tasks.Count > 0 || _transaction is not null)
            {
                LogMembershipLost(ThreadName);
                await AbortAndResetAsync(cancellationToken).ConfigureAwait(false);
            }

            _generation = snapshot.MembershipGeneration;
            _reportedTasks = "";
        }

        ObserveStatus(snapshot.Status);

        var target = new HashSet<TaskId>();
        foreach (var taskSet in snapshot.Assignment.ActiveTasks)
        {
            var subtopology = int.Parse(taskSet.SubtopologyId, System.Globalization.CultureInfo.InvariantCulture);
            foreach (var partition in taskSet.Partitions)
            {
                target.Add(new TaskId(subtopology, partition));
            }
        }

        List<TaskId>? revoked = null;
        foreach (var owned in _tasks.Keys)
        {
            if (!target.Contains(owned))
            {
                (revoked ??= []).Add(owned);
            }
        }

        List<TaskId>? added = null;
        foreach (var assigned in target)
        {
            if (!_tasks.ContainsKey(assigned))
            {
                (added ??= []).Add(assigned);
            }
        }

        if (revoked is not null || added is not null)
        {
            // Commit first: restoring new tasks can take longer than a transaction may stay open.
            if (!await CommitAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            if (revoked is not null)
            {
                await CloseTasksAsync(revoked, clean: true, cancellationToken).ConfigureAwait(false);
            }

            if (added is not null)
            {
                await AddTasksAsync(added, cancellationToken).ConfigureAwait(false);
            }
        }

        // Standby and warmup assignments are part of what is acknowledged, so a change in them is reported too.
        var owning = $"{DescribeOwnedTasks()} standby=[{Describe(snapshot.Assignment.StandbyTasks)}] warmup=[{Describe(snapshot.Assignment.WarmupTasks)}]";
        if (owning != _reportedTasks)
        {
            await _member.UpdateAsync(
                new StreamsGroupMemberUpdate
                {
                    ActiveTasks = OwnedTaskSets(),
                    // Standby and warmup tasks are acknowledged but not materialized: a task that moves
                    // here restores from its changelog.
                    StandbyTasks = snapshot.Assignment.StandbyTasks,
                    WarmupTasks = snapshot.Assignment.WarmupTasks
                },
                cancellationToken).ConfigureAwait(false);
            _reportedTasks = owning;
            LogAssignmentChanged(ThreadName, owning);
        }

        _seenSnapshot = snapshot;
    }

    private void ObserveStatus(IReadOnlyList<StreamsGroupStatus> statuses)
    {
        var description = "";
        foreach (var status in statuses)
        {
            switch (status.Code)
            {
                case StreamsGroupStatusCode.StaleTopology:
                    throw new StreamsException(
                        $"The topology of application '{_topology.ApplicationId}' differs from the one the group runs: " +
                        $"{status.StatusDetail}. Kafka does not support changing the topology of a running streams group; " +
                        "use a new application id, or stop every instance and delete the group first.");
                case StreamsGroupStatusCode.ShutdownApplication:
                    throw new StreamsException(
                        $"Another member requested shutdown of application '{_topology.ApplicationId}'.");
                default:
                    description += $"{status.Code}: {status.StatusDetail}. ";
                    break;
            }
        }

        // The coordinator assigns nothing until these are resolved, so say why the instance is idle. Missing
        // internal topics are normal on first start: the coordinator is creating them.
        if (description != _lastStatus)
        {
            _lastStatus = description;
            if (description.Length > 0)
            {
                LogGroupNotReady(ThreadName, description);
            }
        }
    }

    private async ValueTask AddTasksAsync(List<TaskId> added, CancellationToken cancellationToken)
    {
        var created = new List<ActiveTask>(added.Count);
        try
        {
            foreach (var id in added)
            {
                var directory = _stateDirectory is null ? null : await _stateDirectory.OpenAsync(id, cancellationToken).ConfigureAwait(false);
                try
                {
                    var task = new StreamTask(
                        _topology, id.Subtopology, id.Partition, _collector, _config.StoreEngine, directory?.Path);
                    created.Add(new ActiveTask(id, task, directory));
                }
                catch
                {
                    directory?.Close(offsets: null);
                    throw;
                }
            }

            var sources = new List<TopicPartition>();
            foreach (var task in created)
            {
                sources.AddRange(task.Sources);
            }

            var committed = await FetchStartOffsetsAsync(sources, cancellationToken).ConfigureAwait(false);
            await RestoreAsync(created, committed, cancellationToken).ConfigureAwait(false);

            var assignment = new List<TopicPartitionOffset>(sources.Count);
            foreach (var task in created)
            {
                for (var i = 0; i < task.Sources.Length; i++)
                {
                    task.NextOffsets[i] = committed[task.Sources[i]].Offset;
                    assignment.Add(new TopicPartitionOffset(task.Sources[i].Topic, task.Sources[i].Partition, task.NextOffsets[i]));
                }
            }

            _consumer.Partitions.IncrementalAssign(assignment);
        }
        catch
        {
            foreach (var task in created)
            {
                task.Close(checkpoint: null);
            }

            throw;
        }

        foreach (var task in created)
        {
            _tasks.Add(task.Id, task);
            foreach (var source in task.Sources)
            {
                _tasksByPartition.Add(source, task);
            }
        }

        StreamsMetrics.ActiveTasks.Add(created.Count);
    }

    /// <summary>
    /// Returns where each source partition starts: the group's committed offset, or the start of the
    /// partition when the group has none. <c>HasCommitted</c> tells the two apart.
    /// </summary>
    private async ValueTask<Dictionary<TopicPartition, (long Offset, bool HasCommitted)>> FetchStartOffsetsAsync(
        List<TopicPartition> partitions,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<TopicPartition, (long, bool)>(partitions.Count);
        if (partitions.Count == 0)
        {
            return result;
        }

        var spec = new Dictionary<string, ListStreamsGroupOffsetsSpec>
        {
            [_topology.ApplicationId] = new() { TopicPartitions = partitions }
        };

        // RequireStable waits for offsets that a previous owner's transaction is still committing; reading
        // past them would process their input a second time.
        var options = new ListStreamsGroupOffsetsOptions { RequireStable = true };
        StreamsGroupOffsetsResult offsets;
        while (true)
        {
            offsets = (await _admin.ListStreamsGroupOffsetsAsync(spec, options, cancellationToken).ConfigureAwait(false))
                [_topology.ApplicationId];
            if (offsets.ErrorCode == ErrorCode.None)
            {
                break;
            }

            if (!offsets.ErrorCode.IsRetriable())
            {
                throw new KafkaException(offsets.ErrorCode, $"Fetching the committed offsets of group '{_topology.ApplicationId}' failed: {offsets.ErrorCode}.");
            }

            await Task.Delay(_config.PollInterval, cancellationToken).ConfigureAwait(false);
        }

        List<TopicPartitionOffsetSpec>? uncommitted = null;
        foreach (var partition in partitions)
        {
            if (offsets.Offsets.TryGetValue(partition, out var description) && description.Offset >= 0)
            {
                result[partition] = (description.Offset, true);
            }
            else
            {
                (uncommitted ??= []).Add(new TopicPartitionOffsetSpec { TopicPartition = partition, Spec = OffsetSpec.Earliest });
            }
        }

        if (uncommitted is not null)
        {
            var earliest = await _admin.ListOffsetsAsync(uncommitted, null, cancellationToken).ConfigureAwait(false);
            foreach (var entry in earliest)
            {
                result[entry.Key] = (entry.Value.Offset, false);
            }
        }

        return result;
    }

    /// <summary>
    /// Rebuilds the stores of new tasks before they process anything. A changelog is read to its last
    /// committed record. A table's source topic is read up to the group's committed offset only: later
    /// records are still to be processed, which updates the store and forwards them.
    /// </summary>
    private async ValueTask RestoreAsync(
        List<ActiveTask> tasks,
        Dictionary<TopicPartition, (long Offset, bool HasCommitted)> committed,
        CancellationToken cancellationToken)
    {
        var plans = new Dictionary<TopicPartition, (ActiveTask Task, long End)>();
        var seeks = new List<TopicPartitionOffset>();
        var fromBeginning = new List<TopicPartition>();
        foreach (var task in tasks)
        {
            foreach (var topic in task.Task.RestoreTopics)
            {
                var partition = new TopicPartition(topic, task.Id.Partition);
                var end = long.MaxValue;
                if (committed.TryGetValue(partition, out var start))
                {
                    if (!start.HasCommitted)
                    {
                        continue;
                    }

                    end = start.Offset;
                }

                var checkpoint = task.Directory?.CheckpointOf(topic) ?? -1;
                if (checkpoint >= end)
                {
                    continue;
                }

                plans.Add(partition, (task, end));
                if (checkpoint >= 0)
                {
                    seeks.Add(new TopicPartitionOffset(topic, task.Id.Partition, checkpoint));
                }
                else
                {
                    fromBeginning.Add(partition);
                }
            }
        }

        if (plans.Count == 0)
        {
            return;
        }

        var started = Stopwatch.GetTimestamp();
        long restored = 0;
        _restoreConsumer.Partitions.Assign([.. plans.Keys]);
        try
        {
            foreach (var seek in seeks)
            {
                _restoreConsumer.Positions.Seek(seek);
            }

            if (fromBeginning.Count > 0)
            {
                _restoreConsumer.Positions.SeekToBeginning([.. fromBeginning]);
            }

            await foreach (var record in _restoreConsumer.ConsumeSnapshotAsync(cancellationToken).ConfigureAwait(false))
            {
                var plan = plans[new TopicPartition(record.Topic, record.Partition)];
                if (record.Offset >= plan.End || record.IsKeyNull)
                {
                    continue;
                }

                var value = record.Value;
                plan.Task.Task.Restore(record.Topic, record.Key.Span, value.GetValueOrDefault().Span, value is null);
                restored++;
            }
        }
        finally
        {
            _restoreConsumer.Partitions.Unassign();
        }

        StreamsMetrics.RecordsRestored.Add(restored);
        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        LogRestored(ThreadName, restored, plans.Count, elapsedMs);
    }

    /// <summary>
    /// Commits everything processed since the last commit. Returns false when the commit was aborted; the
    /// tasks were then dropped and are restored and reprocessed from the last committed state.
    /// </summary>
    private async ValueTask<bool> CommitAsync(CancellationToken cancellationToken)
    {
        if (!_dirty)
        {
            return true;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            if (_collector.HasPending)
            {
                await _collector.DrainAsync().ConfigureAwait(false);
            }

            ThrowIfProduceFailed();

            _commitOffsets.Clear();
            foreach (var task in _tasks.Values)
            {
                if (!task.Dirty)
                {
                    continue;
                }

                for (var i = 0; i < task.Sources.Length; i++)
                {
                    _commitOffsets.Add(new TopicPartitionOffset(task.Sources[i].Topic, task.Sources[i].Partition, task.NextOffsets[i]));
                }
            }

            // The commit is not cancelled half way: its outcome would be unknown.
            var snapshot = _member.Snapshot;
            if (snapshot.MembershipGeneration != _generation || !snapshot.IsJoined)
            {
                // Fenced since the tasks were built. The new epoch is valid, so the coordinator would
                // accept these offsets although the tasks belong to another member now.
                throw new GroupException(ErrorCode.FencedMemberEpoch, "The member was fenced before it could commit.");
            }

            if (_exactlyOnce)
            {
                await _transaction!.SendOffsetsToTransactionAsync(
                    _commitOffsets,
                    new ConsumerGroupMetadata
                    {
                        GroupId = _topology.ApplicationId,
                        GenerationId = snapshot.MemberEpoch,
                        MemberId = snapshot.MemberId ?? "",
                        GroupInstanceId = _member.InstanceId
                    },
                    CancellationToken.None).ConfigureAwait(false);
                await _transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
                await _transaction.DisposeAsync().ConfigureAwait(false);
                _transaction = null;
            }
            else
            {
                await _producer.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                ThrowIfProduceFailed();
                await _member.CommitOffsetsAsync(_commitOffsets, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            LogCommitAborted(exception, ThreadName);
            StreamsMetrics.AbortedCommits.Add(1);
            await AbortAndResetAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        foreach (var task in _tasks.Values)
        {
            task.Dirty = false;
        }

        _collector.Reset();
        _dirty = false;
        _consecutiveFailures = 0;
        _lastCommit = Stopwatch.GetTimestamp();
        StreamsMetrics.RecordsProcessed.Add(_uncommittedRecords);
        _uncommittedRecords = 0;
        StreamsMetrics.Commits.Add(1);
        StreamsMetrics.CommitDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds);

        if (Stopwatch.GetElapsedTime(_lastPurge) >= RepartitionPurgeInterval)
        {
            _lastPurge = Stopwatch.GetTimestamp();
            await PurgeRepartitionTopicsAsync(cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Deletes repartition records that are committed as consumed. Repartition topics have no time-based
    /// retention, because a record must stay until it is processed however long that takes; this is what
    /// keeps them from growing without bound.
    /// </summary>
    private async ValueTask PurgeRepartitionTopicsAsync(CancellationToken cancellationToken)
    {
        Dictionary<TopicPartition, long>? consumed = null;
        foreach (var task in _tasks.Values)
        {
            for (var i = 0; i < task.Sources.Length; i++)
            {
                if (_topology.IsRepartitionTopic(task.Sources[i].Topic) && task.NextOffsets[i] > task.PurgedOffsets[i])
                {
                    (consumed ??= []).Add(task.Sources[i], task.NextOffsets[i]);
                    task.PurgedOffsets[i] = task.NextOffsets[i];
                }
            }
        }

        if (consumed is null)
        {
            return;
        }

        try
        {
            await _admin.DeleteRecordsAsync(consumed, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (KafkaException exception)
        {
            // Purging only reclaims space; the next round covers these offsets again.
            LogPurgeFailed(exception, ThreadName);
            foreach (var task in _tasks.Values)
            {
                Array.Clear(task.PurgedOffsets);
            }
        }
    }

    private void ThrowIfProduceFailed()
    {
        if (_collector.Failure is { } failure)
        {
            ExceptionDispatchInfo.Throw(failure);
        }
    }

    /// <summary>
    /// Failures that an abort and a fresh start from the committed state can get past. Anything else,
    /// such as a fenced producer or a record the broker rejects for good, stops the thread.
    /// </summary>
    private static bool IsRecoverable(Exception exception) => exception switch
    {
        FatalTransactionException => false,
        AbortableTransactionException => true,
        KafkaException kafka => kafka.IsRetriable
            || kafka.ErrorCode is ErrorCode.StaleMemberEpoch or ErrorCode.FencedMemberEpoch or ErrorCode.UnknownMemberId,
        _ => false
    };

    /// <summary>
    /// Gives up everything since the last commit: aborts the open transaction and drops every task with
    /// its state. The next reconcile recreates the assigned tasks from their changelogs at the committed offsets.
    /// </summary>
    private async ValueTask AbortAndResetAsync(CancellationToken cancellationToken)
    {
        if (_collector.HasPending)
        {
            await _collector.DrainAsync().ConfigureAwait(false);
        }

        if (_transaction is not null)
        {
            // An abort that fails leaves the producer unusable, so it is not caught here.
            await _transaction.AbortAsync(CancellationToken.None).ConfigureAwait(false);
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
        }

        _collector.Reset();
        _dirty = false;
        _uncommittedRecords = 0;
        await CloseTasksAsync([.. _tasks.Keys], clean: false, cancellationToken).ConfigureAwait(false);
        _seenSnapshot = null;

        // Back off so a failure that repeats does not spin.
        var delay = TimeSpan.FromMilliseconds(Math.Min(
            _config.PollInterval.TotalMilliseconds * (1 << Math.Min(_consecutiveFailures++, 10)),
            MaxRetryDelay.TotalMilliseconds));
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Closes tasks and stops fetching their partitions. A clean close follows a commit, so the state on
    /// disk matches the changelog and is checkpointed; otherwise it is discarded.
    /// </summary>
    private async ValueTask CloseTasksAsync(List<TaskId> ids, bool clean, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var partitions = new List<TopicPartition>();
        var closing = new List<ActiveTask>(ids.Count);
        foreach (var id in ids)
        {
            if (_tasks.Remove(id, out var task))
            {
                closing.Add(task);
                foreach (var source in task.Sources)
                {
                    _tasksByPartition.Remove(source);
                    partitions.Add(source);
                }
            }
        }

        _consumer.Partitions.IncrementalUnassign(partitions);
        StreamsMetrics.ActiveTasks.Add(-closing.Count);

        Dictionary<TopicPartition, long>? ends = null;
        if (clean && _stateDirectory is not null)
        {
            try
            {
                ends = await FetchChangelogEndsAsync(closing, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is KafkaException or OperationCanceledException)
            {
                // Without the offsets there is no checkpoint, and the next owner restores in full.
                LogCheckpointSkipped(exception, ThreadName);
            }
        }

        foreach (var task in closing)
        {
            task.Close(ends is null ? null : task.CheckpointFrom(ends));
        }
    }

    /// <summary>
    /// After a commit a task is the only writer of its changelog partitions and everything it wrote is
    /// committed, so the end of each partition is the offset the store is consistent with.
    /// </summary>
    private async ValueTask<Dictionary<TopicPartition, long>> FetchChangelogEndsAsync(
        List<ActiveTask> tasks,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<TopicPartition, long>();
        var specs = new List<TopicPartitionOffsetSpec>();
        foreach (var task in tasks)
        {
            for (var i = 0; i < task.Sources.Length; i++)
            {
                // A table's source topic is consistent up to what the task consumed, not up to its end.
                result[task.Sources[i]] = task.NextOffsets[i];
            }

            foreach (var topic in task.Task.RestoreTopics)
            {
                var partition = new TopicPartition(topic, task.Id.Partition);
                if (!result.ContainsKey(partition))
                {
                    specs.Add(new TopicPartitionOffsetSpec { TopicPartition = partition, Spec = OffsetSpec.Latest });
                }
            }
        }

        if (specs.Count > 0)
        {
            var ends = await _admin.ListOffsetsAsync(
                specs,
                new ListOffsetsOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                cancellationToken).ConfigureAwait(false);
            foreach (var end in ends)
            {
                result[end.Key] = end.Value.Offset;
            }
        }

        return result;
    }

    private async ValueTask StopAsync(bool clean)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            // Nothing was processed if start-up failed before the member existed; the clients it did create
            // are still disposed below.
            if (_member is not null)
            {
                if (clean)
                {
                    clean = await CommitAsync(timeout.Token).ConfigureAwait(false);
                }
                else if (_transaction is not null)
                {
                    await _transaction.AbortAsync(CancellationToken.None).ConfigureAwait(false);
                }

                await CloseTasksAsync([.. _tasks.Keys], clean, timeout.Token).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            LogStopFailed(exception, ThreadName);
            foreach (var task in _tasks.Values)
            {
                task.Close(checkpoint: null);
            }

            _tasks.Clear();
        }

        await DisposeQuietlyAsync(_member is null ? default : _member.CloseAsync(timeout.Token)).ConfigureAwait(false);
        await DisposeQuietlyAsync(_member?.DisposeAsync() ?? default).ConfigureAwait(false);
        await DisposeQuietlyAsync(_consumer?.DisposeAsync() ?? default).ConfigureAwait(false);
        if (_nextBatch is not null)
        {
            // The pending fetch ends, cancelled or faulted, once its consumer is disposed. Either outcome
            // is expected here; awaiting it only keeps the task from going unobserved.
            await ((Task)_nextBatch).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        await DisposeQuietlyAsync(_restoreConsumer?.DisposeAsync() ?? default).ConfigureAwait(false);
        await DisposeQuietlyAsync(_producer?.DisposeAsync() ?? default).ConfigureAwait(false);
        await DisposeQuietlyAsync(_admin?.DisposeAsync() ?? default).ConfigureAwait(false);
        _collector?.Dispose();
        LogStopped(ThreadName);
    }

    private async ValueTask DisposeQuietlyAsync(ValueTask operation)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogStopFailed(exception, ThreadName);
        }
    }

    private List<StreamsGroupTaskSet> OwnedTaskSets()
    {
        var bySubtopology = new SortedDictionary<int, List<int>>();
        foreach (var id in _tasks.Keys)
        {
            if (!bySubtopology.TryGetValue(id.Subtopology, out var partitions))
            {
                bySubtopology.Add(id.Subtopology, partitions = []);
            }

            partitions.Add(id.Partition);
        }

        var sets = new List<StreamsGroupTaskSet>(bySubtopology.Count);
        foreach (var entry in bySubtopology)
        {
            entry.Value.Sort();
            sets.Add(new StreamsGroupTaskSet
            {
                SubtopologyId = entry.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Partitions = entry.Value
            });
        }

        return sets;
    }

    private static string Describe(IReadOnlyList<StreamsGroupTaskSet> taskSets)
    {
        var ids = new List<string>();
        foreach (var taskSet in taskSets)
        {
            foreach (var partition in taskSet.Partitions)
            {
                ids.Add($"{taskSet.SubtopologyId}_{partition}");
            }
        }

        ids.Sort(StringComparer.Ordinal);
        return string.Join(",", ids);
    }

    private string DescribeOwnedTasks()
    {
        var ids = new List<TaskId>(_tasks.Keys);
        ids.Sort(static (a, b) => a.Subtopology != b.Subtopology
            ? a.Subtopology.CompareTo(b.Subtopology)
            : a.Partition.CompareTo(b.Partition));
        return string.Join(",", ids);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Stream thread {Thread} joined the group as member {MemberId} at epoch {MemberEpoch}")]
    private partial void LogJoined(string thread, string? memberId, int memberEpoch);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stream thread {Thread} now owns tasks [{Tasks}]")]
    private partial void LogAssignmentChanged(string thread, string tasks);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stream thread {Thread} restored {Records} changelog records from {Partitions} partitions in {ElapsedMs} ms")]
    private partial void LogRestored(string thread, long records, int partitions, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stream thread {Thread} has no assignment yet because the group is not ready: {Status}")]
    private partial void LogGroupNotReady(string thread, string status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stream thread {Thread} lost its group membership. Its uncommitted work is discarded and it rejoins")]
    private partial void LogMembershipLost(string thread);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stream thread {Thread} aborted a commit. Its tasks restart from the last committed state")]
    private partial void LogCommitAborted(Exception exception, string thread);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stream thread {Thread} could not apply its assignment and retries")]
    private partial void LogReconcileFailed(Exception exception, string thread);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stream thread {Thread} closed tasks without a checkpoint; their next owner restores them in full")]
    private partial void LogCheckpointSkipped(Exception exception, string thread);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stream thread {Thread} could not delete consumed repartition records and retries later")]
    private partial void LogPurgeFailed(Exception exception, string thread);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stream thread {Thread} hit an error while stopping")]
    private partial void LogStopFailed(Exception exception, string thread);

    [LoggerMessage(Level = LogLevel.Error, Message = "Stream thread {Thread} failed and stops")]
    private partial void LogThreadFailed(Exception exception, string thread);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stream thread {Thread} stopped")]
    private partial void LogStopped(string thread);
}

/// <summary>A task this thread owns, with its input partitions and how far it has read each.</summary>
internal sealed class ActiveTask
{
    public ActiveTask(TaskId id, StreamTask task, TaskDirectory? directory)
    {
        Id = id;
        Task = task;
        Directory = directory;
        var topics = new List<string>(task.SourceTopics);
        topics.Sort(StringComparer.Ordinal);
        Sources = new TopicPartition[topics.Count];
        for (var i = 0; i < Sources.Length; i++)
        {
            Sources[i] = new TopicPartition(topics[i], id.Partition);
        }

        NextOffsets = new long[Sources.Length];
        PurgedOffsets = new long[Sources.Length];
    }

    public TaskId Id { get; }

    public StreamTask Task { get; }

    public TaskDirectory? Directory { get; }

    public TopicPartition[] Sources { get; }

    /// <summary>The next offset to process per source partition; committed as the group's offset.</summary>
    public long[] NextOffsets { get; }

    /// <summary>Per source partition, the offset up to which consumed repartition records were deleted.</summary>
    public long[] PurgedOffsets { get; }

    /// <summary>Whether the task processed records since the last commit.</summary>
    public bool Dirty { get; set; }

    public int SourceIndex(string topic)
    {
        for (var i = 0; i < Sources.Length; i++)
        {
            if (Sources[i].Topic == topic)
            {
                return i;
            }
        }

        throw new StreamsException($"Task {Id} does not read topic '{topic}'.");
    }

    /// <summary>Picks, from partition end offsets, the offset each restore topic of this task is consistent with.</summary>
    public Dictionary<string, long> CheckpointFrom(Dictionary<TopicPartition, long> ends)
    {
        var checkpoint = new Dictionary<string, long>();
        foreach (var topic in Task.RestoreTopics)
        {
            if (ends.TryGetValue(new TopicPartition(topic, Id.Partition), out var end))
            {
                checkpoint[topic] = end;
            }
        }

        return checkpoint;
    }

    /// <summary>Closes the stores, then records <paramref name="checkpoint"/> or, when it is null, discards the state.</summary>
    public void Close(Dictionary<string, long>? checkpoint)
    {
        Task.Dispose();
        Directory?.Close(checkpoint);
    }
}
