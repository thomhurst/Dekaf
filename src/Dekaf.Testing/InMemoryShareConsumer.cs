using System.Runtime.CompilerServices;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Testing;

/// <summary>
/// In-memory <see cref="IKafkaShareConsumer{TKey,TValue}"/> backed by an <see cref="InMemoryKafkaCluster"/>.
/// </summary>
public sealed class InMemoryShareConsumer<TKey, TValue> : IKafkaShareConsumer<TKey, TValue>
{
    private readonly object _gate = new();
    private readonly InMemoryKafkaCluster _cluster;
    private readonly IDeserializer<TKey> _keyDeserializer;
    private readonly IDeserializer<TValue> _valueDeserializer;
    // Non-null when the caller configured an IAsyncDeserializer for that component. The matching
    // synchronous slot then holds a throwing placeholder so a missed asynchronous divert fails
    // loudly instead of silently decoding nothing.
    private readonly IAsyncDeserializer<TKey>? _asyncKeyDeserializer;
    private readonly IAsyncDeserializer<TValue>? _asyncValueDeserializer;
    private readonly bool _hasAsyncDeserializers;
    private readonly InMemoryShareConsumerOptions _options;
    private readonly HashSet<string> _subscription = new(StringComparer.Ordinal);
    private readonly HashSet<TopicPartition> _assignment = [];
    private readonly Dictionary<ShareConsumeResult<TKey, TValue>, PendingShareRecord> _pending = [];
    private readonly string _memberId;
    private bool _disposed;

    public InMemoryShareConsumer(InMemoryKafkaCluster cluster)
        : this(
            cluster,
            InMemorySerdeResolver.Deserializer<TKey>(),
            InMemorySerdeResolver.Deserializer<TValue>(),
            new InMemoryShareConsumerOptions())
    {
    }

