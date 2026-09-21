using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Dekaf.Outbox;
using Dekaf.Outbox.DynamoDB;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Several relay instances competing for one outbox table, each with its own store, as
/// separate pods would have. One manual clock is the true time, so lease expiry is
/// deterministic; every relay reads it through its own host clock, which is synchronized
/// unless a scenario sets it off. Refused conditional writes are counted at the AWS SDK,
/// where an application's telemetry would see them.
/// </summary>
internal sealed class OutboxDynamoDbFleet : IDisposable
{
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan RenewInterval = TimeSpan.FromSeconds(10);

    private readonly AmazonDynamoDBClient _client;
    private readonly IAmazonDynamoDB _storeClient;
    private readonly Dictionary<string, DynamoDbOutboxStore> _stores = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HostClock> _hostClocks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Holding> _holdings = new(StringComparer.Ordinal);
    private int _refusedWrites;

    private OutboxDynamoDbFleet(AmazonDynamoDBClient client, DynamoDbOutboxOptions options)
    {
        _client = client;
        _storeClient = OutboxDynamoDbCallInterceptor.Wrap(client, async (method, request) =>
        {
            if (BeforeStoreCall is { } beforeStoreCall)
                await beforeStoreCall(method, request);
            return OutboxDynamoDbFault.None;
        });
        Options = options;
        _client.ExceptionEvent += OnException;
    }

    public DynamoDbOutboxOptions Options { get; }

    /// <summary>The true time. A relay's lease is valid on it, whatever its host clock says.</summary>
    public ManualClock Clock { get; } = new();

