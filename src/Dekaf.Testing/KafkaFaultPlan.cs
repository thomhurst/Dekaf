using System.Collections.Concurrent;
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
        if (operation is KafkaFaultOperation.JoinGroup or
            KafkaFaultOperation.SyncGroup or
            KafkaFaultOperation.Rebalance)
        {
            if (topic is not null)
            {
                throw new ArgumentException(
                    "Consumer-group transition faults do not support topic selectors.",
                    nameof(topic));
            }

            if (partition is not null)
            {
                throw new ArgumentException(
                    "Consumer-group transition faults do not support partition selectors.",
                    nameof(partition));
            }
        }

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
    /// Gets a value that changes whenever the queued fault script changes.
    /// </summary>
    /// <remarks>
    /// Implementations must expose this value thread-safely and change it whenever matching results can change.
    /// </remarks>
    long Version { get; }

    /// <summary>
    /// Returns whether the supplied concrete operation scope matches a queued entry.
    /// </summary>
    bool HasMatchingFault(in KafkaFaultScope operationScope);

    /// <summary>
    /// Returns whether an operation can match a queued entry for the supplied group and resources.
    /// </summary>
    bool HasPotentialFault(
        KafkaFaultOperation operation,
        string? groupId,
        IReadOnlySet<TopicPartition> resources);

    /// <summary>
    /// Selects the supplied concrete operation scope matched by the earliest queued entry.
    /// </summary>
    bool TryGetFirstMatchingFaultScope(
        ReadOnlySpan<KafkaFaultScope> operationScopes,
        out KafkaFaultScope operationScope);

    /// <summary>
    /// Atomically consumes and applies the earliest entry matching any supplied operation scope.
    /// </summary>
    /// <returns>
    /// True when an entry was consumed; when true, the caller must await <paramref name="application"/>.
    /// </returns>
    bool TryApplyFirstMatchingFault(
        ReadOnlySpan<KafkaFaultScope> operationScopes,
        out ValueTask application,
        CancellationToken cancellationToken = default);

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
    private FaultScopeIndex _scopeIndex = FaultScopeIndex.Empty;
    private int _count;
    private int _hasEntries;
    private int _shareFaultIndexVersion;

    internal bool HasPotentialProduceMatch(KafkaFaultOperation operation, string topic) =>
        Volatile.Read(ref _produceFaultIndex).Matches(operation, topic);

    internal int ShareFaultIndexVersion => Volatile.Read(ref _shareFaultIndexVersion);

    internal int RetainedShareFaultSelectorCount =>
        Volatile.Read(ref _shareFaultIndex).RetainedSelectorCount;

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
    public int Count => Volatile.Read(ref _count);

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
            Volatile.Write(ref _count, _entries.Count);
            Volatile.Write(ref _hasEntries, 1);
            AddFaultIndexUnderLock(scope);
            PublishScopeIndexUnderLock();
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
            Volatile.Write(ref _count, _entries.Count);
            Volatile.Write(ref _hasEntries, 1);
            AddFaultIndexUnderLock(scope);
            PublishScopeIndexUnderLock();
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
            Volatile.Write(ref _count, _entries.Count);
            Volatile.Write(ref _hasEntries, 1);
            AddFaultIndexUnderLock(scope);
            PublishScopeIndexUnderLock();
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

        if (!TryConsumeFirstMatchingEntry(
                operationScope,
                out var entry,
                out var observation,
                out var observer))
        {
            return ValueTask.CompletedTask;
        }

        return ApplyConsumedEntry(entry, observation, observer, cancellationToken);
    }

    /// <inheritdoc />
    public bool TryApplyFirstMatchingFault(
        ReadOnlySpan<KafkaFaultScope> operationScopes,
        out ValueTask application,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        for (var i = 0; i < operationScopes.Length; i++)
            operationScopes[i].Validate();

        FaultEntry? entry = null;
        KafkaFaultObservation observation = default;
        Action<KafkaFaultObservation>? observer = null;
        lock (_gate)
        {
            for (var entryIndex = 0; entryIndex < _entries.Count && entry is null; entryIndex++)
            {
                var candidate = _entries[entryIndex];
                for (var scopeIndex = 0; scopeIndex < operationScopes.Length; scopeIndex++)
                {
                    var operationScope = operationScopes[scopeIndex];
                    if (!candidate.Scope.Matches(operationScope))
                        continue;

                    ConsumeEntryUnderLock(
                        entryIndex,
                        operationScope,
                        out entry,
                        out observation,
                        out observer);
                    break;
                }
            }
        }

        if (entry is null)
        {
            application = ValueTask.CompletedTask;
            return false;
        }

        application = ApplyConsumedEntry(entry, observation, observer, cancellationToken);
        return true;
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
                RemoveFaultIndexUnderLock(entry.Scope);
                entry.Barrier?.ClearBeforeEntry();
                removed++;
            }

            Volatile.Write(ref _count, _entries.Count);
            if (_entries.Count == 0)
                Volatile.Write(ref _hasEntries, 0);
            if (removed != 0)
                PublishScopeIndexUnderLock();
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
            Volatile.Write(ref _count, 0);
            Volatile.Write(ref _hasEntries, 0);
            Volatile.Write(ref _produceFaultIndex, ProduceFaultIndex.Empty);
            if (!ReferenceEquals(Volatile.Read(ref _shareFaultIndex), ShareFaultIndex.Empty))
            {
                Volatile.Write(ref _shareFaultIndex, ShareFaultIndex.Empty);
                Interlocked.Increment(ref _shareFaultIndexVersion);
            }
            PublishScopeIndexUnderLock();
            return removed;
        }
    }

    private void AddFaultIndexUnderLock(KafkaFaultScope scope)
    {
        switch (scope.Operation)
        {
            case KafkaFaultOperation.Produce:
            case KafkaFaultOperation.TransactionProduce:
                PublishProduceFaultIndexUnderLock();
                break;
            case KafkaFaultOperation.ShareConsume:
            case KafkaFaultOperation.ShareAcknowledge:
                var index = Volatile.Read(ref _shareFaultIndex);
                if (ReferenceEquals(index, ShareFaultIndex.Empty))
                    index = new ShareFaultIndex();

                index = index.Add(scope, out var addedEligibility);
                if (addedEligibility)
                {
                    Volatile.Write(ref _shareFaultIndex, index);
                    Interlocked.Increment(ref _shareFaultIndexVersion);
                }
                break;
        }
    }

    private void RemoveFaultIndexUnderLock(KafkaFaultScope scope)
    {
        switch (scope.Operation)
        {
            case KafkaFaultOperation.Produce:
            case KafkaFaultOperation.TransactionProduce:
                PublishProduceFaultIndexUnderLock();
                break;
            case KafkaFaultOperation.ShareConsume:
            case KafkaFaultOperation.ShareAcknowledge:
                var index = Volatile.Read(ref _shareFaultIndex);
                if (ReferenceEquals(index, ShareFaultIndex.Empty) || !index.Remove(scope))
                    break;

                if (index.IsEmpty)
                    index = ShareFaultIndex.Empty;
                Volatile.Write(ref _shareFaultIndex, index);
                Interlocked.Increment(ref _shareFaultIndexVersion);
                break;
        }
    }

    private void PublishProduceFaultIndexUnderLock()
    {
        var produceScopes = new ProduceScopeBuilder();
        var transactionProduceScopes = new ProduceScopeBuilder();
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

    private sealed class ShareFaultIndex
    {
        private readonly ShareScopeIndex _consumeScopes;
        private readonly ShareScopeIndex _acknowledgeScopes;

        public ShareFaultIndex()
            : this(new ShareScopeIndex(), new ShareScopeIndex())
        {
        }

        private ShareFaultIndex(
            ShareScopeIndex consumeScopes,
            ShareScopeIndex acknowledgeScopes)
        {
            _consumeScopes = consumeScopes;
            _acknowledgeScopes = acknowledgeScopes;
        }

        public static ShareFaultIndex Empty { get; } = new(ShareScopeIndex.Empty, ShareScopeIndex.Empty);

        public bool IsEmpty => _consumeScopes.IsEmpty && _acknowledgeScopes.IsEmpty;

        public int RetainedSelectorCount =>
            _consumeScopes.RetainedSelectorCount + _acknowledgeScopes.RetainedSelectorCount;

        public ShareFaultIndex Add(KafkaFaultScope scope, out bool eligibilityChanged)
        {
            var scopes = GetScopes(scope.Operation);
            var updatedScopes = scopes.Add(scope, out eligibilityChanged);
            if (ReferenceEquals(scopes, updatedScopes))
                return this;

            return scope.Operation == KafkaFaultOperation.ShareConsume
                ? new ShareFaultIndex(updatedScopes, _acknowledgeScopes)
                : new ShareFaultIndex(_consumeScopes, updatedScopes);
        }

        public bool Remove(KafkaFaultScope scope) => GetScopes(scope.Operation).Remove(scope);

        public bool Matches(
            KafkaFaultOperation operation,
            string groupId,
            HashSet<TopicPartition> assignment)
        {
            return GetScopes(operation).Matches(groupId, assignment);
        }

        public bool Matches(
            KafkaFaultOperation operation,
            string topic,
            int partition,
            string groupId)
        {
            return GetScopes(operation).Matches(topic, partition, groupId);
        }

        private ShareScopeIndex GetScopes(KafkaFaultOperation operation) => operation switch
        {
            KafkaFaultOperation.ShareConsume => _consumeScopes,
            KafkaFaultOperation.ShareAcknowledge => _acknowledgeScopes,
            _ => ShareScopeIndex.Empty
        };
    }

    private sealed class ShareScopeIndex
    {
        private ConcurrentDictionary<string, ScopeCounter>? _topics;
        private ConcurrentDictionary<int, ScopeCounter>? _partitions;
        private ConcurrentDictionary<string, ScopeCounter>? _groups;
        private ConcurrentDictionary<TopicPartition, ScopeCounter>? _topicPartitions;
        private ConcurrentDictionary<TopicGroupKey, ScopeCounter>? _topicGroups;
        private ConcurrentDictionary<PartitionGroupKey, ScopeCounter>? _partitionGroups;
        private ConcurrentDictionary<ExactShareKey, ScopeCounter>? _exactScopes;
        private int _activeScopeCount;
        private int _allCount;

        public ShareScopeIndex()
        {
        }

        public static ShareScopeIndex Empty { get; } = new();

        private ShareScopeIndex(ShareScopeIndex source)
        {
            _allCount = Volatile.Read(ref source._allCount);
            _activeScopeCount = Volatile.Read(ref source._activeScopeCount);
            _topics = Clone(source._topics, StringComparer.Ordinal);
            _partitions = Clone(source._partitions);
            _groups = Clone(source._groups, StringComparer.Ordinal);
            _topicPartitions = Clone(source._topicPartitions);
            _topicGroups = Clone(source._topicGroups);
            _partitionGroups = Clone(source._partitionGroups);
            _exactScopes = Clone(source._exactScopes);
        }

        public bool IsEmpty => Volatile.Read(ref _activeScopeCount) == 0;

        public int RetainedSelectorCount =>
            (Volatile.Read(ref _allCount) == 0 ? 0 : 1) +
            (_topics?.Count ?? 0) +
            (_partitions?.Count ?? 0) +
            (_groups?.Count ?? 0) +
            (_topicPartitions?.Count ?? 0) +
            (_topicGroups?.Count ?? 0) +
            (_partitionGroups?.Count ?? 0) +
            (_exactScopes?.Count ?? 0);

        public ShareScopeIndex Add(KafkaFaultScope scope, out bool eligibilityChanged)
        {
            if (scope is { Topic: null, Partition: null, GroupId: null })
            {
                eligibilityChanged = _allCount == 0;
                _allCount++;
                if (eligibilityChanged)
                    _activeScopeCount++;
                return this;
            }

            if (TryGetCounter(scope, out var counter))
            {
                var count = Volatile.Read(ref counter.Count);
                eligibilityChanged = count == 0;
                Volatile.Write(ref counter.Count, count + 1);
                if (eligibilityChanged)
                    _activeScopeCount++;
                return this;
            }

            var updated = new ShareScopeIndex(this);
            updated.AddNew(scope);
            eligibilityChanged = true;
            return updated;
        }

        public bool Remove(KafkaFaultScope scope)
        {
            if (scope is { Topic: null, Partition: null, GroupId: null })
            {
                _allCount--;
                if (_allCount != 0)
                    return false;

                _activeScopeCount--;
                return true;
            }

            if (!TryGetCounter(scope, out var counter))
                return false;

            var count = Volatile.Read(ref counter.Count) - 1;
            Volatile.Write(ref counter.Count, count);
            if (count != 0)
                return false;

            RemoveCounter(scope);
            _activeScopeCount--;
            return true;
        }

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
            Volatile.Read(ref _allCount) != 0 ||
            IsActive(_topics, topic) ||
            IsActive(_partitions, partition) ||
            IsActive(_groups, groupId) ||
            IsActive(_topicPartitions, new TopicPartition(topic, partition)) ||
            IsActive(_topicGroups, new TopicGroupKey(topic, groupId)) ||
            IsActive(_partitionGroups, new PartitionGroupKey(partition, groupId)) ||
            IsActive(_exactScopes, new ExactShareKey(topic, partition, groupId));

        private void AddNew(KafkaFaultScope scope)
        {
            var counter = new ScopeCounter();
            switch (scope)
            {
                case { Topic: null, Partition: null, GroupId: null }:
                    _allCount = 1;
                    break;
                case { Topic: { } topic, Partition: null, GroupId: null }:
                    (_topics ??= new(StringComparer.Ordinal)).TryAdd(topic, counter);
                    break;
                case { Topic: null, Partition: { } partition, GroupId: null }:
                    (_partitions ??= []).TryAdd(partition, counter);
                    break;
                case { Topic: null, Partition: null, GroupId: { } groupId }:
                    (_groups ??= new(StringComparer.Ordinal)).TryAdd(groupId, counter);
                    break;
                case { Topic: { } topic, Partition: { } partition, GroupId: null }:
                    (_topicPartitions ??= []).TryAdd(new TopicPartition(topic, partition), counter);
                    break;
                case { Topic: { } topic, Partition: null, GroupId: { } groupId }:
                    (_topicGroups ??= []).TryAdd(new TopicGroupKey(topic, groupId), counter);
                    break;
                case { Topic: null, Partition: { } partition, GroupId: { } groupId }:
                    (_partitionGroups ??= []).TryAdd(new PartitionGroupKey(partition, groupId), counter);
                    break;
                case { Topic: { } topic, Partition: { } partition, GroupId: { } groupId }:
                    (_exactScopes ??= []).TryAdd(new ExactShareKey(topic, partition, groupId), counter);
                    break;
            }

            _activeScopeCount++;
        }

        private bool TryGetCounter(KafkaFaultScope scope, out ScopeCounter counter)
        {
            switch (scope)
            {
                case { Topic: { } topic, Partition: null, GroupId: null }:
                    return TryGetCounter(_topics, topic, out counter);
                case { Topic: null, Partition: { } partition, GroupId: null }:
                    return TryGetCounter(_partitions, partition, out counter);
                case { Topic: null, Partition: null, GroupId: { } groupId }:
                    return TryGetCounter(_groups, groupId, out counter);
                case { Topic: { } topic, Partition: { } partition, GroupId: null }:
                    return TryGetCounter(_topicPartitions, new TopicPartition(topic, partition), out counter);
                case { Topic: { } topic, Partition: null, GroupId: { } groupId }:
                    return TryGetCounter(_topicGroups, new TopicGroupKey(topic, groupId), out counter);
                case { Topic: null, Partition: { } partition, GroupId: { } groupId }:
                    return TryGetCounter(_partitionGroups, new PartitionGroupKey(partition, groupId), out counter);
                case { Topic: { } topic, Partition: { } partition, GroupId: { } groupId }:
                    return TryGetCounter(_exactScopes, new ExactShareKey(topic, partition, groupId), out counter);
                default:
                    counter = null!;
                    return false;
            }
        }

        private void RemoveCounter(KafkaFaultScope scope)
        {
            switch (scope)
            {
                case { Topic: { } topic, Partition: null, GroupId: null }:
                    _topics!.TryRemove(topic, out _);
                    break;
                case { Topic: null, Partition: { } partition, GroupId: null }:
                    _partitions!.TryRemove(partition, out _);
                    break;
                case { Topic: null, Partition: null, GroupId: { } groupId }:
                    _groups!.TryRemove(groupId, out _);
                    break;
                case { Topic: { } topic, Partition: { } partition, GroupId: null }:
                    _topicPartitions!.TryRemove(new TopicPartition(topic, partition), out _);
                    break;
                case { Topic: { } topic, Partition: null, GroupId: { } groupId }:
                    _topicGroups!.TryRemove(new TopicGroupKey(topic, groupId), out _);
                    break;
                case { Topic: null, Partition: { } partition, GroupId: { } groupId }:
                    _partitionGroups!.TryRemove(new PartitionGroupKey(partition, groupId), out _);
                    break;
                case { Topic: { } topic, Partition: { } partition, GroupId: { } groupId }:
                    _exactScopes!.TryRemove(new ExactShareKey(topic, partition, groupId), out _);
                    break;
            }
        }

        private static bool IsActive<TKey>(ConcurrentDictionary<TKey, ScopeCounter>? counts, TKey key)
            where TKey : notnull
            => counts is not null &&
               counts.TryGetValue(key, out var counter) &&
               Volatile.Read(ref counter.Count) != 0;

        private static bool TryGetCounter<TKey>(
            ConcurrentDictionary<TKey, ScopeCounter>? counts,
            TKey key,
            out ScopeCounter counter)
            where TKey : notnull
        {
            if (counts is not null && counts.TryGetValue(key, out var existing))
            {
                counter = existing;
                return true;
            }

            counter = null!;
            return false;
        }

        private static ConcurrentDictionary<TKey, ScopeCounter>? Clone<TKey>(
            ConcurrentDictionary<TKey, ScopeCounter>? source,
            IEqualityComparer<TKey>? comparer = null)
            where TKey : notnull =>
            source is null ? null : new ConcurrentDictionary<TKey, ScopeCounter>(source, comparer);

        private sealed class ScopeCounter
        {
            public int Count = 1;
        }
    }

    private readonly record struct TopicGroupKey(string Topic, string GroupId);

    private readonly record struct PartitionGroupKey(int Partition, string GroupId);

    private readonly record struct ExactShareKey(string Topic, int Partition, string GroupId);

    /// <inheritdoc />
    public long Version => Volatile.Read(ref _scopeIndex).Version;

    internal bool HasPotentialMatch(KafkaFaultOperation operation, string? groupId) =>
        Volatile.Read(ref _scopeIndex).HasPotentialMatch(operation, groupId);

    /// <inheritdoc />
    public bool HasMatchingFault(in KafkaFaultScope operationScope) =>
        Volatile.Read(ref _scopeIndex).HasMatchingFault(operationScope);

    /// <inheritdoc />
    public bool HasPotentialFault(
        KafkaFaultOperation operation,
        string? groupId,
        IReadOnlySet<TopicPartition> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        return Volatile.Read(ref _scopeIndex).HasPotentialMatch(operation, groupId, resources);
    }

    /// <inheritdoc />
    public bool TryGetFirstMatchingFaultScope(
        ReadOnlySpan<KafkaFaultScope> operationScopes,
        out KafkaFaultScope operationScope) =>
        Volatile.Read(ref _scopeIndex).TryGetFirstMatchingFaultScope(
            operationScopes,
            out operationScope);

    internal bool TryApplyUnconditionalCommitFault(
        string groupId,
        out ValueTask application,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var operationScope = new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: groupId);
        if (!TryConsumeFirstMatchingEntry(
                operationScope,
                out var entry,
                out var observation,
                out var observer))
        {
            application = ValueTask.CompletedTask;
            return false;
        }

        application = ApplyConsumedEntry(entry, observation, observer, cancellationToken);
        return true;
    }

    internal bool TryApplyLeadingUnconditionalCommitFault(
        string groupId,
        out ValueTask application,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FaultEntry? entry = null;
        KafkaFaultObservation observation = default;
        Action<KafkaFaultObservation>? observer = null;
        var operationScope = new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: groupId);
        lock (_gate)
        {
            for (var entryIndex = 0; entryIndex < _entries.Count; entryIndex++)
            {
                var scope = _entries[entryIndex].Scope;
                if (scope.Operation != KafkaFaultOperation.Commit ||
                    scope.GroupId is not null && scope.GroupId != groupId)
                {
                    continue;
                }

                if (scope.Topic is not null || scope.Partition is not null)
                    break;

                ConsumeEntryUnderLock(
                    entryIndex,
                    operationScope,
                    out entry,
                    out observation,
                    out observer);
                break;
            }
        }

        if (entry is null)
        {
            application = ValueTask.CompletedTask;
            return false;
        }

        application = ApplyConsumedEntry(entry, observation, observer, cancellationToken);
        return true;
    }

    internal bool TryApplyFirstMatchingCommitFault(
        string groupId,
        IReadOnlyList<TopicPartitionOffset> offsets,
        out ValueTask application,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FaultEntry? entry = null;
        KafkaFaultObservation observation = default;
        Action<KafkaFaultObservation>? observer = null;
        lock (_gate)
        {
            for (var entryIndex = 0; entryIndex < _entries.Count && entry is null; entryIndex++)
            {
                var candidate = _entries[entryIndex];
                if (candidate.Scope.Operation != KafkaFaultOperation.Commit ||
                    candidate.Scope.GroupId is not null && candidate.Scope.GroupId != groupId)
                {
                    continue;
                }

                for (var offsetIndex = 0; offsetIndex < offsets.Count; offsetIndex++)
                {
                    var offset = offsets[offsetIndex];
                    var operationScope = new KafkaFaultScope(
                        KafkaFaultOperation.Commit,
                        offset.Topic,
                        offset.Partition,
                        groupId);
                    if (!candidate.Scope.Matches(operationScope))
                        continue;

                    ConsumeEntryUnderLock(
                        entryIndex,
                        operationScope,
                        out entry,
                        out observation,
                        out observer);
                    break;
                }
            }
        }

        if (entry is null)
        {
            application = ValueTask.CompletedTask;
            return false;
        }

        application = ApplyConsumedEntry(entry, observation, observer, cancellationToken);
        return true;
    }

    internal bool HasPotentialConsumerMatch(
        string? groupId,
        IReadOnlySet<TopicPartition> activeResources,
        IReadOnlySet<TopicPartition> ownedResources,
        bool includeCommit,
        out long scopeVersion)
    {
        var index = Volatile.Read(ref _scopeIndex);
        scopeVersion = index.Version;
        return index.HasPotentialMatch(KafkaFaultOperation.Fetch, groupId, activeResources) ||
               index.HasPotentialMatch(KafkaFaultOperation.Consume, groupId, activeResources) ||
               includeCommit &&
               index.HasPotentialMatch(KafkaFaultOperation.Commit, groupId, ownedResources);
    }

    private void PublishScopeIndexUnderLock()
    {
        var version = unchecked(Volatile.Read(ref _scopeIndex).Version + 1);
        Volatile.Write(ref _scopeIndex, new FaultScopeIndex(_entries, version));
    }

    private void ConsumeEntryUnderLock(
        int entryIndex,
        KafkaFaultScope operationScope,
        out FaultEntry entry,
        out KafkaFaultObservation observation,
        out Action<KafkaFaultObservation>? observer)
    {
        entry = _entries[entryIndex];
        if (!entry.IsPersistent)
        {
            entry.RemainingOccurrences--;
            if (entry.RemainingOccurrences == 0)
            {
                _entries.RemoveAt(entryIndex);
                Volatile.Write(ref _count, _entries.Count);
                if (_entries.Count == 0)
                    Volatile.Write(ref _hasEntries, 0);
                RemoveFaultIndexUnderLock(entry.Scope);
                PublishScopeIndexUnderLock();
            }
        }

        observation = new KafkaFaultObservation(
            entry.Scope,
            operationScope,
            entry.Action,
            entry.Exception,
            entry.IsPersistent,
            entry.IsPersistent ? null : entry.RemainingOccurrences);
        observer = FaultConsumed;
    }

    private bool TryConsumeFirstMatchingEntry(
        KafkaFaultScope operationScope,
        out FaultEntry entry,
        out KafkaFaultObservation observation,
        out Action<KafkaFaultObservation>? observer)
    {
        lock (_gate)
        {
            for (var entryIndex = 0; entryIndex < _entries.Count; entryIndex++)
            {
                if (!_entries[entryIndex].Scope.Matches(operationScope))
                    continue;

                ConsumeEntryUnderLock(
                    entryIndex,
                    operationScope,
                    out entry,
                    out observation,
                    out observer);
                return true;
            }
        }

        entry = null!;
        observation = default;
        observer = null;
        return false;
    }

    private static ValueTask ApplyConsumedEntry(
        FaultEntry entry,
        KafkaFaultObservation observation,
        Action<KafkaFaultObservation>? observer,
        CancellationToken cancellationToken)
    {
        observer?.Invoke(observation);
        if (entry.Exception is { } exception)
            return ValueTask.FromException(exception);

        return entry.Barrier!.EnterAsync(cancellationToken);
    }

    private sealed class FaultScopeIndex
    {
        internal static readonly FaultScopeIndex Empty = new();

        private readonly HashSet<OperationGroupKey> _operationGroups;
        private readonly HashSet<ScopeKey> _scopes;
        private readonly ScopeKey[] _orderedScopes;
        private readonly ulong _operations;

        private FaultScopeIndex()
        {
            _operationGroups = [];
            _scopes = [];
            _orderedScopes = [];
        }

        internal FaultScopeIndex(List<FaultEntry> entries, long version)
        {
            Version = version;
            _operationGroups = new HashSet<OperationGroupKey>(entries.Count);
            _scopes = new HashSet<ScopeKey>(entries.Count);
            _orderedScopes = new ScopeKey[entries.Count];
            for (var index = 0; index < entries.Count; index++)
            {
                var scope = entries[index].Scope;
                _operations |= 1UL << (int)scope.Operation;
                _operationGroups.Add(new OperationGroupKey(scope.Operation, scope.GroupId));
                var key = new ScopeKey(
                    scope.Operation,
                    scope.Topic,
                    scope.Partition,
                    scope.GroupId);
                _scopes.Add(key);
                _orderedScopes[index] = key;
            }
        }

        internal long Version { get; }

        internal bool HasPotentialMatch(KafkaFaultOperation operation, string? groupId)
        {
            if ((_operations & (1UL << (int)operation)) == 0)
                return false;

            return _operationGroups.Contains(new OperationGroupKey(operation, null)) ||
                   (groupId is not null &&
                    _operationGroups.Contains(new OperationGroupKey(operation, groupId)));
        }

        internal bool HasPotentialMatch(
            KafkaFaultOperation operation,
            string? groupId,
            IReadOnlySet<TopicPartition> assignment)
        {
            if (!HasPotentialMatch(operation, groupId))
                return false;

            foreach (var scope in _scopes)
            {
                if (scope.Operation != operation ||
                    scope.GroupId is not null && scope.GroupId != groupId)
                {
                    continue;
                }

                if (scope.Topic is null && scope.Partition is null)
                    return true;

                foreach (var assigned in assignment)
                {
                    if ((scope.Topic is null || scope.Topic == assigned.Topic) &&
                        (scope.Partition is null || scope.Partition == assigned.Partition))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal bool HasMatchingFault(in KafkaFaultScope operationScope)
        {
            if (!HasPotentialMatch(operationScope.Operation, operationScope.GroupId))
                return false;

            for (var selectors = 0; selectors < 8; selectors++)
            {
                var key = new ScopeKey(
                    operationScope.Operation,
                    (selectors & 1) == 0 ? null : operationScope.Topic,
                    (selectors & 2) == 0 ? null : operationScope.Partition,
                    (selectors & 4) == 0 ? null : operationScope.GroupId);
                if (_scopes.Contains(key))
                    return true;
            }

            return false;
        }

        internal bool TryGetFirstMatchingFaultScope(
            ReadOnlySpan<KafkaFaultScope> operationScopes,
            out KafkaFaultScope operationScope)
        {
            for (var scopeIndex = 0; scopeIndex < _orderedScopes.Length; scopeIndex++)
            {
                var ruleScope = _orderedScopes[scopeIndex];
                for (var operationIndex = 0; operationIndex < operationScopes.Length; operationIndex++)
                {
                    var candidate = operationScopes[operationIndex];
                    if (!Matches(ruleScope, candidate))
                        continue;

                    operationScope = candidate;
                    return true;
                }
            }

            operationScope = default;
            return false;
        }

        private static bool Matches(ScopeKey ruleScope, KafkaFaultScope candidate)
        {
            if (ruleScope.Operation != candidate.Operation)
                return false;
            if (ruleScope.GroupId is not null && ruleScope.GroupId != candidate.GroupId)
                return false;
            if (ruleScope.Topic is not null && ruleScope.Topic != candidate.Topic)
                return false;
            return ruleScope.Partition is null || ruleScope.Partition == candidate.Partition;
        }

    }

    private readonly record struct OperationGroupKey(
        KafkaFaultOperation Operation,
        string? GroupId);

    private readonly record struct ScopeKey(
        KafkaFaultOperation Operation,
        string? Topic,
        int? Partition,
        string? GroupId);

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