    public InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        InMemoryShareConsumerOptions options)
        : this(
            cluster,
            InMemorySerdeResolver.Deserializer<TKey>(),
            InMemorySerdeResolver.Deserializer<TValue>(),
            options)
    {
    }

    public InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer)
        : this(cluster, keyDeserializer, valueDeserializer, new InMemoryShareConsumerOptions())
    {
    }

    public InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        InMemoryShareConsumerOptions options)
        : this(
            cluster,
            InMemorySerdeResolver.Required(keyDeserializer, nameof(keyDeserializer)),
            InMemorySerdeResolver.Required(valueDeserializer, nameof(valueDeserializer)),
            asyncKeyDeserializer: null,
            asyncValueDeserializer: null,
            options)
    {
    }

    /// <summary>
    /// Creates a share consumer that awaits <see cref="IAsyncDeserializer{T}"/> for both components.
    /// </summary>
    public InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        IAsyncDeserializer<TKey> keyDeserializer,
        IAsyncDeserializer<TValue> valueDeserializer)
        : this(cluster, keyDeserializer, valueDeserializer, new InMemoryShareConsumerOptions())
    {
    }

    /// <summary>
    /// Creates a share consumer that awaits <see cref="IAsyncDeserializer{T}"/> for both components.
    /// </summary>
    public InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        IAsyncDeserializer<TKey> keyDeserializer,
        IAsyncDeserializer<TValue> valueDeserializer,
        InMemoryShareConsumerOptions options)
        : this(
            cluster,
            keyDeserializer: null,
            valueDeserializer: null,
            InMemorySerdeResolver.Required(keyDeserializer, nameof(keyDeserializer)),
            InMemorySerdeResolver.Required(valueDeserializer, nameof(valueDeserializer)),
            options)
    {
    }

    /// <summary>
    /// Creates a share consumer with a synchronous key deserializer and an asynchronous value deserializer.
    /// </summary>
    public InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        IDeserializer<TKey> keyDeserializer,
        IAsyncDeserializer<TValue> valueDeserializer)
        : this(cluster, keyDeserializer, valueDeserializer, new InMemoryShareConsumerOptions())
    {
    }

    /// <summary>
    /// Creates a share consumer with a synchronous key deserializer and an asynchronous value deserializer.
    /// </summary>
    public InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        IDeserializer<TKey> keyDeserializer,
        IAsyncDeserializer<TValue> valueDeserializer,
        InMemoryShareConsumerOptions options)
        : this(
            cluster,
            InMemorySerdeResolver.Required(keyDeserializer, nameof(keyDeserializer)),
            valueDeserializer: null,
            asyncKeyDeserializer: null,
            InMemorySerdeResolver.Required(valueDeserializer, nameof(valueDeserializer)),
            options)
    {
    }

    /// <summary>
    /// Creates a share consumer with an asynchronous key deserializer and a synchronous value deserializer.
    /// </summary>
    public InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        IAsyncDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer)
        : this(cluster, keyDeserializer, valueDeserializer, new InMemoryShareConsumerOptions())
    {
    }

    /// <summary>
    /// Creates a share consumer with an asynchronous key deserializer and a synchronous value deserializer.
    /// </summary>
    public InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        IAsyncDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        InMemoryShareConsumerOptions options)
        : this(
            cluster,
            keyDeserializer: null,
            InMemorySerdeResolver.Required(valueDeserializer, nameof(valueDeserializer)),
            InMemorySerdeResolver.Required(keyDeserializer, nameof(keyDeserializer)),
            asyncValueDeserializer: null,
            options)
    {
    }

    private InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        IDeserializer<TKey>? keyDeserializer,
        IDeserializer<TValue>? valueDeserializer,
        IAsyncDeserializer<TKey>? asyncKeyDeserializer,
        IAsyncDeserializer<TValue>? asyncValueDeserializer,
        InMemoryShareConsumerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.GroupId);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxPollRecords, 1);
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _asyncKeyDeserializer = asyncKeyDeserializer;
        _asyncValueDeserializer = asyncValueDeserializer;
        _keyDeserializer = asyncKeyDeserializer is null
            ? keyDeserializer!
            : AsyncOnlyDeserializerPlaceholder<TKey>.Instance;
        _valueDeserializer = asyncValueDeserializer is null
            ? valueDeserializer!
            : AsyncOnlyDeserializerPlaceholder<TValue>.Instance;
        _hasAsyncDeserializers = asyncKeyDeserializer is not null || asyncValueDeserializer is not null;
        _options = options;
        _memberId = _options.MemberId ?? Guid.NewGuid().ToString("N");
    }

    public IReadOnlySet<string> Subscription
    {
        get
        {
            lock (_gate)
                return _subscription.ToHashSet(StringComparer.Ordinal);
        }
    }

    public IReadOnlySet<TopicPartition> Assignment
    {
        get
        {
            lock (_gate)
                return _assignment.ToHashSet();
        }
    }

    public string? MemberId => _memberId;
    public int? AcquisitionLockTimeoutMs => null;

#if !NET10_0_OR_GREATER
    IReadOnlyCollection<string> IKafkaShareConsumer<TKey, TValue>.Subscription => Subscription;

    IReadOnlyCollection<TopicPartition> IKafkaShareConsumer<TKey, TValue>.Assignment => Assignment;
