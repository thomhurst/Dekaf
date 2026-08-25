using System.Runtime.CompilerServices;

namespace Dekaf.Testing;

/// <summary>
/// Identifies an in-memory client operation that can consume a scripted fault.
/// </summary>
public enum KafkaFaultOperation
{
    /// <summary>A produce acknowledgement.</summary>
    Produce = 1,
    /// <summary>A broker fetch.</summary>
    Fetch,
    /// <summary>A consumer delivery.</summary>
    Consume,
    /// <summary>An offset store.</summary>
    StoreOffset,
    /// <summary>An offset commit.</summary>
    Commit,
    /// <summary>A consumer-group join.</summary>
    JoinGroup,
    /// <summary>A consumer-group sync.</summary>
    SyncGroup,
    /// <summary>A rebalance callback or transition.</summary>
    Rebalance,
    /// <summary>Transaction initialization.</summary>
    InitializeTransactions,
    /// <summary>A produce within a transaction.</summary>
    TransactionProduce,
    /// <summary>An offsets-to-transaction operation.</summary>
    SendOffsetsToTransaction,
    /// <summary>A transaction commit.</summary>
    CommitTransaction,
    /// <summary>A transaction abort.</summary>
    AbortTransaction,
    /// <summary>An Admin client operation.</summary>
    Admin,
    /// <summary>A Share Consumer delivery.</summary>
    ShareConsume,
    /// <summary>A Share Consumer acknowledgement.</summary>
    ShareAcknowledge
}

/// <summary>
/// Identifies the action taken when a fault-plan entry is consumed.
/// </summary>
public enum KafkaFaultAction
{
    /// <summary>Throw a configured exception.</summary>
    Throw,
    /// <summary>Pause at a deterministic barrier.</summary>
    Pause
}