    /// <summary>
    /// Runs before every request a store sends, with the method name and the request. A
    /// scenario uses it to land a write between two requests of one store call.
    /// </summary>
    public Func<string, AmazonWebServiceRequest, Task>? BeforeStoreCall { get; set; }

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
            _stores[relayId] = store = new DynamoDbOutboxStore(_storeClient, Options, HostClockOf(relayId));
        return store;
    }

    /// <summary>
    /// Sets a relay's host clock off from the true time: positive runs ahead. It stays with
    /// the host, so a relay that crashes and restarts there keeps it.
    /// </summary>
    public void SetClockOffset(string relayId, TimeSpan offset) => HostClockOf(relayId).Offset = offset;

    private HostClock HostClockOf(string relayId)
    {
        if (!_hostClocks.TryGetValue(relayId, out var clock))
            _hostClocks[relayId] = clock = new HostClock(Clock);
        return clock;
    }

    /// <summary>The expiry of every lease in the table, by bucket.</summary>
    public async Task<Dictionary<int, DateTimeOffset>> ReadLeaseExpiriesAsync()
    {
        var expiries = new Dictionary<int, DateTimeOffset>();
        foreach (var item in await ReadLeasesAsync())
        {
            if (item.TryGetValue("ExpiresAtUtc", out var expiry))
            {
                expiries[BucketOf(item)] = new DateTimeOffset(
                    long.Parse(expiry.N, System.Globalization.CultureInfo.InvariantCulture), TimeSpan.Zero);
            }
        }

        return expiries;
    }

    /// <summary>The buckets whose lease names the relay, expired or not.</summary>
    public async Task<IReadOnlyList<int>> ReadNamedAsync(string relayId) =>
        [.. (await ReadLeasesAsync())
            .Where(item => item.TryGetValue("Owner", out var owner) && owner.S == relayId)
            .Select(BucketOf).Order()];

    private async Task<List<Dictionary<string, AttributeValue>>> ReadLeasesAsync()
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
        return response.Items ?? [];
    }

    private static int BucketOf(Dictionary<string, AttributeValue> lease) =>
        int.Parse(lease["SK"].S.AsSpan("LEASE#".Length), System.Globalization.CultureInfo.InvariantCulture);

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

    /// <summary>
    /// The heartbeat of a round that a stopping host cancelled, reaching DynamoDB after the
    /// release: the client gave the request up, but the service can still apply it. Written
    /// here in the store's own shape, with the timestamp the cancelled round carried.
    /// </summary>
    /// <returns>Whether DynamoDB applied it.</returns>
    public async Task<bool> LandStragglingHeartbeatAsync(string relayId, DateTimeOffset roundStartedAt)
    {
        var lastSeen = new AttributeValue
        {
            N = roundStartedAt.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        try
        {
            await _client.PutItemAsync(new PutItemRequest
            {
                TableName = Options.TableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new() { S = "OUTBOX#COORDINATION" },
                    ["SK"] = new() { S = $"RELAY#{relayId}" },
                    ["LastSeenUtc"] = lastSeen
                },
                ConditionExpression = "attribute_not_exists(#lastSeen) OR #lastSeen <= :now",
                ExpressionAttributeNames = new Dictionary<string, string> { ["#lastSeen"] = "LastSeenUtc" },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":now"] = lastSeen }
            });
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    /// <summary>
    /// The claim of a round that a stopping host cancelled, reaching DynamoDB later, in the
    /// store's own shape. No condition can refuse it once the release has freed the lease.
    /// </summary>
    /// <returns>Whether DynamoDB applied it.</returns>
    public Task<bool> LandStragglingClaimAsync(string relayId, int bucket, DateTimeOffset roundStartedAt) =>
        LandStragglingLeaseWriteAsync(bucket, new UpdateItemRequest
        {
            UpdateExpression = "SET #owner = :me, #expires = :expiry",
            ConditionExpression = "attribute_not_exists(#owner) OR attribute_not_exists(#expires) OR #expires <= :now",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":me"] = new() { S = relayId },
                [":now"] = Ticks(roundStartedAt),
                [":expiry"] = Ticks(roundStartedAt + LeaseDuration)
            }
        });

    /// <summary>
    /// The renewal of a lease the cancelled round had read with <paramref name="seenExpiry"/>,
    /// reaching DynamoDB later. The expiry it read is what fences it.
    /// </summary>
    /// <returns>Whether DynamoDB applied it.</returns>
    public Task<bool> LandStragglingKeepAsync(
        string relayId, int bucket, DateTimeOffset seenExpiry, DateTimeOffset roundStartedAt) =>
        LandStragglingLeaseWriteAsync(bucket, new UpdateItemRequest
        {
            UpdateExpression = "SET #expires = :expiry",
            ConditionExpression = "#owner = :me AND #expires = :seen AND #expires <= :expiry",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":me"] = new() { S = relayId },
                [":seen"] = Ticks(seenExpiry),
                [":expiry"] = Ticks(roundStartedAt + LeaseDuration)
            }
        });

    private async Task<bool> LandStragglingLeaseWriteAsync(int bucket, UpdateItemRequest write)
    {
        write.TableName = Options.TableName;
        write.Key = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new() { S = "OUTBOX#COORDINATION" },
            ["SK"] = new() { S = $"LEASE#{bucket:D10}" }
        };
        write.ExpressionAttributeNames = new Dictionary<string, string>
        {
            ["#owner"] = "Owner",
            ["#expires"] = "ExpiresAtUtc"
        };
        try
        {
            await _client.UpdateItemAsync(write);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    private static AttributeValue Ticks(DateTimeOffset time) =>
        new() { N = time.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture) };

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

    /// <summary>One host's view of the true time.</summary>
    private sealed class HostClock(ManualClock trueTime) : TimeProvider
    {
        private long _offsetTicks;

        public TimeSpan Offset
        {
            get => TimeSpan.FromTicks(Volatile.Read(ref _offsetTicks));
            set => Volatile.Write(ref _offsetTicks, value.Ticks);
        }

        public override DateTimeOffset GetUtcNow() => trueTime.GetUtcNow() + Offset;
    }

    internal sealed class ManualClock : TimeProvider
    {
        private long _utcTicks = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero).UtcTicks;

        public override DateTimeOffset GetUtcNow() => new(Volatile.Read(ref _utcTicks), TimeSpan.Zero);

        public void Advance(TimeSpan duration) => Interlocked.Add(ref _utcTicks, duration.Ticks);
    }
}