#endif

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return ValueTask.CompletedTask;
    }

    public IKafkaShareConsumer<TKey, TValue> Subscribe(params string[] topics)
    {
        ArgumentNullException.ThrowIfNull(topics);
        ThrowIfDisposed();

        var topicPartitions = topics
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Distinct(StringComparer.Ordinal)
            .SelectMany(topic => _cluster.GetTopicPartitions(topic))
            .ToArray();

        lock (_gate)
        {
            ReleasePendingUnderLock();
            _subscription.Clear();
            foreach (var topic in topics.Where(topic => !string.IsNullOrWhiteSpace(topic)).Distinct(StringComparer.Ordinal))
                _subscription.Add(topic);

            _assignment.Clear();
            foreach (var topicPartition in topicPartitions)
                _assignment.Add(topicPartition);
        }

        return this;
    }

    public IKafkaShareConsumer<TKey, TValue> Unsubscribe()
    {
        ThrowIfDisposed();

        lock (_gate)
        {
            ReleasePendingUnderLock();
            _subscription.Clear();
            _assignment.Clear();
        }

        return this;
    }

    public async IAsyncEnumerable<ShareConsumeResult<TKey, TValue>> PollAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await CommitAsync(cancellationToken).ConfigureAwait(false);

        for (var i = 0; i < _options.MaxPollRecords; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = _hasAsyncDeserializers
                ? await TryTakeAvailableRecordAsync(cancellationToken).ConfigureAwait(false)
                : TryTakeAvailableRecord();
            if (record is null)
                yield break;

            yield return record;
        }
    }

    public void Acknowledge(
        ShareConsumeResult<TKey, TValue> record,
        AcknowledgeType type = AcknowledgeType.Accept)
    {
        ArgumentNullException.ThrowIfNull(record);
        ThrowIfDisposed();

        lock (_gate)
        {
            if (!_pending.TryGetValue(record, out var pending))
                throw new InvalidOperationException("Record was not returned by the current poll.");

            pending.AcknowledgeType = type;
        }
    }

    public ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        lock (_gate)
        {
            if (_pending.Count == 0)
                return ValueTask.CompletedTask;

            var pending = _pending.Values.ToArray();
            var offsets = BuildCommitOffsets(pending);
            var completedRecords = BuildCompletedRecords(pending);

            _cluster.CompleteShareRecords(_options.GroupId, _memberId, completedRecords, offsets);
            _pending.Clear();
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        await CommitAsync(cancellationToken).ConfigureAwait(false);
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await CloseAsync().ConfigureAwait(false);
    }

    private ShareConsumeResult<TKey, TValue>? TryTakeAvailableRecord()
    {
        foreach (var partition in OrderedAssignment())
        {
            if (!TryAcquireRecord(partition, out var record, out var deliveryCount))
                continue;

            return RegisterPending(partition, record, ToShareResult(record, deliveryCount));
        }

        return null;
    }

    /// <summary>
    /// Poll path used when at least one component has an <see cref="IAsyncDeserializer{T}"/>.
    /// Deliberate mirror of <see cref="TryTakeAvailableRecord"/>: both drive the same acquisition
    /// and pending-record bookkeeping helpers.
    /// </summary>
    private async ValueTask<ShareConsumeResult<TKey, TValue>?> TryTakeAvailableRecordAsync(
        CancellationToken cancellationToken)
    {
        foreach (var partition in OrderedAssignment())
        {
            if (!TryAcquireRecord(partition, out var record, out var deliveryCount))
                continue;

            var result = await ToShareResultAsync(record, deliveryCount, cancellationToken).ConfigureAwait(false);
            return RegisterPending(partition, record, result);
        }

        return null;
    }

    private TopicPartition[] OrderedAssignment()
    {
        lock (_gate)
        {
            return _assignment
                .OrderBy(item => item.Topic, StringComparer.Ordinal)
                .ThenBy(item => item.Partition)
                .ToArray();
        }
    }

    private bool TryAcquireRecord(TopicPartition partition, out InMemoryRecord record, out int deliveryCount)
    {
        long offset;
        lock (_gate)
            offset = GetNextOffsetUnderLock(partition);

        return _cluster.TryAcquireShareRecord(
            _options.GroupId,
            _memberId,
            partition,
            offset,
            out record,
            out deliveryCount);
    }

    private ShareConsumeResult<TKey, TValue> RegisterPending(
        TopicPartition partition,
        InMemoryRecord record,
        ShareConsumeResult<TKey, TValue> result)
    {
        var pending = new PendingShareRecord(partition, record.Offset, record.Offset + 1);

        lock (_gate)
            _pending[result] = pending;

        return result;
    }

    private async ValueTask<ShareConsumeResult<TKey, TValue>> ToShareResultAsync(
        InMemoryRecord record,
        int deliveryCount,
        CancellationToken cancellationToken)
    {
        var key = record.IsKeyNull
            ? default
            : await DeserializeAsync(
                _asyncKeyDeserializer,
                _keyDeserializer,
                record.Key,
                Context(record.Topic, SerializationComponent.Key, record.Headers, isNull: false),
                cancellationToken).ConfigureAwait(false);

        var value = await DeserializeAsync(
            _asyncValueDeserializer,
            _valueDeserializer,
            record.IsValueNull ? ReadOnlyMemory<byte>.Empty : record.Value,
            Context(record.Topic, SerializationComponent.Value, record.Headers, isNull: record.IsValueNull),
            cancellationToken).ConfigureAwait(false);

        return new ShareConsumeResult<TKey, TValue>
        {
            Topic = record.Topic,
            Partition = record.Partition,
            Offset = record.Offset,
            Key = key,
            Value = value,
            Headers = record.Headers,
            TimestampMs = record.TimestampMs,
            DeliveryCount = deliveryCount
        };
    }

    private static ValueTask<T> DeserializeAsync<T>(
        IAsyncDeserializer<T>? asyncDeserializer,
        IDeserializer<T> deserializer,
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        CancellationToken cancellationToken) =>
        asyncDeserializer is not null
            ? asyncDeserializer.DeserializeAsync(data, context, cancellationToken)
            : ValueTask.FromResult(deserializer.Deserialize(data, context));

    private ShareConsumeResult<TKey, TValue> ToShareResult(InMemoryRecord record, int deliveryCount)
    {
        var key = record.IsKeyNull
            ? default
            : _keyDeserializer.Deserialize(
                record.Key,
                Context(record.Topic, SerializationComponent.Key, record.Headers, isNull: false));

        var value = record.IsValueNull
            ? _valueDeserializer.Deserialize(
                ReadOnlyMemory<byte>.Empty,
                Context(record.Topic, SerializationComponent.Value, record.Headers, isNull: true))
            : _valueDeserializer.Deserialize(
                record.Value,
                Context(record.Topic, SerializationComponent.Value, record.Headers, isNull: false));

        return new ShareConsumeResult<TKey, TValue>
        {
            Topic = record.Topic,
            Partition = record.Partition,
            Offset = record.Offset,
            Key = key,
            Value = value,
            Headers = record.Headers,
            TimestampMs = record.TimestampMs,
            DeliveryCount = deliveryCount
        };
    }

    private long GetNextOffsetUnderLock(TopicPartition partition)
    {
        var offset = _cluster.GetCommittedOffset(_options.GroupId, partition) ??
                     _cluster.GetWatermarks(partition).Low;

        foreach (var pending in _pending.Values)
        {
            if (pending.TopicPartition == partition && pending.NextOffset > offset)
                offset = pending.NextOffset;
        }

        return offset;
    }

    private void ReleasePendingUnderLock()
    {
        if (_pending.Count == 0)
            return;

        _cluster.ReleaseShareRecords(_options.GroupId, _memberId, BuildCompletedRecords(_pending.Values));
        _pending.Clear();
    }

    private static TopicPartitionOffset[] BuildCommitOffsets(IEnumerable<PendingShareRecord> records)
    {
        var offsets = new List<TopicPartitionOffset>();

        foreach (var group in records.GroupBy(record => record.TopicPartition))
        {
            var ordered = group.OrderBy(record => record.NextOffset).ToArray();
            if (ordered.Length == 0)
                continue;

            var commitOffset = ordered[0].NextOffset - 1;
            foreach (var record in ordered)
            {
                var recordOffset = record.NextOffset - 1;
                if (recordOffset != commitOffset)
                    break;

                if (record.AcknowledgeType is not (AcknowledgeType.Accept or AcknowledgeType.Reject))
                    break;

                commitOffset = record.NextOffset;
            }

            if (commitOffset > ordered[0].NextOffset - 1)
            {
                offsets.Add(new TopicPartitionOffset(
                    group.Key.Topic,
                    group.Key.Partition,
                    commitOffset));
            }
        }

        return offsets.ToArray();
    }

    private static TopicPartitionOffset[] BuildCompletedRecords(IEnumerable<PendingShareRecord> records)
    {
        return records
            .Select(record => new TopicPartitionOffset(
                record.TopicPartition.Topic,
                record.TopicPartition.Partition,
                record.Offset))
            .ToArray();
    }

    private static SerializationContext Context(
        string topic,
        SerializationComponent component,
        IReadOnlyList<Header>? headers,
        bool isNull) =>
        new()
        {
            Topic = topic,
            Component = component,
            Headers = headers is null ? null : new Headers(headers),
            IsNull = isNull
        };

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class PendingShareRecord
    {
        public PendingShareRecord(TopicPartition topicPartition, long offset, long nextOffset)
        {
            TopicPartition = topicPartition;
            Offset = offset;
            NextOffset = nextOffset;
        }

        public TopicPartition TopicPartition { get; }
        public long Offset { get; }
        public long NextOffset { get; }
        public AcknowledgeType AcknowledgeType { get; set; } = AcknowledgeType.Accept;
    }
}
