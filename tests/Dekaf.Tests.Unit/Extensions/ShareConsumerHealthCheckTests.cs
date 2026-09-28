using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Extensions.HealthChecks;
using Dekaf.Producer;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Dekaf.Tests.Unit.Extensions;

public class ShareConsumerHealthCheckTests
{
    [Test]
    public async Task StableMemberWithFreshHeartbeat_IsHealthy()
    {
        var result = await Check(Status(Group()));

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Data["State"]).IsEqualTo("Stable");
    }

    [Test]
    public async Task IdleMemberWithoutAssignment_IsHealthy()
    {
        var result = await Check(Status(Group(assignment: [])));

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Data["AssignedPartitionCount"]).IsEqualTo(0);
    }

    [Test]
    public async Task StoppedConsumer_IsUnhealthy()
    {
        var result = await Check(Status(Group(), isStopped: true));

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Description).Contains("stopped");
    }

    [Test]
    public async Task MissingGroup_IsUnhealthy()
    {
        var result = await Check(Status(group: null));

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
    }

    [Test]
    [Arguments(CoordinatorState.Unjoined)]
    [Arguments(CoordinatorState.Joining)]
    public async Task UnstableMember_IsUnhealthy(CoordinatorState state)
    {
        var result = await Check(Status(Group(state: state)));

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
    }

    [Test]
    public async Task MissingMemberId_IsUnhealthy()
    {
        var result = await Check(Status(Group(memberId: null)));

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
    }

    [Test]
    public async Task HeartbeatFailure_IsUnhealthy()
    {
        var result = await Check(Status(Group(failure: "COORDINATOR_NOT_AVAILABLE")));

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Description).Contains("COORDINATOR_NOT_AVAILABLE");
    }

    [Test]
    [Arguments(null)]
    [Arguments(10_000)]
    public async Task MissingOrStaleHeartbeat_IsUnhealthy(int? heartbeatAgeMs)
    {
        var group = heartbeatAgeMs is { } ms
            ? Group(heartbeatAge: TimeSpan.FromMilliseconds(ms))
            : Group(missingHeartbeat: true);

        var result = await Check(Status(group));

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
    }

    [Test]
    public async Task ConsumerWithoutStatus_IsUnhealthy()
    {
        var consumer = Substitute.For<IKafkaShareConsumer<string, string>>();

        var result = await new DekafShareConsumerHealthCheck<string, string>(consumer).CheckHealthAsync(Context());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
    }

    [Test]
    public async Task AddDekafShareConsumerHealthCheck_ResolvesUnkeyedAndKeyedConsumers()
    {
        var unkeyed = Consumer(Status(Group()));
        var keyed = Consumer(Status(Group(), isStopped: true));
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(unkeyed);
        services.AddKeyedSingleton("orders", keyed);
        services.AddHealthChecks()
            .AddDekafShareConsumerHealthCheck<string, string>()
            .AddDekafShareConsumerHealthCheck<string, string>("orders", "orders-share");
        await using var provider = services.BuildServiceProvider();

        var results = await RunAll(provider);

        await Assert.That(results["dekaf-share-consumer"]).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(results["orders-share"]).IsEqualTo(HealthStatus.Unhealthy);
    }

    [Test]
    public async Task KeyedRegistrations_ResolveKeyedClients()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        var producer = Substitute.For<IKafkaProducer<string, string>>();
        var admin = Substitute.For<IAdminClient>();
        var services = new ServiceCollection();
        services.AddKeyedSingleton("orders", consumer);
        services.AddKeyedSingleton("orders", producer);
        services.AddKeyedSingleton("orders", admin);
        services.AddHealthChecks()
            .AddDekafConsumerHealthCheck<string, string>("orders", "orders-consumer")
            .AddDekafProducerHealthCheck<string, string>("orders", "orders-producer")
            .AddDekafBrokerHealthCheck("orders", "orders-broker");
        await using var provider = services.BuildServiceProvider();

        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .ToDictionary(registration => registration.Name);

        await Assert.That(registrations.Keys).IsEquivalentTo(["orders-consumer", "orders-producer", "orders-broker"]);
        await Assert.That(registrations["orders-consumer"].Factory(provider)).IsTypeOf<DekafConsumerHealthCheck<string, string>>();
        await Assert.That(registrations["orders-producer"].Factory(provider)).IsTypeOf<DekafProducerHealthCheck<string, string>>();
        await Assert.That(registrations["orders-broker"].Factory(provider)).IsTypeOf<DekafBrokerHealthCheck>();
        await Assert.That(() => new ServiceCollection().AddHealthChecks()
            .AddDekafBrokerHealthCheck(serviceKey: null!, "missing")).Throws<ArgumentNullException>();
    }

    private static async Task<Dictionary<string, HealthStatus>> RunAll(IServiceProvider provider)
    {
        var report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();
        return report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status);
    }

    private static Task<HealthCheckResult> Check(KafkaClientStatus status)
        => new DekafShareConsumerHealthCheck<string, string>(Consumer(status)).CheckHealthAsync(Context());

    private static IKafkaShareConsumer<string, string> Consumer(KafkaClientStatus status)
    {
        var consumer = Substitute.For<IKafkaShareConsumer<string, string>, IKafkaClientStatusProvider>();
        ((IKafkaClientStatusProvider)consumer).GetStatus().Returns(status);
        return consumer;
    }

    private static KafkaClientStatus Status(ConsumerGroupStatus? group, bool isStopped = false) => new()
    {
        CapturedAtUtc = DateTimeOffset.UtcNow,
        Role = KafkaClientRole.ShareConsumer,
        IsStopped = isStopped,
        Brokers = [],
        ConsumerGroup = group
    };

    private static ConsumerGroupStatus Group(
        CoordinatorState state = CoordinatorState.Stable,
        string? memberId = "member-1",
        TimeSpan? heartbeatAge = null,
        string? failure = null,
        IReadOnlyList<TopicPartition>? assignment = null,
        bool missingHeartbeat = false) => new()
    {
        HasConsumerGroup = true,
        State = state,
        CoordinatorId = 1,
        MemberId = memberId,
        GenerationOrMemberEpoch = 3,
        HeartbeatInterval = TimeSpan.FromSeconds(1),
        TimeSinceLastHeartbeat = missingHeartbeat ? null : heartbeatAge ?? TimeSpan.FromMilliseconds(100),
        LastHeartbeatFailure = failure,
        Assignment = assignment ?? [new TopicPartition("orders", 0)]
    };

    private static HealthCheckContext Context() => new()
    {
        Registration = new HealthCheckRegistration("test", Substitute.For<IHealthCheck>(), null, null)
    };
}
