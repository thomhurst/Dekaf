using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Extensions.HealthChecks;
using Dekaf.Extensions.Hosting;
using Dekaf.Producer;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
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

    [Test]
    [Arguments(3000, HealthStatus.Healthy)]
    [Arguments(3001, HealthStatus.Unhealthy)]
    public async Task HeartbeatAtThreeIntervals_IsTheStalenessBoundary(int heartbeatAgeMs, HealthStatus expected)
    {
        var result = await Check(Status(Group(heartbeatAge: TimeSpan.FromMilliseconds(heartbeatAgeMs))));

        await Assert.That(result.Status).IsEqualTo(expected);
    }

    [Test]
    public async Task NonPositiveHeartbeatInterval_IsUnhealthy()
    {
        var result = await Check(Status(Group(heartbeatInterval: TimeSpan.Zero)));

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Description).Contains("stale");
    }

    [Test]
    public async Task GroupWithoutMembership_IsUnhealthy()
    {
        var result = await Check(Status(Group(hasConsumerGroup: false)));

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Description).Contains("no share group");
    }

    [Test]
    public async Task UnhealthyMember_ReportsGroupData()
    {
        var result = await Check(Status(Group(state: CoordinatorState.Joining)));

        await Assert.That(result.Data["State"]).IsEqualTo("Joining");
        await Assert.That(result.Data["MemberEpoch"]).IsEqualTo(3);
        await Assert.That(result.Data["AssignedPartitionCount"]).IsEqualTo(1);
    }

    [Test]
    public async Task Constructor_RejectsNullConsumer()
    {
        await Assert.That(() => new DekafShareConsumerHealthCheck<string, string>(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task HostedServiceKey_ResolvesTheWorkersOwnConsumer()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddDekaf(builder => builder
            .AddShareConsumerService<Worker, string, string>("worker-a", Configure)
            .AddShareConsumerService<OtherWorker, string, string>("worker-a", Configure));
        services.AddHealthChecks()
            .AddDekafShareConsumerHealthCheck<string, string>(KafkaShareConsumerServiceKey.For<Worker>("worker-a"), "worker-a");
        await using var provider = services.BuildServiceProvider();

        var check = Registrations(provider)["worker-a"].Factory(provider);
        var consumer = typeof(DekafShareConsumerHealthCheck<string, string>)
            .GetField("_consumer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(check);
        var worker = provider.GetServices<IHostedService>().OfType<Worker>().Single();

        await Assert.That(consumer).IsSameReferenceAs(worker.Consumer);
        // The worker has not started, so it holds no share group membership yet.
        await Assert.That((await RunAll(provider))["worker-a"]).IsEqualTo(HealthStatus.Unhealthy);
    }

    [Test]
    public async Task KeyedRegistrations_PassFailureStatusTagsAndOptions()
    {
        var consumerOptions = new DekafConsumerHealthCheckOptions();
        var producerOptions = new DekafProducerHealthCheckOptions();
        var brokerOptions = new DekafBrokerHealthCheckOptions();
        var services = new ServiceCollection();
        services.AddKeyedSingleton("orders", Substitute.For<IKafkaConsumer<string, string>>());
        services.AddKeyedSingleton("orders", Substitute.For<IKafkaProducer<string, string>>());
        services.AddKeyedSingleton("orders", Substitute.For<IAdminClient>());
        services.AddKeyedSingleton("orders", Substitute.For<IKafkaShareConsumer<string, string>>());
        services.AddSingleton(Substitute.For<IKafkaShareConsumer<string, string>>());
        services.AddHealthChecks()
            .AddDekafConsumerHealthCheck<string, string>("orders", "consumer", HealthStatus.Degraded, ["c"], consumerOptions)
            .AddDekafProducerHealthCheck<string, string>("orders", "producer", HealthStatus.Degraded, ["p"], producerOptions)
            .AddDekafBrokerHealthCheck("orders", "broker", HealthStatus.Degraded, ["b"], brokerOptions)
            .AddDekafShareConsumerHealthCheck<string, string>("orders", "share", HealthStatus.Degraded, ["s"])
            .AddDekafShareConsumerHealthCheck<string, string>("unkeyed-share", HealthStatus.Degraded, ["u"]);
        await using var provider = services.BuildServiceProvider();

        var registrations = Registrations(provider);

        foreach (var (name, tag) in new[] { ("consumer", "c"), ("producer", "p"), ("broker", "b"), ("share", "s"), ("unkeyed-share", "u") })
        {
            await Assert.That(registrations[name].FailureStatus).IsEqualTo(HealthStatus.Degraded);
            await Assert.That(registrations[name].Tags).IsEquivalentTo([tag]);
        }
        await Assert.That(OptionsOf(registrations["consumer"].Factory(provider))).IsSameReferenceAs(consumerOptions);
        await Assert.That(OptionsOf(registrations["producer"].Factory(provider))).IsSameReferenceAs(producerOptions);
        await Assert.That(OptionsOf(registrations["broker"].Factory(provider))).IsSameReferenceAs(brokerOptions);
    }

    [Test]
    public async Task KeyedRegistrations_WithMissingKeyedClient_FailWhenResolved()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IKafkaShareConsumer<string, string>>());
        services.AddSingleton(Substitute.For<IKafkaConsumer<string, string>>());
        services.AddHealthChecks()
            .AddDekafShareConsumerHealthCheck<string, string>("missing", "share")
            .AddDekafConsumerHealthCheck<string, string>("missing", "consumer");
        await using var provider = services.BuildServiceProvider();

        var registrations = Registrations(provider);

        // A keyed check never falls back to the unkeyed client.
        await Assert.That(() => registrations["share"].Factory(provider)).Throws<InvalidOperationException>();
        await Assert.That(() => registrations["consumer"].Factory(provider)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task KeyedRegistrations_RejectNullServiceKey()
    {
        var builder = new ServiceCollection().AddHealthChecks();

        await Assert.That(() => builder.AddDekafConsumerHealthCheck<string, string>(serviceKey: null!, "consumer")).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddDekafProducerHealthCheck<string, string>(serviceKey: null!, "producer")).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddDekafShareConsumerHealthCheck<string, string>(serviceKey: null!, "share")).Throws<ArgumentNullException>();
    }

    private static Dictionary<string, HealthCheckRegistration> Registrations(IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .ToDictionary(registration => registration.Name);

    private static object? OptionsOf(IHealthCheck check) =>
        check.GetType().GetField("_options", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(check);

    private static void Configure(ShareConsumerBuilder<string, string> consumer)
        => consumer.WithBootstrapServers("localhost:9092").WithGroupId("workers");

    private sealed class Worker(IKafkaShareConsumer<string, string> consumer)
        : KafkaShareConsumerService<string, string>(consumer, NullLogger.Instance)
    {
        public IKafkaShareConsumer<string, string> Consumer { get; } = consumer;
        protected override IEnumerable<string> Topics => ["orders"];
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> record, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class OtherWorker(IKafkaShareConsumer<string, string> consumer)
        : KafkaShareConsumerService<string, string>(consumer, NullLogger.Instance)
    {
        protected override IEnumerable<string> Topics => ["orders"];
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> record, CancellationToken cancellationToken) => ValueTask.CompletedTask;
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
        bool missingHeartbeat = false,
        TimeSpan? heartbeatInterval = null,
        bool hasConsumerGroup = true) => new()
    {
        HasConsumerGroup = hasConsumerGroup,
        State = state,
        CoordinatorId = 1,
        MemberId = memberId,
        GenerationOrMemberEpoch = 3,
        HeartbeatInterval = heartbeatInterval ?? TimeSpan.FromSeconds(1),
        TimeSinceLastHeartbeat = missingHeartbeat ? null : heartbeatAge ?? TimeSpan.FromMilliseconds(100),
        LastHeartbeatFailure = failure,
        Assignment = assignment ?? [new TopicPartition("orders", 0)]
    };

    private static HealthCheckContext Context() => new()
    {
        Registration = new HealthCheckRegistration("test", Substitute.For<IHealthCheck>(), null, null)
    };
}
