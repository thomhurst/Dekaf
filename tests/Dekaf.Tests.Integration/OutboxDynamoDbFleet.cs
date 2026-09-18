using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Dekaf.Outbox;
using Dekaf.Outbox.DynamoDB;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Several relay instances competing for one outbox table, each with its own store, as
/// separate pods would have. One manual clock stands in for their synchronized host clocks,
/// so lease expiry is deterministic. Refused conditional writes are counted at the AWS SDK,
/// where an application's telemetry would see them.
/// </summary>
internal sealed class OutboxDynamoDbFleet : IDisposable
{
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan RenewInterval = TimeSpan.FromSeconds(10);

    private readonly AmazonDynamoDBClient _client;
    private readonly Dictionary<string, DynamoDbOutboxStore> _stores = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Holding> _holdings = new(StringComparer.Ordinal);
    private int _refusedWrites;

    private OutboxDynamoDbFleet(AmazonDynamoDBClient client, DynamoDbOutboxOptions options)
    {
        _client = client;
        Options = options;
        _client.ExceptionEvent += OnException;
    }

    public DynamoDbOutboxOptions Options { get; }

    public ManualClock Clock { get; } = new();

    public IAmazonDynamoDB Client => _client;

    /// <summary>Conditional writes DynamoDB refused since the fleet started.</summary>
    public int RefusedWrites => Volatile.Read(ref _refusedWrites);

    public static async Task<OutboxDynamoDbFleet> CreateAsync(DynamoDbLocalContainer dynamoDb, int bucketCount = 8)
    {
        var client = dynamoDb.CreateClient();
        return new OutboxDynamoDbFleet(client, await DynamoDbLocalContainer.CreateTableAsync(client, bucketCount));
    }

    public OutboxLeaseRequest Request(string relayId) => new()
    {
        RelayId = relayId,
        BucketCount = Options.BucketCount,
        LeaseDuration = LeaseDuration
    };

    public DynamoDbOutboxStore Store(string relayId)
    {
        if (!_stores.TryGetValue(relayId, out var store))
            _stores[relayId] = store = new DynamoDbOutboxStore(_client, Options, Clock);
        return store;
    }

    /// <summary>One acquisition round of one relay, as the relay service runs it.</summary>
    public async Task<IReadOnlyList<int>> AcquireAsync(string relayId)
    {
        var startedAt = Clock.GetUtcNow();
        var owned = await Store(relayId).AcquireBucketLeasesAsync(Request(relayId));
        lock (_holdings)
            _holdings[relayId] = new Holding(owned, startedAt + LeaseDuration);
        AssertSingleOwner();
        await AssertTableNamesAsync(relayId, owned);
        return owned;
    }

    /// <summary>
    /// What a relay is told it owns must be what the table says: a result built from a write
    /// that DynamoDB refused would be a bucket with two publishers.
    /// </summary>
    private async Task AssertTableNamesAsync(string relayId, IReadOnlyList<int> owned)
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
        var named = (response.Items ?? [])
            .Where(item => item.TryGetValue("Owner", out var owner) && owner.S == relayId)
            .Select(item => int.Parse(item["SK"].S.AsSpan("LEASE#".Length), System.Globalization.CultureInfo.InvariantCulture))
            .Where(bucket => bucket < Options.BucketCount)
            .ToHashSet();
        foreach (var bucket in owned)
        {
            if (!named.Contains(bucket))
                Assert.Fail($"{relayId} was told it owns bucket {bucket}, but the table does not name it.");
        }
    }

    /// <summary>The relays acquire one after another, then the clock moves one renew interval on.</summary>
    public async Task RoundAsync(params string[] relayIds)
    {
        foreach (var relayId in relayIds)
            await AcquireAsync(relayId);
        Clock.Advance(RenewInterval);
    }

    /// <summary>The relays acquire at the same time, then the clock moves one renew interval on.</summary>
    public async Task ConcurrentRoundAsync(params string[] relayIds)
    {
        // Created up front: the store map is not safe to grow from the racing tasks.
        foreach (var relayId in relayIds)
            Store(relayId);
        await Task.WhenAll(relayIds.Select(relayId => Task.Run(() => AcquireAsync(relayId))));
        Clock.Advance(RenewInterval);
    }

    public async Task<bool> RenewAsync(string relayId)
    {
        var startedAt = Clock.GetUtcNow();
        var owned = Owned(relayId);
        var renewed = await Store(relayId).RenewBucketLeasesAsync(Request(relayId), owned);
        lock (_holdings)
        {
            if (renewed)
                _holdings[relayId] = new Holding(owned, startedAt + LeaseDuration);
            else
                _holdings.Remove(relayId);
        }

        return renewed;
    }

    /// <summary>A graceful stop: the relay hands its leases back and never runs again.</summary>
    public async Task ReleaseAsync(string relayId)
    {
        await Store(relayId).ReleaseBucketLeasesAsync(Request(relayId), Owned(relayId));
        Forget(relayId);
    }

    /// <summary>A crash: the relay stops without a word. Its leases and heartbeat stay behind.</summary>
    public void Forget(string relayId)
    {
        lock (_holdings)
            _holdings.Remove(relayId);
        _stores.Remove(relayId);
    }

    /// <summary>What the relay believes it owns: the result of its last acquisition.</summary>
    public IReadOnlyList<int> Owned(string relayId)
    {
        lock (_holdings)
            return _holdings.TryGetValue(relayId, out var holding) ? holding.Buckets : [];
    }

    public string Describe(params string[] relayIds) =>
        string.Join(" | ", relayIds.Select(relayId => $"{relayId}: {string.Join(',', Owned(relayId))}"));

    /// <summary>
    /// The safety property every scenario must hold at every step: no bucket has two relays
    /// that both believe, within their lease, that they own it.
    /// </summary>
    public void AssertSingleOwner()
    {
        var now = Clock.GetUtcNow();
        var owners = new Dictionary<int, string>();
        List<(string RelayId, Holding Holding)> holdings;
        lock (_holdings)
            holdings = [.. _holdings.Select(pair => (pair.Key, pair.Value))];

        foreach (var (relayId, holding) in holdings)
        {
            if (holding.ValidUntil <= now)
                continue;
            foreach (var bucket in holding.Buckets)
            {
                if (owners.TryGetValue(bucket, out var other))
                    Assert.Fail($"Bucket {bucket} is owned by both {other} and {relayId}.");
                owners[bucket] = relayId;
            }
        }
    }

    public void Dispose()
    {
        _client.ExceptionEvent -= OnException;
        _client.Dispose();
    }

    private void OnException(object sender, ExceptionEventArgs args)
    {
        if (args is WebServiceExceptionEventArgs { Exception: ConditionalCheckFailedException })
            Interlocked.Increment(ref _refusedWrites);
    }

    private sealed record Holding(IReadOnlyList<int> Buckets, DateTimeOffset ValidUntil);

    internal sealed class ManualClock : TimeProvider
    {
        private long _utcTicks = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero).UtcTicks;

        public override DateTimeOffset GetUtcNow() => new(Volatile.Read(ref _utcTicks), TimeSpan.Zero);

        public void Advance(TimeSpan duration) => Interlocked.Add(ref _utcTicks, duration.Ticks);
    }
}