/// <summary>
/// Selects a fault by operation and optional Kafka resource identifiers.
/// Null selectors are wildcards when the scope is used to configure a rule.
/// </summary>
public readonly struct KafkaFaultScope
{
    /// <summary>
    /// Creates a scope. Null resource selectors act as wildcards on configured rules.
    /// </summary>
    public KafkaFaultScope(
        KafkaFaultOperation operation,
        string? topic = null,
        int? partition = null,
        string? groupId = null)
    {
        if (operation is < KafkaFaultOperation.Produce or > KafkaFaultOperation.ShareAcknowledge)
            throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown fault operation.");
        if (topic is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        if (partition < 0)
            throw new ArgumentOutOfRangeException(nameof(partition), partition, "Partition cannot be negative.");
        if (groupId is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        Operation = operation;
        Topic = topic;
        Partition = partition;
        GroupId = groupId;
    }

    /// <summary>Gets the operation selector.</summary>
    public KafkaFaultOperation Operation { get; }

    /// <summary>Gets the optional topic selector.</summary>
    public string? Topic { get; }

    /// <summary>Gets the optional partition selector.</summary>
    public int? Partition { get; }

    /// <summary>Gets the optional consumer-group selector.</summary>
    public string? GroupId { get; }

    internal bool Matches(KafkaFaultScope context)
    {
        if (Operation != context.Operation)
            return false;
        if (Topic is not null && !string.Equals(Topic, context.Topic, StringComparison.Ordinal))
            return false;
        if (Partition is not null && Partition != context.Partition)
            return false;

        return GroupId is null || string.Equals(GroupId, context.GroupId, StringComparison.Ordinal);
    }

    internal bool EqualsExactly(KafkaFaultScope other) =>
        Operation == other.Operation &&
        string.Equals(Topic, other.Topic, StringComparison.Ordinal) &&
        Partition == other.Partition &&
        string.Equals(GroupId, other.GroupId, StringComparison.Ordinal);

    internal void Validate()
    {
        if (Operation is < KafkaFaultOperation.Produce or > KafkaFaultOperation.ShareAcknowledge)
            throw new ArgumentOutOfRangeException(nameof(Operation), Operation, "Unknown fault operation.");
    }
}

/// <summary>
/// Describes a fault-plan entry at the instant it is consumed.
/// </summary>
public readonly struct KafkaFaultObservation
{
    internal KafkaFaultObservation(
        KafkaFaultScope ruleScope,
        KafkaFaultScope operationScope,
        KafkaFaultAction action,
        Exception? exception,
        bool isPersistent,
        int? remainingOccurrences)
    {
        RuleScope = ruleScope;
        OperationScope = operationScope;
        Action = action;
        Exception = exception;
        IsPersistent = isPersistent;
        RemainingOccurrences = remainingOccurrences;
    }

    /// <summary>Gets the configured rule scope.</summary>
    public KafkaFaultScope RuleScope { get; }

    /// <summary>Gets the concrete operation scope that matched.</summary>
    public KafkaFaultScope OperationScope { get; }

    /// <summary>Gets the consumed action.</summary>
    public KafkaFaultAction Action { get; }

    /// <summary>Gets the configured exception for a throw action.</summary>
    public Exception? Exception { get; }

    /// <summary>Gets whether the consumed rule remains persistent.</summary>
    public bool IsPersistent { get; }

    /// <summary>Gets the remaining occurrences, or null for a persistent rule.</summary>
    public int? RemainingOccurrences { get; }
}

/// <summary>
/// Deterministic pause point used to coordinate race tests without timing delays.
/// </summary>
public sealed class KafkaFaultBarrier
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal KafkaFaultBarrier()
    {
    }

    /// <summary>
    /// Gets whether the barrier has been released.
    /// </summary>
    public bool IsReleased => _released.Task.IsCompleted;

    /// <summary>
    /// Waits until an operation consumes this barrier.
    /// </summary>
    /// <exception cref="TaskCanceledException">
    /// The barrier was removed from its fault plan before an operation consumed it.
    /// </exception>
    public ValueTask WaitUntilEnteredAsync(CancellationToken cancellationToken = default)
    {
        if (_entered.Task.IsCompletedSuccessfully)
            return ValueTask.CompletedTask;

        return WaitAsync(_entered.Task, cancellationToken);
    }

    /// <summary>
    /// Releases the paused operation. Returns false when already released.
    /// </summary>
    public bool Release() => _released.TrySetResult();

    internal void ClearBeforeEntry()
    {
        _entered.TrySetCanceled();
        _released.TrySetResult();
    }

    internal ValueTask EnterAsync(CancellationToken cancellationToken)
    {
        _entered.TrySetResult();
        if (_released.Task.IsCompletedSuccessfully)
            return ValueTask.CompletedTask;

        return WaitAsync(_released.Task, cancellationToken);
    }

    private static async ValueTask WaitAsync(Task task, CancellationToken cancellationToken) =>
        await task.WaitAsync(cancellationToken).ConfigureAwait(false);
}

/// <summary>
/// Thread-safe ordered fault script for Dekaf in-memory clients.
/// </summary>
public interface IKafkaFaultPlan
{
    /// <summary>
    /// Raised synchronously after a matching entry is consumed and before its action runs.
    /// </summary>
    event Action<KafkaFaultObservation>? FaultConsumed;

    /// <summary>
    /// Gets the number of queued entries. A next-N failure is one entry.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Appends a failure consumed by the next matching operations.
    /// </summary>
    void Fail(KafkaFaultScope scope, Exception exception, int occurrenceCount = 1);

    /// <summary>
    /// Appends a failure that every matching operation consumes until it is cleared.
    /// </summary>
    void FailPersistently(KafkaFaultScope scope, Exception exception);

    /// <summary>
    /// Appends a one-shot deterministic barrier and returns its controller.
    /// </summary>
    KafkaFaultBarrier PauseNext(KafkaFaultScope scope);

    /// <summary>
    /// Applies the first matching scripted entry, or completes synchronously when none matches.
    /// </summary>
    ValueTask ApplyAsync(
        KafkaFaultScope operationScope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes entries configured with exactly the supplied scope and releases their barriers.
    /// </summary>
    int Clear(KafkaFaultScope scope);

    /// <summary>
    /// Removes every entry and releases all queued barriers.
    /// </summary>
    int Clear();
}

/// <summary>
/// Default thread-safe <see cref="IKafkaFaultPlan"/> implementation.
/// </summary>
public sealed class KafkaFaultPlan : IKafkaFaultPlan
{
    private readonly object _gate = new();
    private readonly List<FaultEntry> _entries = [];
    private ProduceFaultIndex _produceFaultIndex = ProduceFaultIndex.Empty;
    private ShareFaultIndex _shareFaultIndex = ShareFaultIndex.Empty;
    private int _hasEntries;
    private int _shareFaultIndexVersion;

    internal bool HasPotentialProduceMatch(KafkaFaultOperation operation, string topic) =>
        Volatile.Read(ref _produceFaultIndex).Matches(operation, topic);

    internal int ShareFaultIndexVersion => Volatile.Read(ref _shareFaultIndexVersion);

    internal bool HasPotentialShareMatch(
        KafkaFaultOperation operation,
        string groupId,
        HashSet<TopicPartition> assignment) =>
        Volatile.Read(ref _shareFaultIndex).Matches(operation, groupId, assignment);

    internal bool HasPotentialShareMatch(
        KafkaFaultOperation operation,
        string topic,
        int partition,
        string groupId) =>
        Volatile.Read(ref _shareFaultIndex).Matches(operation, topic, partition, groupId);

    /// <summary>
    /// Raised synchronously after a matching entry is consumed and before its action runs.
    /// </summary>
    public event Action<KafkaFaultObservation>? FaultConsumed;

    /// <summary>
    /// Gets the number of queued entries. A next-N failure is one entry.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
                return _entries.Count;
        }
    }

    /// <summary>
    /// Appends a failure consumed by the next matching operations.
    /// </summary>
    public void Fail(KafkaFaultScope scope, Exception exception, int occurrenceCount = 1)
    {
        scope.Validate();
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentOutOfRangeException.ThrowIfLessThan(occurrenceCount, 1);

        lock (_gate)
        {
            _entries.Add(FaultEntry.Failure(scope, exception, occurrenceCount, isPersistent: false));
            Volatile.Write(ref _hasEntries, 1);
            PublishFaultIndexesUnderLock();
        }
    }

    /// <summary>
    /// Appends a failure that every matching operation consumes until it is cleared.
    /// </summary>
    public void FailPersistently(KafkaFaultScope scope, Exception exception)
    {
        scope.Validate();
        ArgumentNullException.ThrowIfNull(exception);

        lock (_gate)
        {
            _entries.Add(FaultEntry.Failure(scope, exception, remainingOccurrences: 0, isPersistent: true));
            Volatile.Write(ref _hasEntries, 1);
            PublishFaultIndexesUnderLock();
        }
    }

    /// <summary>
    /// Appends a one-shot deterministic barrier and returns its controller.
    /// </summary>
    public KafkaFaultBarrier PauseNext(KafkaFaultScope scope)
    {
        scope.Validate();
        var barrier = new KafkaFaultBarrier();
        lock (_gate)
        {
            _entries.Add(FaultEntry.Pause(scope, barrier));
            Volatile.Write(ref _hasEntries, 1);
            PublishFaultIndexesUnderLock();
        }

        return barrier;
    }

    /// <summary>
    /// Applies the first matching scripted entry, or completes synchronously when none matches.
    /// </summary>
    public ValueTask ApplyAsync(
        KafkaFaultScope operationScope,
        CancellationToken cancellationToken = default)
    {
        operationScope.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        if (Volatile.Read(ref _hasEntries) == 0)
            return ValueTask.CompletedTask;

        FaultEntry? entry = null;
        KafkaFaultObservation observation = default;
        Action<KafkaFaultObservation>? observer = null;
        lock (_gate)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                var candidate = _entries[i];
                if (!candidate.Scope.Matches(operationScope))
                    continue;

                entry = candidate;
                if (!candidate.IsPersistent)
                {
                    candidate.RemainingOccurrences--;
                    if (candidate.RemainingOccurrences == 0)
                    {
                        _entries.RemoveAt(i);
                        if (_entries.Count == 0)
                            Volatile.Write(ref _hasEntries, 0);
                        PublishFaultIndexesUnderLock();
                    }
                }

                observation = new KafkaFaultObservation(
                    candidate.Scope,
                    operationScope,
                    candidate.Action,
                    candidate.Exception,
                    candidate.IsPersistent,
                    candidate.IsPersistent ? null : candidate.RemainingOccurrences);
                observer = FaultConsumed;
                break;
            }
        }

        if (entry is null)
            return ValueTask.CompletedTask;

        observer?.Invoke(observation);
        if (entry.Exception is { } exception)
            return ValueTask.FromException(exception);

        return entry.Barrier!.EnterAsync(cancellationToken);
    }

    /// <summary>
    /// Removes entries configured with exactly the supplied scope and releases their barriers.
    /// </summary>
    public int Clear(KafkaFaultScope scope)
    {
        scope.Validate();
        var removed = 0;
        lock (_gate)
        {
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                if (!entry.Scope.EqualsExactly(scope))
                    continue;

                _entries.RemoveAt(i);
                entry.Barrier?.ClearBeforeEntry();
                removed++;
            }

            if (_entries.Count == 0)
                Volatile.Write(ref _hasEntries, 0);
            if (removed != 0)
                PublishFaultIndexesUnderLock();
        }

        return removed;
    }

    /// <summary>
    /// Removes every entry and releases all queued barriers.
    /// </summary>
    public int Clear()
    {
        lock (_gate)
        {
            var removed = _entries.Count;
            for (var i = 0; i < _entries.Count; i++)
                _entries[i].Barrier?.ClearBeforeEntry();

            _entries.Clear();
            Volatile.Write(ref _hasEntries, 0);
            Volatile.Write(ref _produceFaultIndex, ProduceFaultIndex.Empty);
            Volatile.Write(ref _shareFaultIndex, ShareFaultIndex.Empty);
            Interlocked.Increment(ref _shareFaultIndexVersion);
            return removed;
        }
    }

    private void PublishFaultIndexesUnderLock()
    {
        var produceScopes = new ProduceScopeBuilder();
        var transactionProduceScopes = new ProduceScopeBuilder();
        var shareConsumeScopes = new ShareScopeBuilder();
        var shareAcknowledgeScopes = new ShareScopeBuilder();
        for (var entryIndex = 0; entryIndex < _entries.Count; entryIndex++)
        {
            var scope = _entries[entryIndex].Scope;

            switch (scope.Operation)
            {
                case KafkaFaultOperation.Produce:
                    if (scope.GroupId is null)
                        produceScopes.Add(scope.Topic);
                    break;
                case KafkaFaultOperation.TransactionProduce:
                    if (scope.GroupId is null)
                        transactionProduceScopes.Add(scope.Topic);
                    break;
                case KafkaFaultOperation.ShareConsume:
                    shareConsumeScopes.Add(scope);
                    break;
                case KafkaFaultOperation.ShareAcknowledge:
                    shareAcknowledgeScopes.Add(scope);
                    break;
            }
        }

        var produceFaultIndex = produceScopes.HasScopes || transactionProduceScopes.HasScopes
            ? new ProduceFaultIndex(
                produceScopes.AllTopics,
                produceScopes.SingleTopic,
                produceScopes.Topics,
                transactionProduceScopes.AllTopics,
                transactionProduceScopes.SingleTopic,
                transactionProduceScopes.Topics)
            : ProduceFaultIndex.Empty;
        Volatile.Write(ref _produceFaultIndex, produceFaultIndex);
        var shareFaultIndex = shareConsumeScopes.HasScopes || shareAcknowledgeScopes.HasScopes
            ? new ShareFaultIndex(
                shareConsumeScopes.Build(),
                shareAcknowledgeScopes.Build())
            : ShareFaultIndex.Empty;
        Volatile.Write(ref _shareFaultIndex, shareFaultIndex);
        Interlocked.Increment(ref _shareFaultIndexVersion);
    }

    private struct ProduceScopeBuilder
    {
        internal bool AllTopics;
        internal string? SingleTopic;
        internal HashSet<string>? Topics;

        internal readonly bool HasScopes => AllTopics || SingleTopic is not null || Topics is not null;

        internal void Add(string? topic)
        {
            if (topic is null)
            {
                AllTopics = true;
                return;
            }

            if (Topics is not null)
            {
                Topics.Add(topic);
                return;
            }

            if (SingleTopic is null)
            {
                SingleTopic = topic;
                return;
            }

            if (string.Equals(SingleTopic, topic, StringComparison.Ordinal))
                return;

            Topics = new HashSet<string>(StringComparer.Ordinal) { SingleTopic, topic };
            SingleTopic = null;
        }
    }

    private struct ShareScopeBuilder
    {
        private bool _all;
        private HashSet<string>? _topics;
        private HashSet<int>? _partitions;
        private HashSet<string>? _groups;
        private HashSet<TopicPartition>? _topicPartitions;
        private HashSet<TopicGroupKey>? _topicGroups;
        private HashSet<PartitionGroupKey>? _partitionGroups;
        private HashSet<ExactShareKey>? _exactScopes;

        internal readonly bool HasScopes =>
            _all ||
            _topics is not null ||
            _partitions is not null ||
            _groups is not null ||
            _topicPartitions is not null ||
            _topicGroups is not null ||
            _partitionGroups is not null ||
            _exactScopes is not null;

        internal void Add(KafkaFaultScope scope)
        {
            switch (scope)
            {
                case { Topic: null, Partition: null, GroupId: null }:
                    _all = true;
                    break;
                case { Topic: { } topic, Partition: null, GroupId: null }:
                    (_topics ??= new(StringComparer.Ordinal)).Add(topic);
                    break;
                case { Topic: null, Partition: { } partition, GroupId: null }:
                    (_partitions ??= []).Add(partition);
                    break;
                case { Topic: null, Partition: null, GroupId: { } groupId }:
                    (_groups ??= new(StringComparer.Ordinal)).Add(groupId);
                    break;
                case { Topic: { } topic, Partition: { } partition, GroupId: null }:
                    (_topicPartitions ??= []).Add(new TopicPartition(topic, partition));
                    break;
                case { Topic: { } topic, Partition: null, GroupId: { } groupId }:
                    (_topicGroups ??= []).Add(new TopicGroupKey(topic, groupId));
                    break;
                case { Topic: null, Partition: { } partition, GroupId: { } groupId }:
                    (_partitionGroups ??= []).Add(new PartitionGroupKey(partition, groupId));
                    break;
                case { Topic: { } topic, Partition: { } partition, GroupId: { } groupId }:
                    (_exactScopes ??= []).Add(new ExactShareKey(topic, partition, groupId));
                    break;
            }
        }

        internal readonly ShareScopeIndex Build() => new(
            _all,
            _topics,
            _partitions,
            _groups,
            _topicPartitions,
            _topicGroups,
            _partitionGroups,
            _exactScopes);
    }

    private sealed class ProduceFaultIndex(
        bool allProduceTopics,
        string? produceTopic,
        HashSet<string>? produceTopics,
        bool allTransactionProduceTopics,
        string? transactionProduceTopic,
        HashSet<string>? transactionProduceTopics)
    {
        public static ProduceFaultIndex Empty { get; } = new(false, null, null, false, null, null);

        public bool Matches(KafkaFaultOperation operation, string topic) => operation switch
        {
            KafkaFaultOperation.Produce =>
                allProduceTopics ||
                string.Equals(produceTopic, topic, StringComparison.Ordinal) ||
                produceTopics is not null && produceTopics.Contains(topic),
            KafkaFaultOperation.TransactionProduce =>
                allTransactionProduceTopics ||
                string.Equals(transactionProduceTopic, topic, StringComparison.Ordinal) ||
                transactionProduceTopics is not null && transactionProduceTopics.Contains(topic),
            _ => false
        };
    }

    private sealed class ShareFaultIndex(
        ShareScopeIndex consumeScopes,
        ShareScopeIndex acknowledgeScopes)
    {
        public static ShareFaultIndex Empty { get; } = new(ShareScopeIndex.Empty, ShareScopeIndex.Empty);

        public bool Matches(
            KafkaFaultOperation operation,
            string groupId,
            HashSet<TopicPartition> assignment)
        {
            var scopes = operation switch
            {
                KafkaFaultOperation.ShareConsume => consumeScopes,
                KafkaFaultOperation.ShareAcknowledge => acknowledgeScopes,
                _ => ShareScopeIndex.Empty
            };
            return scopes.Matches(groupId, assignment);
        }

        public bool Matches(
            KafkaFaultOperation operation,
            string topic,
            int partition,
            string groupId)
        {
            var scopes = operation switch
            {
                KafkaFaultOperation.ShareConsume => consumeScopes,
                KafkaFaultOperation.ShareAcknowledge => acknowledgeScopes,
                _ => ShareScopeIndex.Empty
            };
            return scopes.Matches(topic, partition, groupId);
        }
    }

    private sealed class ShareScopeIndex(
        bool all,
        HashSet<string>? topics,
        HashSet<int>? partitions,
        HashSet<string>? groups,
        HashSet<TopicPartition>? topicPartitions,
        HashSet<TopicGroupKey>? topicGroups,
        HashSet<PartitionGroupKey>? partitionGroups,
        HashSet<ExactShareKey>? exactScopes)
    {
        public static ShareScopeIndex Empty { get; } = new(
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        public bool Matches(string groupId, HashSet<TopicPartition> assignment)
        {
            if (assignment.Count == 0)
                return false;

            foreach (var partition in assignment)
            {
                if (Matches(partition.Topic, partition.Partition, groupId))
                    return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(string topic, int partition, string groupId) =>
            all ||
            topics is not null && topics.Contains(topic) ||
            partitions is not null && partitions.Contains(partition) ||
            groups is not null && groups.Contains(groupId) ||
            topicPartitions is not null && topicPartitions.Contains(new TopicPartition(topic, partition)) ||
            topicGroups is not null && topicGroups.Contains(new TopicGroupKey(topic, groupId)) ||
            partitionGroups is not null && partitionGroups.Contains(new PartitionGroupKey(partition, groupId)) ||
            exactScopes is not null && exactScopes.Contains(new ExactShareKey(topic, partition, groupId));
    }

    private readonly record struct TopicGroupKey(string Topic, string GroupId);

    private readonly record struct PartitionGroupKey(int Partition, string GroupId);

    private readonly record struct ExactShareKey(string Topic, int Partition, string GroupId);

    private sealed class FaultEntry
    {
        private FaultEntry(
            KafkaFaultScope scope,
            KafkaFaultAction action,
            Exception? exception,
            KafkaFaultBarrier? barrier,
            int remainingOccurrences,
            bool isPersistent)
        {
            Scope = scope;
            Action = action;
            Exception = exception;
            Barrier = barrier;
            RemainingOccurrences = remainingOccurrences;
            IsPersistent = isPersistent;
        }

        public KafkaFaultScope Scope { get; }
        public KafkaFaultAction Action { get; }
        public Exception? Exception { get; }
        public KafkaFaultBarrier? Barrier { get; }
        public int RemainingOccurrences { get; set; }
        public bool IsPersistent { get; }

        public static FaultEntry Failure(
            KafkaFaultScope scope,
            Exception exception,
            int remainingOccurrences,
            bool isPersistent) =>
            new(scope, KafkaFaultAction.Throw, exception, null, remainingOccurrences, isPersistent);

        public static FaultEntry Pause(KafkaFaultScope scope, KafkaFaultBarrier barrier) =>
            new(scope, KafkaFaultAction.Pause, null, barrier, remainingOccurrences: 1, isPersistent: false);
    }
}
