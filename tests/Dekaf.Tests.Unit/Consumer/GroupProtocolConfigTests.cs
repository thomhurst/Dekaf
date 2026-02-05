using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Tests for GroupProtocol enum and ConsumerOptions configuration related to KIP-848.
/// </summary>
public sealed class GroupProtocolConfigTests
{
    private static ConsumerOptions CreateOptions() => new()
    {
        BootstrapServers = ["localhost:9092"]
    };

    #region GroupProtocol Enum

    [Test]
    public async Task GroupProtocol_HasClassicValue()
    {
        var protocol = GroupProtocol.Classic;
        await Assert.That(protocol).IsEqualTo(GroupProtocol.Classic);
    }

    [Test]
    public async Task GroupProtocol_HasConsumerValue()
    {
        var protocol = GroupProtocol.Consumer;
        await Assert.That(protocol).IsEqualTo(GroupProtocol.Consumer);
    }

    [Test]
    public async Task GroupProtocol_ClassicAndConsumer_AreDifferent()
    {
        GroupProtocol classic = GroupProtocol.Classic;
        GroupProtocol consumer = GroupProtocol.Consumer;
        await Assert.That(classic).IsNotEqualTo(consumer);
    }

    #endregion

    #region ConsumerOptions Defaults

    [Test]
    public async Task GroupProtocol_DefaultsTo_Classic()
    {
        var options = CreateOptions();
        await Assert.That(options.GroupProtocol).IsEqualTo(GroupProtocol.Classic);
    }

    [Test]
    public async Task GroupRemoteAssignor_DefaultsTo_Null()
    {
        var options = CreateOptions();
        await Assert.That(options.GroupRemoteAssignor).IsNull();
    }

    #endregion

    #region ConsumerOptions with Consumer Protocol

    [Test]
    public async Task GroupProtocol_CanBeSetTo_Consumer()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupProtocol = GroupProtocol.Consumer
        };
        await Assert.That(options.GroupProtocol).IsEqualTo(GroupProtocol.Consumer);
    }

    [Test]
    public async Task GroupRemoteAssignor_CanBeSetTo_Uniform()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupProtocol = GroupProtocol.Consumer,
            GroupRemoteAssignor = "uniform"
        };
        await Assert.That(options.GroupRemoteAssignor).IsEqualTo("uniform");
    }

    [Test]
    public async Task GroupRemoteAssignor_CanBeSetTo_Range()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupProtocol = GroupProtocol.Consumer,
            GroupRemoteAssignor = "range"
        };
        await Assert.That(options.GroupRemoteAssignor).IsEqualTo("range");
    }

    #endregion

    #region Builder Methods

    [Test]
    public async Task WithGroupProtocol_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithGroupProtocol(GroupProtocol.Consumer);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithGroupRemoteAssignor_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithGroupRemoteAssignor("uniform");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithGroupProtocol_Classic_ThenBuild_Succeeds()
    {
        var act = () => Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupProtocol(GroupProtocol.Classic)
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task WithGroupProtocol_Consumer_ThenBuild_Succeeds()
    {
        var act = () => Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupProtocol(GroupProtocol.Consumer)
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task WithGroupProtocol_Consumer_WithRemoteAssignor_ThenBuild_Succeeds()
    {
        var act = () => Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupProtocol(GroupProtocol.Consumer)
            .WithGroupRemoteAssignor("uniform")
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task WithGroupRemoteAssignor_Null_ThrowsArgumentNullException()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithGroupRemoteAssignor(null!);

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    #endregion

    #region Validation

    [Test]
    public async Task Build_WithGroupRemoteAssignor_WithoutConsumerProtocol_Throws()
    {
        var act = () => Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupRemoteAssignor("uniform")
            .Build();

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Build_WithGroupRemoteAssignor_WithClassicProtocol_Throws()
    {
        var act = () => Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupProtocol(GroupProtocol.Classic)
            .WithGroupRemoteAssignor("uniform")
            .Build();

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Build_WithGroupRemoteAssignor_WithConsumerProtocol_Succeeds()
    {
        var act = () => Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupProtocol(GroupProtocol.Consumer)
            .WithGroupRemoteAssignor("uniform")
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region Builder Chaining

    [Test]
    public async Task FullChain_WithConsumerProtocol_Succeeds()
    {
        var act = () => Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("my-group")
            .WithGroupProtocol(GroupProtocol.Consumer)
            .WithGroupRemoteAssignor("uniform")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task FullChain_WithClassicProtocol_Succeeds()
    {
        var act = () => Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("my-group")
            .WithGroupProtocol(GroupProtocol.Classic)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    #endregion
}
