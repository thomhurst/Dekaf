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
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.GroupId);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxPollRecords, 1);
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _keyDeserializer = keyDeserializer ?? throw new ArgumentNullException(nameof(keyDeserializer));
        _valueDeserializer = valueDeserializer ?? throw new ArgumentNullException(nameof(valueDeserializer));
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
            _subscription.Clear();
            foreach (var topic in topics.Where(topic => !string.IsNullOrWhiteSpace(topic)).Distinct(StringComparer.Ordinal))
                _subscription.Add(topic);

            _assignment.Clear();
            foreach (var topicPartition in topicPartitions)
                _assignment.Add(topicPartition);

            _pending.Clear();
        }

        return this;
    }

    public IKafkaShareConsumer<TKey, TValue> Unsubscribe()
    {
        ThrowIfDisposed();

        lock (_gate)
        {
            _subscription.Clear();
            _assignment.Clear();
            _pending.Clear();
        }

        return this;
    }

    public async IAsyncEnumerable<ShareConsumeResult<TKey, TValue>> PollAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await CommitAsync(cancellationToken).ConfigureAwait(false);

        var records = TakeAvailableRecords();
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

            var offsets = _pending.Values
                .Where(record => record.AcknowledgeType is AcknowledgeType.Accept or AcknowledgeType.Reject)
                .GroupBy(record => record.TopicPartition)
                .Select(group => new TopicPartitionOffset(
                    group.Key.Topic,
                    group.Key.Partition,
                    group.Max(record => record.NextOffset)))
                .ToArray();

            if (offsets.Length > 0)
                _cluster.CommitOffsets(_options.GroupId, offsets);

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

    private IReadOnlyList<ShareConsumeResult<TKey, TValue>> TakeAvailableRecords()
    {
        var results = new List<ShareConsumeResult<TKey, TValue>>();
        Dictionary<TopicPartition, long> nextOffsets;
        TopicPartition[] assignment;

        lock (_gate)
        {
            assignment = _assignment
                .OrderBy(item => item.Topic, StringComparer.Ordinal)
                .ThenBy(item => item.Partition)
                .ToArray();
        }

        nextOffsets = assignment.ToDictionary(
            partition => partition,
            partition => _cluster.GetCommittedOffset(_options.GroupId, partition) ??
                         _cluster.GetWatermarks(partition).Low);

        while (results.Count < _options.MaxPollRecords)
        {
            var madeProgress = false;
            foreach (var partition in assignment)
            {
                if (results.Count >= _options.MaxPollRecords)
                    break;

                var offset = nextOffsets[partition];
                if (!_cluster.TryRead(partition, offset, out var record))
                    continue;

                var result = ToShareResult(record);
                var pending = new PendingShareRecord(partition, record.Offset + 1);

                lock (_gate)
                    _pending[result] = pending;

                results.Add(result);
                nextOffsets[partition] = record.Offset + 1;
                madeProgress = true;
            }

            if (!madeProgress)
                break;
        }

        return results;
    }

    private ShareConsumeResult<TKey, TValue> ToShareResult(InMemoryRecord record)
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
            DeliveryCount = 1
        };
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
        public PendingShareRecord(TopicPartition topicPartition, long nextOffset)
        {
            TopicPartition = topicPartition;
            NextOffset = nextOffset;
        }

        public TopicPartition TopicPartition { get; }
        public long NextOffset { get; }
        public AcknowledgeType AcknowledgeType { get; set; } = AcknowledgeType.Accept;
    }
}
