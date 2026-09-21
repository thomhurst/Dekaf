namespace Dekaf.Tests.Integration;

/// <summary>
/// The DynamoDB outbox as production runs it: many pods, each with a real relay on the real
/// clock, all competing for the buckets of one table while writers enqueue under load. The
/// scenarios are the life of a deployment: a cold start, a rolling update, scaling out and
/// in, pods that are killed or frozen, and more pods than buckets.
/// </summary>
/// <remarks>
/// Every scenario checks the same contract. Nothing is lost; each key's first deliveries are
/// in enqueue order; no pod publishes a bucket while the table names a peer as its owner; the
/// fleet settles on a fair split; and a settled fleet makes no refused conditional write.
/// Timing is real, so the assertions are invariants and eventual conditions, never a count
/// that depends on how the rounds of different pods interleave.
/// </remarks>
[Category("MessagingPatterns")]
[NotInParallel("OutboxDynamoDbCluster")]
[ClassDataSource<DynamoDbLocalContainer>(Shared = SharedType.PerTestSession)]
public sealed class OutboxDynamoDbClusterTests(DynamoDbLocalContainer dynamoDb)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(90);

    [Test]
    public async Task TwelvePods_StartingTogether_SplitTheBuckets_PublishEverything_AndThenStayQuiet()
    {
        // A lease this long cannot lapse on a slow build agent, so the quiet check below
        // measures the protocol and not the machine.
        await using var cluster = await OutboxDynamoDbCluster.CreateAsync(
            dynamoDb, bucketCount: 48, TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(500));
        var pods = Enumerable.Range(0, 12).Select(index => $"pod-{index:D2}").ToArray();

        var writers = cluster.RunWritersAsync(writers: 4, keysPerWriter: 8, CancellationToken.None, messagesPerKey: 20);
        await cluster.StartPodsAsync(pods);
        await writers;

        await cluster.WaitForFairSplitAsync(Patience);
        await cluster.WaitForDrainedAsync(Patience);
        await cluster.AssertDeliveryAsync();
        await Assert.That(cluster.EnqueuedCount).IsEqualTo(4 * 8 * 20);

        // The cold start is the one legitimate race. From here on the fleet is settled:
        // ten renewal rounds of twelve pods, all running at once, must refuse nothing and
        // must not move a single bucket.
        var settledOwners = Describe(await cluster.ReadOwnersAsync());
        var settledRefusals = cluster.RefusedWrites;
        await Task.Delay(TimeSpan.FromSeconds(5));

        await Assert.That(cluster.RefusedWrites).IsEqualTo(settledRefusals);
        await Assert.That(Describe(await cluster.ReadOwnersAsync())).IsEqualTo(settledOwners);
        // Far below what blind probing costs: buckets x (pods - 1), every round, forever.
        await Assert.That(settledRefusals).IsLessThan(48 * 11);
    }

    [Test]
    public async Task RollingUpdate_UnderLoad_HandsBucketsOverWithoutWaitingForExpiry_AndLosesNothing()
    {
        var leaseDuration = TimeSpan.FromSeconds(20);
        await using var cluster = await OutboxDynamoDbCluster.CreateAsync(
            dynamoDb, bucketCount: 16, leaseDuration, TimeSpan.FromMilliseconds(300));
        var running = new List<OutboxDynamoDbCluster.Pod>();
        foreach (var name in new[] { "v1-a", "v1-b", "v1-c", "v1-d" })
            running.Add(await cluster.StartPodAsync(name));
        await cluster.WaitForFairSplitAsync(Patience);

        using var stopWriters = new CancellationTokenSource();
        var writers = cluster.RunWritersAsync(writers: 3, keysPerWriter: 6, stopWriters.Token);

        for (var index = 0; index < 4; index++)
        {
            // Surge one, then stop one: the default Kubernetes rolling update.
            var replacement = await cluster.StartPodAsync($"v2-{(char)('a' + index)}");
            await running[index].StopGracefullyAsync();
            running[index] = replacement;

            // The old pod released its leases, so its buckets move within a few renewal
            // rounds. Waiting for them to expire would take the whole lease duration.
            var handover = await cluster.WaitForFairSplitAsync(Patience);
            await Assert.That(handover).IsLessThan(leaseDuration);
        }

        await stopWriters.CancelAsync();
        await writers;
        await cluster.WaitForDrainedAsync(Patience);
        await cluster.AssertDeliveryAsync();
        await Assert.That(cluster.LivePods.All(pod => pod.StartsWith("v2-", StringComparison.Ordinal))).IsTrue();
        // Eight membership changes. A refusal needs two pods to plan from different
        // membership within the same few milliseconds, so there are at most a handful.
        await Assert.That(cluster.RefusedWrites).IsLessThanOrEqualTo(16);
    }

    [Test]
    public async Task ScalingOutAndIn_UnderLoad_RebalancesEveryTime_AndLosesNothing()
    {
        await using var cluster = await OutboxDynamoDbCluster.CreateAsync(
            dynamoDb, bucketCount: 24, TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(300));
        var first = await cluster.StartPodAsync("pod-00");
        using var stopWriters = new CancellationTokenSource();
        var writers = cluster.RunWritersAsync(writers: 3, keysPerWriter: 6, stopWriters.Token);
        await cluster.WaitForFairSplitAsync(Patience);

        // The autoscaler adds seven pods at once.
        var added = new List<OutboxDynamoDbCluster.Pod>();
        foreach (var name in Enumerable.Range(1, 7).Select(index => $"pod-{index:D2}"))
            added.Add(await cluster.StartPodAsync(name));
        await cluster.WaitForFairSplitAsync(Patience);
        await Assert.That((await cluster.ReadOwnersAsync()).Values.Distinct().Count()).IsEqualTo(8);

        // Then load drops and it removes six, several at the same moment.
        await Task.WhenAll(added.Take(5).Select(pod => pod.StopGracefullyAsync()));
        await first.StopGracefullyAsync();
        await cluster.WaitForFairSplitAsync(Patience);
        await Assert.That((await cluster.ReadOwnersAsync()).Values.Distinct().Count()).IsEqualTo(2);

        await stopWriters.CancelAsync();
        await writers;
        await cluster.WaitForDrainedAsync(Patience);
        await cluster.AssertDeliveryAsync();
    }

    [Test]
    public async Task KilledAndFrozenPods_UnderLoad_NeverLoseOrReorderAMessage_AndNeverPublishAPeersBucket()
    {
        var leaseDuration = TimeSpan.FromSeconds(3);
        await using var cluster = await OutboxDynamoDbCluster.CreateAsync(
            dynamoDb, bucketCount: 20, leaseDuration, TimeSpan.FromMilliseconds(300));
        var pods = new Dictionary<string, OutboxDynamoDbCluster.Pod>();
        foreach (var name in new[] { "pod-a", "pod-b", "pod-c", "pod-d", "pod-e" })
            pods[name] = await cluster.StartPodAsync(name);
        await cluster.WaitForFairSplitAsync(Patience);

        using var stopWriters = new CancellationTokenSource();
        var writers = cluster.RunWritersAsync(writers: 4, keysPerWriter: 5, stopWriters.Token);

        // An out-of-memory kill, mid-publish for all the pod knows.
        await pods["pod-a"].KillAsync();
        // A pod frozen past its lease while its peers carry on, and a replacement arriving.
        var frozen = pods["pod-b"].FreezeAsync(leaseDuration + TimeSpan.FromSeconds(4));
        pods["pod-f"] = await cluster.StartPodAsync("pod-f");
        // Its peers must take its buckets while it is still frozen. Once it thaws it renews
        // again, so a takeover that has not happened by then never would.
        await WaitUntilAsync(async () => !(await cluster.ReadOwnersAsync()).ContainsValue("pod-b"));
        await Assert.That(frozen.IsCompleted).IsFalse();
        await frozen;
        // A node is lost: two pods vanish at the same moment.
        await Task.WhenAll(pods["pod-c"].KillAsync(), pods["pod-d"].KillAsync());
        pods["pod-g"] = await cluster.StartPodAsync("pod-g");
        // A second freeze, of a pod that took over buckets during the first one.
        await pods["pod-e"].FreezeAsync(leaseDuration + TimeSpan.FromSeconds(2));

        await stopWriters.CancelAsync();
        await writers;

        // Dead pods' buckets are taken over once their leases lapse; the thawed pods are
        // handed a share again. Four pods remain: b, e, f and g.
        await cluster.WaitForFairSplitAsync(Patience);
        await cluster.WaitForDrainedAsync(Patience);
        await cluster.AssertDeliveryAsync();
        await Assert.That(string.Join(',', cluster.LivePods)).IsEqualTo("pod-b,pod-e,pod-f,pod-g");
    }

    [Test]
    public async Task MorePodsThanBuckets_TheIdlePodsStepIn_WhenOwnersAreKilled()
    {
        await using var cluster = await OutboxDynamoDbCluster.CreateAsync(
            dynamoDb, bucketCount: 4, TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(300));
        var pods = new Dictionary<string, OutboxDynamoDbCluster.Pod>();
        foreach (var name in Enumerable.Range(0, 6).Select(index => $"pod-{index}"))
            pods[name] = await cluster.StartPodAsync(name);
        await cluster.WaitForFairSplitAsync(Patience);
        var owners = (await cluster.ReadOwnersAsync()).Values.Distinct().ToArray();
        await Assert.That(owners.Length).IsEqualTo(4);

        using var stopWriters = new CancellationTokenSource();
        var writers = cluster.RunWritersAsync(writers: 2, keysPerWriter: 8, stopWriters.Token);

        // Two of the four owners die. Only the two idle pods can restore a fair split.
        await Task.WhenAll(owners.Take(2).Select(owner => pods[owner].KillAsync()));
        await cluster.WaitForFairSplitAsync(Patience);

        await stopWriters.CancelAsync();
        await writers;
        await cluster.WaitForDrainedAsync(Patience);
        await cluster.AssertDeliveryAsync();
        await Assert.That((await cluster.ReadOwnersAsync()).Values.Distinct().Count()).IsEqualTo(4);
    }

    [Test]
    public async Task PodFrozenInsideAPublish_LosesItsBuckets_AndTheOnlyDamageIsDuplicates()
    {
        var leaseDuration = TimeSpan.FromSeconds(3);
        await using var cluster = await OutboxDynamoDbCluster.CreateAsync(
            dynamoDb, bucketCount: 8, leaseDuration, TimeSpan.FromMilliseconds(300),
            publishLatency: TimeSpan.FromMilliseconds(200));
        var victim = await cluster.StartPodAsync("pod-a");
        await cluster.StartPodsAsync("pod-b", "pod-c");
        await cluster.WaitForFairSplitAsync(Patience);

        using var stopWriters = new CancellationTokenSource();
        var writers = cluster.RunWritersAsync(writers: 3, keysPerWriter: 8, stopWriters.Token);

        // The broker has the batch, the pod has not marked it, and the pod stops dead for
        // longer than its lease: the documented at-least-once window, at its widest.
        await victim.FreezeDuringNextPublishAsync(leaseDuration + TimeSpan.FromSeconds(3));

        await stopWriters.CancelAsync();
        await writers;
        await cluster.WaitForFairSplitAsync(Patience);
        await cluster.WaitForDrainedAsync(Patience);

        // A peer republished the frozen batch; the thawed pod's renewal of the buckets it no
        // longer owns was refused, which is the one refusal that reports a real loss.
        await cluster.AssertDeliveryAsync();
        await Assert.That(cluster.DuplicatePublications()).IsGreaterThan(0);
        await Assert.That(cluster.RefusedWrites).IsGreaterThan(0);
    }

    [Test]
    public async Task Cluster_UnderStoreFaults_NeverLosesOrSharesABucket()
    {
        // Every pod's store is throttled in bursts and loses the answer to writes that were
        // applied, while the fleet goes through a rolling update under load. A lease this long
        // does not lapse over a burst, so every takeover below is a handover, not an expiry.
        var faults = new OutboxDynamoDbStoreFaults(seed: 20260920);
        await using var cluster = await OutboxDynamoDbCluster.CreateAsync(
            dynamoDb, bucketCount: 12, TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(300), storeFaults: faults);
        var running = new List<OutboxDynamoDbCluster.Pod>();
        foreach (var name in new[] { "v1-a", "v1-b", "v1-c" })
            running.Add(await cluster.StartPodAsync(name));

        using var stopWriters = new CancellationTokenSource();
        var writers = cluster.RunWritersAsync(writers: 3, keysPerWriter: 6, stopWriters.Token);
        await Task.Delay(TimeSpan.FromSeconds(3));

        for (var index = 0; index < running.Count; index++)
        {
            var replacement = await cluster.StartPodAsync($"v2-{(char)('a' + index)}");
            // A release can be throttled too. The pod still stops; its leases then lapse.
            await running[index].StopGracefullyAsync();
            running[index] = replacement;
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        await stopWriters.CancelAsync();
        await writers;
        faults.Heal();

        // Once the table recovers the fleet does too: a fair split, an empty table, nothing
        // lost, every key's first deliveries in order, and no bucket published by two pods.
        await cluster.WaitForFairSplitAsync(Patience);
        await cluster.WaitForDrainedAsync(Patience);
        await cluster.AssertDeliveryAsync();

        Console.WriteLine(
            $"[outbox-cluster] throttled={faults.Throttled} throttledDeletes={faults.ThrottledDeletes} "
            + $"lostAnswers={faults.LostAnswers}");
        await Assert.That(faults.Throttled).IsGreaterThan(0);
        await Assert.That(faults.LostAnswers).IsGreaterThan(0);
        // Only a refused delete leaves rows behind that Kafka already has, and it costs that
        // one batch once more: not a batch per retry, and not a lease. A throttled lease
        // write, renewal or heartbeat costs nothing, and neither does a lost answer, whose
        // write was applied. Each stopped pod is allowed the batch it was publishing.
        await Assert.That(cluster.DuplicatePublications())
            .IsLessThanOrEqualTo((faults.ThrottledDeletes + running.Count) * OutboxDynamoDbCluster.RelayBatchSize);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var started = TimeProvider.System.GetTimestamp();
        while (!await condition())
        {
            if (TimeProvider.System.GetElapsedTime(started) > Patience)
                Assert.Fail("The condition was not met in time.");
            await Task.Delay(50);
        }
    }

    private static string Describe(Dictionary<int, string> owners) =>
        string.Join(' ', owners.OrderBy(owner => owner.Key).Select(owner => $"{owner.Key}={owner.Value}"));
}
