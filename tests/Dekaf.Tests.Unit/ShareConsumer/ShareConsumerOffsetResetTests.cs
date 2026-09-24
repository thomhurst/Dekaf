using System.Reflection;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed class ShareConsumerOffsetResetTests
{
    [Test]
    [Arguments(null, null, null)]
    [Arguments(AutoOffsetReset.Earliest, null, "earliest")]
    [Arguments(AutoOffsetReset.Latest, null, "latest")]
    [Arguments(AutoOffsetReset.ByDuration, 0, "by_duration:PT0S")]
    [Arguments(AutoOffsetReset.ByDuration, 1500, "by_duration:PT1.5S")]
    [Arguments(AutoOffsetReset.ByDuration, 86400000, "by_duration:P1D")]
    public async Task Initialize_AppliesExplicitPolicyOnlyOnce(AutoOffsetReset? reset, int? milliseconds, string? expected)
    {
        await using var fixture = new Fixture(Options(reset, milliseconds));
        await fixture.Consumer.InitializeAsync();
        await fixture.Consumer.InitializeAsync();
        await Assert.That(fixture.Requests.Count).IsEqualTo(expected is null ? 0 : 1);
        if (expected is not null)
        {
            var request = fixture.Requests.Single();
            await Assert.That(request.ValidateOnly).IsFalse();
            var resource = request.Resources.Single();
            await Assert.That(resource.ResourceType).IsEqualTo((sbyte)ConfigResourceType.Group);
            await Assert.That(resource.ResourceName).IsEqualTo("offset-reset");
            var config = resource.Configs.Single();
            await Assert.That(config.Name).IsEqualTo("share.auto.offset.reset");
            await Assert.That(config.ConfigOperation).IsEqualTo((sbyte)0);
            await Assert.That(config.Value).IsEqualTo(expected);
        }
        await fixture.Pool.DidNotReceive().DisposeAsync();
        // Initialization cannot join before the group configuration has been applied.
        await fixture.Connection.DidNotReceive().SendAsync<ShareGroupHeartbeatRequest, ShareGroupHeartbeatResponse>(
            Arg.Any<ShareGroupHeartbeatRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AuthorizationFailure_PreventsInitializationAndCanBeRetriedExplicitly()
    {
        await using var fixture = new Fixture(Options(AutoOffsetReset.Earliest));
        fixture.Error = ErrorCode.GroupAuthorizationFailed;
        await Assert.That(async () => await fixture.Consumer.InitializeAsync()).Throws<AuthorizationException>();
        fixture.Consumer.Subscribe("topic");
        await using var records = fixture.Consumer.PollAsync().GetAsyncEnumerator();
        await Assert.That(async () => await records.MoveNextAsync()).Throws<InvalidOperationException>();
        fixture.Error = ErrorCode.None;
        await fixture.Consumer.InitializeAsync();
        await Assert.That(fixture.Requests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task CancellationDuringConfiguration_LeavesInitializationRetryable()
    {
        await using var fixture = new Fixture(Options(AutoOffsetReset.Earliest));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<IncrementalAlterConfigsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Connection.SendAsync<IncrementalAlterConfigsRequest, IncrementalAlterConfigsResponse>(
            Arg.Any<IncrementalAlterConfigsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => { entered.TrySetResult(); return new(response.Task.WaitAsync(call.ArgAt<CancellationToken>(2))); });
        using var cancellation = new CancellationTokenSource();
        var initialize = fixture.Consumer.InitializeAsync(cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await cancellation.CancelAsync();
        await Assert.That(async () => await initialize).Throws<OperationCanceledException>();
        response.SetResult(fixture.Response());
        await fixture.Consumer.InitializeAsync();
        await fixture.Connection.Received(2).SendAsync<IncrementalAlterConfigsRequest, IncrementalAlterConfigsResponse>(
            Arg.Any<IncrementalAlterConfigsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
        await fixture.Pool.DidNotReceive().DisposeAsync();
    }

    [Test]
    public async Task ConcurrentInitialization_WaitsForConfigurationAndWritesOnce()
    {
        await using var fixture = new Fixture(Options(AutoOffsetReset.Earliest));
        var response = new TaskCompletionSource<IncrementalAlterConfigsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Connection.SendAsync<IncrementalAlterConfigsRequest, IncrementalAlterConfigsResponse>(
            Arg.Any<IncrementalAlterConfigsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IncrementalAlterConfigsResponse>(response.Task));
        var first = fixture.Consumer.InitializeAsync().AsTask();
        var second = fixture.Consumer.InitializeAsync().AsTask();
        await Assert.That(first.IsCompleted).IsFalse();
        await Assert.That(second.IsCompleted).IsFalse();
        response.SetResult(fixture.Response());
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        await fixture.Connection.Received(1).SendAsync<IncrementalAlterConfigsRequest, IncrementalAlterConfigsResponse>(
            Arg.Any<IncrementalAlterConfigsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(AutoOffsetReset.None)]
    [Arguments(AutoOffsetReset.ByDuration)]
    [Arguments((AutoOffsetReset)99)]
    public async Task Builder_RejectsUnsupportedOrIncompletePolicy(AutoOffsetReset reset)
    {
        await Assert.That(() => Kafka.CreateShareConsumer<string, string>().WithAutoOffsetReset(reset))
            .Throws<ArgumentException>();
    }

    [Test]
    [Arguments(AutoOffsetReset.None, null)]
    [Arguments((AutoOffsetReset)99, null)]
    [Arguments(AutoOffsetReset.ByDuration, null)]
    [Arguments(AutoOffsetReset.ByDuration, -1)]
    [Arguments(AutoOffsetReset.Earliest, 1000)]
    [Arguments(null, 1000)]
    public async Task Options_RejectInvalidPolicyAndDuration(AutoOffsetReset? reset, int? milliseconds)
    {
        await Assert.That(() => ShareAutoOffsetResetStrategy.GetConfigValue(Options(reset, milliseconds)))
            .Throws<ArgumentException>();
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder.AddShareConsumer<string, string>(Options(reset, milliseconds)));
        await using var provider = services.BuildServiceProvider();
        await Assert.That(() => provider.GetRequiredService<IKafkaShareConsumer<string, string>>())
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Builder_ValidatesDurationAndClearsItWhenPolicyChanges()
    {
        var builder = Kafka.CreateShareConsumer<string, string>().WithBootstrapServers("localhost:9092").WithGroupId("offset-reset");
        await Assert.That(() => builder.WithAutoOffsetResetByDuration(TimeSpan.FromTicks(-1))).Throws<ArgumentOutOfRangeException>();
        builder.WithAutoOffsetResetByDuration(TimeSpan.FromSeconds(10));
        await using var duration = builder.Build();
        await Assert.That(GetOptions(duration).AutoOffsetResetDuration).IsEqualTo(TimeSpan.FromSeconds(10));
        builder.WithAutoOffsetReset(AutoOffsetReset.Latest);
        await using var latest = builder.Build();
        await Assert.That(GetOptions(latest).AutoOffsetReset).IsEqualTo(AutoOffsetReset.Latest);
        await Assert.That(GetOptions(latest).AutoOffsetResetDuration).IsNull();
    }

    [Test]
    [Arguments(null, null)]
    [Arguments(AutoOffsetReset.Earliest, null)]
    [Arguments(AutoOffsetReset.ByDuration, 1000)]
    public async Task DependencyInjection_PreservesOptions(AutoOffsetReset? reset, int? milliseconds)
    {
        var options = Options(reset, milliseconds);
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder.AddShareConsumer<string, string>(options));
        await using var provider = services.BuildServiceProvider();
        var consumer = provider.GetRequiredService<IKafkaShareConsumer<string, string>>();
        await Assert.That(GetOptions(consumer).AutoOffsetReset).IsEqualTo(options.AutoOffsetReset);
        await Assert.That(GetOptions(consumer).AutoOffsetResetDuration).IsEqualTo(options.AutoOffsetResetDuration);
    }

    [Test]
    [Arguments(false, null, null, null)]
    [Arguments(true, null, null, null)]
    [Arguments(false, "Earliest", null, "earliest")]
    [Arguments(true, "Earliest", null, "earliest")]
    [Arguments(false, "ByDuration", "00:00:01.500", "by_duration:PT1.5S")]
    [Arguments(true, "ByDuration", "00:00:01.500", "by_duration:PT1.5S")]
    public async Task ConfigurationBinding_PreservesPolicyAndDuration(
        bool keyed, string? policy, string? duration, string? expectedConfigValue)
    {
        var values = new Dictionary<string, string?>
        {
            ["BootstrapServers:0"] = "localhost:9092",
            ["GroupId"] = "offset-reset"
        };
        if (policy is not null) values["AutoOffsetReset"] = policy;
        if (duration is not null) values["AutoOffsetResetDuration"] = duration;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddDekaf(builder =>
        {
            if (keyed) builder.AddShareConsumer<string, string>("configured", configuration);
            else builder.AddShareConsumer<string, string>(configuration);
        });
        await using var provider = services.BuildServiceProvider();
        var consumer = keyed
            ? provider.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>("configured")
            : provider.GetRequiredService<IKafkaShareConsumer<string, string>>();
        var options = GetOptions(consumer);
        await Assert.That(options.AutoOffsetReset).IsEqualTo(policy switch
        {
            "Earliest" => AutoOffsetReset.Earliest,
            "ByDuration" => AutoOffsetReset.ByDuration,
            _ => (AutoOffsetReset?)null
        });
        await Assert.That(options.AutoOffsetResetDuration)
            .IsEqualTo(duration is null ? (TimeSpan?)null : TimeSpan.FromMilliseconds(1500));
        await Assert.That(ShareAutoOffsetResetStrategy.GetConfigValue(options)).IsEqualTo(expectedConfigValue);
    }

    private static ShareConsumerOptions GetOptions(IKafkaShareConsumer<string, string> consumer) =>
        (ShareConsumerOptions)consumer.GetType().GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(consumer)!;

    private static ShareConsumerOptions Options(AutoOffsetReset? reset = null, int? milliseconds = null) => new()
    {
        BootstrapServers = ["localhost:9092"], GroupId = "offset-reset", AutoOffsetReset = reset,
        AutoOffsetResetDuration = milliseconds is { } value ? TimeSpan.FromMilliseconds(value) : null
    };

    private sealed class Fixture : IAsyncDisposable
    {
        internal readonly IKafkaConnection Connection = Substitute.For<IKafkaConnection>();
        internal readonly IConnectionPool Pool = Substitute.For<IConnectionPool>();
        internal readonly List<IncrementalAlterConfigsRequest> Requests = [];
        internal readonly KafkaShareConsumer<string, string> Consumer;
        private readonly MetadataManager _metadata;
        internal ErrorCode Error;

        internal Fixture(ShareConsumerOptions options)
        {
            Connection.BrokerId.Returns(1);
            Connection.IsConnected.Returns(true);
            Pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Connection);
            _metadata = new MetadataManager(Pool, options.BootstrapServers);
            _metadata.Metadata.Update(new MetadataResponse
            {
                Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
                ControllerId = 1, Topics = []
            });
            _metadata.SetApiVersion(ApiKey.IncrementalAlterConfigs, 1, 1);
            typeof(MetadataManager).GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_metadata, true);
            Connection.SendAsync<IncrementalAlterConfigsRequest, IncrementalAlterConfigsResponse>(
                Arg.Any<IncrementalAlterConfigsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => { Requests.Add(call.ArgAt<IncrementalAlterConfigsRequest>(0)); return new(Response()); });
            Consumer = new KafkaShareConsumer<string, string>(options,
                Substitute.For<IDeserializer<string>>(), Substitute.For<IDeserializer<string>>(), Pool, _metadata);
        }
        internal IncrementalAlterConfigsResponse Response() => new()
        {
            Responses = [new IncrementalAlterConfigsResourceResponse
                { ResourceType = (sbyte)ConfigResourceType.Group, ResourceName = "offset-reset", ErrorCode = Error }]
        };
        public async ValueTask DisposeAsync()
        {
            await Consumer.DisposeAsync();
            await _metadata.DisposeAsync();
        }
    }
}
