using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Dekaf.Outbox;
using Dekaf.Outbox.DynamoDB;
using Dekaf.Serialization;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Tests.Integration;

/// <summary>
/// A horizontally scaled service in miniature: every pod runs a real
/// <see cref="OutboxRelayService"/> on the real clock against one DynamoDB Local table, while
/// writers enqueue under load. Pods can be started, stopped gracefully, killed and frozen.
/// </summary>
/// <remarks>
/// <para>Kafka is replaced by a ledger that records every publication in order. That keeps
/// the broker out of the picture and makes the delivery guarantees checkable: nothing lost,
/// each key's first deliveries in enqueue order, and no pod publishing a bucket while the
/// table names a peer as its owner.</para>
/// <para>A killed pod is one whose store and publisher stop reaching anything, mid-call if
/// need be: no release, no further write, exactly what <c>SIGKILL</c> leaves behind. A frozen
/// pod blocks inside its next store or publisher call, as a suspended VM would.</para>
/// </remarks>
internal sealed class OutboxDynamoDbCluster : IAsyncDisposable
{
    private readonly AmazonDynamoDBClient _client;
    private readonly TimeSpan _leaseDuration;
    private readonly TimeSpan _renewInterval;
    private readonly TimeSpan _publishLatency;
    private readonly ConcurrentDictionary<string, Pod> _pods = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<Enqueued> _enqueued = new();
    private readonly List<Publication> _ledger = [];
    private readonly ConcurrentQueue<string> _fencingViolations = new();
    private int _refusedWrites;

    private OutboxDynamoDbCluster(
        AmazonDynamoDBClient client, DynamoDbOutboxOptions options, TimeSpan leaseDuration, TimeSpan renewInterval,
        TimeSpan publishLatency)
    {
        _client = client;
        Options = options;
        _leaseDuration = leaseDuration;
        _renewInterval = renewInterval;
        _publishLatency = publishLatency;
        _client.ExceptionEvent += OnException;
    }

    public DynamoDbOutboxOptions Options { get; }

    /// <summary>Conditional writes DynamoDB refused, counted where telemetry would see them.</summary>
    public int RefusedWrites => Volatile.Read(ref _refusedWrites);

    public IReadOnlyCollection<string> FencingViolations => [.. _fencingViolations];

    public IReadOnlyCollection<string> LivePods => [.. _pods.Keys.Order(StringComparer.Ordinal)];

    public int EnqueuedCount => _enqueued.Count;

    /// <param name="publishLatency">How long the stand-in broker takes to acknowledge a batch.</param>
    public static async Task<OutboxDynamoDbCluster> CreateAsync(
        DynamoDbLocalContainer dynamoDb, int bucketCount, TimeSpan leaseDuration, TimeSpan renewInterval,
        TimeSpan publishLatency = default)
    {
        var client = dynamoDb.CreateClient();
        var options = await DynamoDbLocalContainer.CreateTableAsync(client, bucketCount);
        return new OutboxDynamoDbCluster(client, options, leaseDuration, renewInterval, publishLatency);
    }

    public async Task<Pod> StartPodAsync(string name)
    {
        var pod = new Pod(this, name);
        if (!_pods.TryAdd(name, pod))
            throw new InvalidOperationException($"Pod {name} is already running.");
        await pod.Relay.StartAsync(CancellationToken.None);
        return pod;
    }

    public Task StartPodsAsync(params string[] names) => Task.WhenAll(names.Select(StartPodAsync));

