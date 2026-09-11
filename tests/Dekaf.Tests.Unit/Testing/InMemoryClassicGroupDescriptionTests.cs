using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Testing;
using Dekaf.Tests.Unit.Admin;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryClassicGroupDescriptionTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CancellationDuringFinalSnapshotCopy_PreservesCancellationOrTimeout(bool timeout)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.SetClassicGroupDescription(new() { GroupId = "group", State = "Empty", Members = [] });
        await using var admin = new InMemoryAdminClient(cluster);
        using var cancellation = new CancellationTokenSource();
        CancellationTokenSource? timeoutSource = null;
        admin.ConfigureTimeoutSourceTestHook = source => timeoutSource = source;
        var snapshots = (Dictionary<string, ClassicGroupDescription>)typeof(InMemoryKafkaCluster)
            .GetField("_classicGroupDescriptions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(cluster)!;
        // Replace only the stored fixture collection. Count is read during the final
        // snapshot copy, after the operation's preceding cancellation check.
        snapshots["group"] = new ClassicGroupDescription
        {
            GroupId = "group", State = "Empty", Members = new CancelOnCountMembers(() =>
            {
                if (timeout) timeoutSource!.Cancel();
                else cancellation.Cancel();
            })
        };

        var operation = admin.DescribeClassicGroupsAsync(["group"], cancellationToken: cancellation.Token);
        if (timeout)
            await Assert.That(async () => await operation).Throws<KafkaTimeoutException>();
        else
            await Assert.That(async () => await operation).Throws<OperationCanceledException>();
    }

    private sealed class CancelOnCountMembers(Action cancel) : IReadOnlyList<ClassicGroupMemberDescription>
    {
        public int Count
        {
            get
            {
                cancel();
                return 0;
            }
        }

        public ClassicGroupMemberDescription this[int index] => throw new ArgumentOutOfRangeException(nameof(index));
        public IEnumerator<ClassicGroupMemberDescription> GetEnumerator() =>
            ((IEnumerable<ClassicGroupMemberDescription>)Array.Empty<ClassicGroupMemberDescription>()).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Test]
    public async Task Inventory_ClassicSnapshotOverridesOverlappingFamiliesWithoutDuplicates()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("input");
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.AlterConsumerGroupOffsetsAsync("aaa", [new TopicPartitionOffset("input", 0, 0)]);
        cluster.RegisterConsumerGroupMember("overlap", "consumer", [], out _);
        cluster.RegisterShareGroupMember("overlap", "share");
        cluster.RegisterShareGroupMember("zzz", "share");
        cluster.SetClassicGroupDescription(new()
        {
            GroupId = "overlap", ProtocolType = "connect", State = "Empty", Members = []
        });

        var inventory = await admin.ListGroupsAsync();
        await Assert.That(inventory.Select(static group => group.GroupId).SequenceEqual(["aaa", "overlap", "zzz"]))
            .IsTrue();
        await Assert.That(inventory[1].GroupType).IsEqualTo("classic");
        await Assert.That(inventory[1].ProtocolType).IsEqualTo("connect");
        await Assert.That(inventory[1].State).IsEqualTo("Empty");
    }

    [Test]
    [Arguments("consumer")]
    [Arguments("connect")]
    public async Task Fixtures_CopyProtocolBytesAndPreserveGroupOutcomes(string protocol)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var bytes = AdminClientClassicGroupDescriptionTests.AssignmentBytes();
        var original = bytes.ToArray();
        var members = new ClassicGroupMemberDescription[]
        {
            new() { MemberId = "member", AssignmentData = bytes, Metadata = bytes }
        };
        cluster.SetClassicGroupDescription(new()
        {
            GroupId = "fixture", ProtocolType = protocol, ProtocolData = "selected", State = "Stable",
            CoordinatorId = 12, AuthorizedOperations = 123, Members = members
        });
        bytes[0] = 99;
        members[0] = new() { MemberId = "changed" };
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin, groupId: "denied"),
            new GroupException(ErrorCode.GroupAuthorizationFailed, "denied"));
        var results = await admin.DescribeClassicGroupsAsync(["fixture", "denied", "missing"],
            new() { IncludeAuthorizedOperations = true });
        var description = results["fixture"].Description!;
        await Assert.That(description.CoordinatorId).IsEqualTo(12);
        await Assert.That(description.AuthorizedOperations).IsEqualTo(123);
        await Assert.That(description.Members[0].MemberId).IsEqualTo("member");
        await Assert.That(description.Members[0].AssignmentData.ToArray()).IsEquivalentTo(original);
        await Assert.That(description.Members[0].Assignment is not null).IsEqualTo(protocol == "consumer");
        await Assert.That(results["denied"].ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await Assert.That(results["missing"].ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
        var inventory = await admin.ListGroupsAsync();
        await Assert.That(inventory.Single().GroupType).IsEqualTo("classic");
        await Assert.That(inventory.Single().ProtocolType).IsEqualTo(protocol);
        await Assert.That((await admin.DescribeClassicGroupsAsync(["fixture"]))["fixture"].Description!.AuthorizedOperations).IsNull();
    }

    [Test]
    public async Task DeleteSeededEmptyGroup_RemovesDescriptionAndInventory()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        cluster.SetClassicGroupDescription(new() { GroupId = "seeded", State = "Empty", ProtocolType = "consumer", Members = [] });
        await admin.DeleteConsumerGroupsAsync(["seeded"]);
        await Assert.That((await admin.DescribeClassicGroupsAsync(["seeded"]))["seeded"].ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
        await Assert.That(await admin.ListGroupsAsync()).IsEmpty();
    }

    [Test]
    public async Task DeleteSeededActiveGroup_PreservesMembersAndReportsNonEmpty()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        cluster.SetClassicGroupDescription(new()
        {
            GroupId = "seeded", State = "Stable", ProtocolType = "consumer", Members = [new() { MemberId = "member" }]
        });
        var exception = await Assert.ThrowsAsync<GroupException>(() => admin.DeleteConsumerGroupsAsync(["seeded"]).AsTask());
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.NonEmptyGroup);
        await Assert.That((await admin.DescribeClassicGroupsAsync(["seeded"]))["seeded"].Description!.Members.Count).IsEqualTo(1);
    }

    [Test]
    public async Task OffsetOnlyGroup_HasEmptyClassicDescription()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic");
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.AlterConsumerGroupOffsetsAsync("simple", [new TopicPartitionOffset("topic", 0, 0)]);
        var description = (await admin.DescribeClassicGroupsAsync(["simple"]))["simple"].Description!;
        await Assert.That(description.State).IsEqualTo("Empty");
        await Assert.That(description.ProtocolType).IsEqualTo("");
        await Assert.That(description.Members).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FaultBarrier_RespectsCallerCancellationAndDeadline(bool timeout)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var barrier = cluster.FaultPlan.PauseNext(new KafkaFaultScope(KafkaFaultOperation.Admin, groupId: "group"));
        using var cancellation = new CancellationTokenSource();
        var operation = admin.DescribeClassicGroupsAsync(["group"], new() { TimeoutMs = timeout ? 100 : 30000 }, cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync();
        if (timeout)
            await Assert.That(async () => await operation).Throws<KafkaTimeoutException>();
        else
        {
            cancellation.Cancel();
            await Assert.That(async () => await operation).Throws<OperationCanceledException>();
        }
        barrier.Release();
    }

    [Test]
    public async Task ValidationAndEmptyInput_LeaveFaultPlanUntouched()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin), new InvalidOperationException("pending"));
        await Assert.That(async () => await admin.DescribeClassicGroupsAsync(["a", "a"])).Throws<ArgumentException>();
        await Assert.That(async () => await admin.DescribeClassicGroupsAsync([], new() { TimeoutMs = -1 })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(await admin.DescribeClassicGroupsAsync([])).IsEmpty();
        await Assert.That(async () => await admin.DescribeClassicGroupsAsync(["a"])).Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments(false, ErrorCode.GroupAuthorizationFailed)]
    [Arguments(false, ErrorCode.NotCoordinator)]
    [Arguments(true, ErrorCode.GroupAuthorizationFailed)]
    [Arguments(true, ErrorCode.NotCoordinator)]
    public async Task ScopedFaultAfterCancellation_PreservesCancellationOrTimeout(bool timeout, ErrorCode code)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.SetClassicGroupDescription(new() { GroupId = "first", State = "Empty", Members = [] });
        await using var admin = new InMemoryAdminClient(cluster);
        using var cancellation = new CancellationTokenSource();
        CancellationTokenSource? timeoutSource = null;
        admin.ConfigureTimeoutSourceTestHook = source => timeoutSource = source;
        var observed = 0;
        cluster.FaultPlan.FaultConsumed += _ =>
        {
            observed++;
            if (timeout) timeoutSource!.Cancel();
            else cancellation.Cancel();
        };
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin, groupId: "fault"),
            new GroupException(code, "scoped failure"));

        // Cancellation occurs after the fault plan's token check, immediately before
        // its KafkaException is returned. The last group must not suppress cancellation.
        var operation = admin.DescribeClassicGroupsAsync(["first", "fault"], cancellationToken: cancellation.Token);
        if (timeout)
            await Assert.That(async () => await operation).Throws<KafkaTimeoutException>();
        else
            await Assert.That(async () => await operation).Throws<OperationCanceledException>();
        await Assert.That(observed).IsEqualTo(1);
    }

    [Test]
    public async Task DisposedClient_PreservesValidationPrecedence()
    {
        var admin = new InMemoryAdminClient(new InMemoryKafkaCluster());
        await admin.DisposeAsync();
        await Assert.That(async () => await admin.DescribeClassicGroupsAsync(["a", "a"]))
            .Throws<ObjectDisposedException>();
        await Assert.That(async () => await admin.DescribeClassicGroupsAsync(null!))
            .Throws<ArgumentNullException>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.That(async () => await admin.DescribeClassicGroupsAsync(["a"], cancellationToken: cancellation.Token))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task GenerationTrackedGroup_IsListedAsConsumerButNotDescribedAsClassic()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("input");
        cluster.RegisterConsumerGroupMember("consumer", "member", [new TopicPartition("input", 0)], out var registration);
        await using var admin = new InMemoryAdminClient(cluster);
        try
        {
            var listing = (await admin.ListGroupsAsync()).Single();
            await Assert.That(listing.GroupId).IsEqualTo("consumer");
            await Assert.That(listing.GroupType).IsEqualTo("consumer");
            var result = (await admin.DescribeClassicGroupsAsync(["consumer"]))["consumer"];
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
            await Assert.That(result.Description).IsNull();
        }
        finally
        {
            cluster.UnregisterConsumerGroupMember("consumer", "member", registration);
        }
    }
}