    /// <summary>
    /// Runs writers until <paramref name="stop"/> is cancelled. Every key belongs to one
    /// writer, which enqueues its sequence numbers one after another, so each key has a
    /// defined enqueue order, as an aggregate under optimistic concurrency has.
    /// </summary>
    public Task RunWritersAsync(int writers, int keysPerWriter, CancellationToken stop, int? messagesPerKey = null) =>
        Task.WhenAll(Enumerable.Range(0, writers).Select(writer => Task.Run(async () =>
        {
            var outbox = new DynamoDbOutboxWriter(_client, Options);
            for (var sequence = 0; !stop.IsCancellationRequested && sequence != messagesPerKey; sequence++)
            {
                for (var key = 0; key < keysPerWriter; key++)
                {
                    var name = $"w{writer}-k{key}";
                    var message = OutboxMessage.Create(
                        "orders", name, sequence.ToString(CultureInfo.InvariantCulture), Serializers.String,
                        Serializers.String, bucketCount: Options.BucketCount);
                    // A transaction, as production code would write it with its business item.
                    await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest
                    {
                        TransactItems = [await outbox.CreateTransactWriteItemAsync(message)]
                    });
                    _enqueued.Enqueue(new Enqueued(message.MessageId, name, sequence));
                }
            }
        })));

    /// <summary>The unexpired owner of every bucket, read from the table.</summary>
    public async Task<Dictionary<int, string>> ReadOwnersAsync()
    {
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = Options.TableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :lease)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new() { S = "OUTBOX#COORDINATION" },
                [":lease"] = new() { S = "LEASE#" }
            },
            ConsistentRead = true
        });
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var owners = new Dictionary<int, string>();
        foreach (var item in response.Items ?? [])
        {
            if (item.TryGetValue("Owner", out var owner)
                && long.Parse(item["ExpiresAtUtc"].N, CultureInfo.InvariantCulture) > now)
            {
                owners[int.Parse(item["SK"].S.AsSpan("LEASE#".Length), CultureInfo.InvariantCulture)] = owner.S;
            }
        }

        return owners;
    }

    /// <summary>
    /// Waits until every bucket is owned by a live pod, no pod is more than one bucket ahead
    /// of another, and the split has stopped moving for two renew intervals.
    /// </summary>
    /// <returns>How long the fleet took to get there.</returns>
    public async Task<TimeSpan> WaitForFairSplitAsync(TimeSpan timeout)
    {
        var started = TimeProvider.System.GetTimestamp();
        string? settled = null;
        var settledAt = 0L;
        while (true)
        {
            var owners = await ReadOwnersAsync();
            var live = LivePods;
            var counts = live.ToDictionary(pod => pod, pod => owners.Count(owner => owner.Value == pod));
            var fair = owners.Count == Options.BucketCount
                && owners.Values.All(live.Contains)
                && counts.Values.Max() - counts.Values.Min() <= 1;
            var split = fair ? string.Join(';', owners.OrderBy(owner => owner.Key).Select(owner => $"{owner.Key}={owner.Value}")) : null;
            if (split is null || split != settled)
            {
                settled = split;
                settledAt = TimeProvider.System.GetTimestamp();
            }
            else if (TimeProvider.System.GetElapsedTime(settledAt) >= _renewInterval * 2)
            {
                return TimeProvider.System.GetElapsedTime(started, settledAt);
            }

            if (TimeProvider.System.GetElapsedTime(started) > timeout)
            {
                Assert.Fail($"No fair split after {timeout}. Live pods: {string.Join(',', live)}. Owners: "
                    + string.Join(' ', owners.OrderBy(owner => owner.Key).Select(owner => $"{owner.Key}={owner.Value}")));
            }

            await Task.Delay(50);
        }
    }

    /// <summary>Waits until every enqueued message was published and the table holds none.</summary>
    public async Task WaitForDrainedAsync(TimeSpan timeout)
    {
        var started = TimeProvider.System.GetTimestamp();
        var store = new DynamoDbOutboxStore(_client, Options);
        var buckets = Enumerable.Range(0, Options.BucketCount).ToArray();
        while (true)
        {
            var pending = await store.GetBucketsWithPendingAsync(buckets);
            if (pending.Count == 0 && MissingMessages().Count == 0)
                return;
            if (TimeProvider.System.GetElapsedTime(started) > timeout)
            {
                Assert.Fail($"Not drained after {timeout}: buckets {string.Join(',', pending)} still hold messages and "
                    + $"{MissingMessages().Count} of {_enqueued.Count} messages were never published. "
                    + $"Owners: {string.Join(' ', (await ReadOwnersAsync()).OrderBy(o => o.Key).Select(o => $"{o.Key}={o.Value}"))}");
            }

            await Task.Delay(100);
        }
    }

    /// <summary>
    /// The delivery contract: at least once, and per key the first deliveries in enqueue
    /// order. Duplicates are allowed; a handover may publish a message twice.
    /// </summary>
    public async Task AssertDeliveryAsync()
    {
        // In the test log, so a run shows what the fleet went through and not only that it passed.
        Console.WriteLine(
            $"[outbox-cluster] enqueued={_enqueued.Count} duplicates={DuplicatePublications()} "
            + $"refusedWrites={RefusedWrites} fencingViolations={_fencingViolations.Count} live={string.Join(',', LivePods)}");
        await Assert.That(MissingMessages()).IsEmpty();

        Publication[] ledger;
        lock (_ledger)
            ledger = [.. _ledger];
        var lastSequence = new Dictionary<string, int>(StringComparer.Ordinal);
        var seen = new HashSet<Guid>();
        foreach (var publication in ledger)
        {
            if (!seen.Add(publication.MessageId))
                continue;
            if (lastSequence.TryGetValue(publication.Key, out var last) && publication.Sequence <= last)
            {
                Assert.Fail($"Key {publication.Key}: sequence {publication.Sequence} was first published by "
                    + $"{publication.Pod} after sequence {last}.");
            }

            lastSequence[publication.Key] = publication.Sequence;
        }

        await Assert.That(FencingViolations).IsEmpty();
    }

    public int DuplicatePublications()
    {
        lock (_ledger)
            return _ledger.Count - _ledger.Select(publication => publication.MessageId).Distinct().Count();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var pod in _pods.Values.ToArray())
            await pod.KillAsync();
        _client.ExceptionEvent -= OnException;
        _client.Dispose();
    }

    private List<Guid> MissingMessages()
    {
        HashSet<Guid> published;
        lock (_ledger)
            published = [.. _ledger.Select(publication => publication.MessageId)];
        return [.. _enqueued.Select(message => message.MessageId).Where(id => !published.Contains(id))];
    }

    private void OnException(object sender, ExceptionEventArgs args)
    {
        if (args is WebServiceExceptionEventArgs { Exception: ConditionalCheckFailedException })
            Interlocked.Increment(ref _refusedWrites);
    }

    private sealed record Enqueued(Guid MessageId, string Key, int Sequence);

    private sealed record Publication(string Pod, int Bucket, Guid MessageId, string Key, int Sequence);

    private sealed class PodKilledException() : Exception("The pod was killed.");

    /// <summary>One instance of the service: a relay with its own store and publisher.</summary>
    internal sealed class Pod
    {
        private readonly OutboxDynamoDbCluster _cluster;
        private volatile bool _killed;
        private volatile TaskCompletionSource? _frozen;
        private TaskCompletionSource? _thawedAfterPublishFreeze;
        private long _freezeOnPublishTicks;

        public Pod(OutboxDynamoDbCluster cluster, string name)
        {
            _cluster = cluster;
            Name = name;
            Relay = new OutboxRelayService(
                new ChaosStore(this, new DynamoDbOutboxStore(cluster._client, cluster.Options)),
                new LedgerPublisher(this),
                new OutboxRelayOptions
                {
                    RelayId = name,
                    BucketCount = cluster.Options.BucketCount,
                    LeaseDuration = cluster._leaseDuration,
                    LeaseRenewInterval = cluster._renewInterval,
                    PollInterval = TimeSpan.FromMilliseconds(200),
                    ErrorBackoff = TimeSpan.FromMilliseconds(100),
                    BatchSize = 25
                },
                NullLogger<OutboxRelayService>.Instance);
        }

        public string Name { get; }

        public OutboxRelayService Relay { get; }

        /// <summary>A rolling update or a scale-in: the relay stops and hands its buckets back.</summary>
        public async Task StopGracefullyAsync()
        {
            await Relay.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(60));
            _cluster._pods.TryRemove(Name, out _);
            Relay.Dispose();
        }

        /// <summary><c>SIGKILL</c>, out of memory, node loss: nothing is released.</summary>
        public async Task KillAsync()
        {
            _killed = true;
            _frozen?.TrySetResult();
            _cluster._pods.TryRemove(Name, out _);
            // Only tidies the test process up: every call the stopping relay makes fails.
            await Relay.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(60));
            Relay.Dispose();
        }

        /// <summary>A long pause or a suspended VM: the pod does nothing, then carries on.</summary>
        public async Task FreezeAsync(TimeSpan duration)
        {
            var frozen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _frozen = frozen;
            await Task.Delay(duration);
            _frozen = null;
            frozen.TrySetResult();
        }

        /// <summary>
        /// Freezes the pod the next time it is inside a publish, after the broker took the
        /// batch and before the pod learns of it: the worst moment for a pause.
        /// </summary>
        /// <returns>Completes when the pod has thawed.</returns>
        public Task FreezeDuringNextPublishAsync(TimeSpan duration)
        {
            var thawed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _thawedAfterPublishFreeze = thawed;
            Volatile.Write(ref _freezeOnPublishTicks, duration.Ticks);
            return thawed.Task;
        }

        private async ValueTask EnterAsync()
        {
            if (_frozen is { } frozen)
                await frozen.Task;
            if (_killed)
                throw new PodKilledException();
        }

        private sealed class ChaosStore(Pod pod, DynamoDbOutboxStore inner)
            : IOutboxStore, IOutboxLeaseRenewalStore, IOutboxLeaseOwnershipStore
        {
            public async ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(
                OutboxLeaseRequest request, CancellationToken cancellationToken = default)
            {
                await pod.EnterAsync();
                return await inner.AcquireBucketLeasesAsync(request, cancellationToken);
            }

            public async ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(
                OutboxLeaseRequest request, IReadOnlyList<int> previousBuckets, CancellationToken cancellationToken = default)
            {
                await pod.EnterAsync();
                return await ((IOutboxLeaseOwnershipStore)inner).AcquireBucketLeasesAsync(
                    request, previousBuckets, cancellationToken);
            }

            public async ValueTask<bool> RenewBucketLeasesAsync(
                OutboxLeaseRequest request, IReadOnlyList<int> buckets, CancellationToken cancellationToken = default)
            {
                await pod.EnterAsync();
                return await inner.RenewBucketLeasesAsync(request, buckets, cancellationToken);
            }

            public async ValueTask ReleaseBucketLeasesAsync(
                OutboxLeaseRequest request, IReadOnlyList<int> previousBuckets, CancellationToken cancellationToken = default)
            {
                await pod.EnterAsync();
                await inner.ReleaseBucketLeasesAsync(request, previousBuckets, cancellationToken);
            }

            public async ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(
                IReadOnlyList<int> buckets, CancellationToken cancellationToken = default)
            {
                await pod.EnterAsync();
                return await inner.GetBucketsWithPendingAsync(buckets, cancellationToken);
            }

            public async ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(
                int bucket, int maxCount, CancellationToken cancellationToken = default)
            {
                await pod.EnterAsync();
                return await inner.GetNextBatchAsync(bucket, maxCount, cancellationToken);
            }

            public async ValueTask MarkPublishedAsync(
                int bucket, IReadOnlyList<OutboxMessage> publishedMessages, CancellationToken cancellationToken = default)
            {
                await pod.EnterAsync();
                await inner.MarkPublishedAsync(bucket, publishedMessages, cancellationToken);
            }
        }

        /// <summary>
        /// Stands in for the Kafka producer. It fails now and then, part way through a batch,
        /// so the relay's retained-suffix path runs under the same load.
        /// </summary>
        private sealed class LedgerPublisher(Pod pod) : IOutboxPublisher
        {
            // One publisher call at a time per relay, so the generator needs no lock.
            private readonly Random _random = new(StringComparer.Ordinal.GetHashCode(pod.Name));

            public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

            public async ValueTask<OutboxPublishResult> PublishAsync(
                IReadOnlyList<OutboxMessage> messages, string messageIdHeaderName, CancellationToken cancellationToken = default)
            {
                // Checked at the door, before any freeze. A pod frozen between this point and
                // its produce call is the documented duplicate window: nothing short of a
                // Kafka transaction fences an in-flight publish.
                await AssertNoPeerOwnsAsync(messages[0].Bucket);
                await pod.EnterAsync();
                // Not cancellable: a record handed to a producer is delivered whatever the
                // relay does next, which is why a stopping relay waits for its publisher.
                await Task.Delay(
                    pod._cluster._publishLatency + TimeSpan.FromMilliseconds(_random.Next(0, 4)), CancellationToken.None);
                var freezeTicks = Interlocked.Exchange(ref pod._freezeOnPublishTicks, 0);
                var froze = freezeTicks > 0;
                if (froze)
                {
                    await pod.FreezeAsync(TimeSpan.FromTicks(freezeTicks));
                    pod._thawedAfterPublishFreeze?.TrySetResult();
                }

                await pod.EnterAsync();

                // The batch a pod froze on is always acknowledged in full: that is the case
                // in which a takeover has to republish what the broker already has.
                var acknowledged = !froze && _random.Next(20) == 0 ? _random.Next(messages.Count) : messages.Count;
                lock (pod._cluster._ledger)
                {
                    for (var index = 0; index < acknowledged; index++)
                    {
                        var message = messages[index];
                        pod._cluster._ledger.Add(new Publication(
                            pod.Name, message.Bucket, message.MessageId, Encoding.UTF8.GetString(message.Key!),
                            int.Parse(Encoding.UTF8.GetString(message.Value!), CultureInfo.InvariantCulture)));
                    }
                }

                return new OutboxPublishResult(
                    acknowledged, acknowledged == messages.Count ? null : new IOException("Simulated broker failure."));
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            private async Task AssertNoPeerOwnsAsync(int bucket)
            {
                var response = await pod._cluster._client.GetItemAsync(new GetItemRequest
                {
                    TableName = pod._cluster.Options.TableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["PK"] = new() { S = "OUTBOX#COORDINATION" },
                        ["SK"] = new() { S = $"LEASE#{bucket:D10}" }
                    },
                    ConsistentRead = true
                });
                if (response.Item is { } lease
                    && lease.TryGetValue("Owner", out var owner) && owner.S != pod.Name
                    && long.Parse(lease["ExpiresAtUtc"].N, CultureInfo.InvariantCulture) > DateTimeOffset.UtcNow.UtcTicks)
                {
                    pod._cluster._fencingViolations.Enqueue(
                        $"{pod.Name} published bucket {bucket} while the table named {owner.S} as its owner.");
                }
            }
        }
    }
}
