using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Tests for ConsumerOptions configuration related to KIP-848.
/// </summary>
public sealed class GroupProtocolConfigTests
{
    private static ConsumerOptions CreateOptions() => new()
    {
        BootstrapServers = ["localhost:9092"]
    };

    #region ConsumerOptions Defaults

    [Test]
    public async Task GroupRemoteAssignor_DefaultsTo_Null()
    {
        var options = CreateOptions();
        await Assert.That(options.GroupRemoteAssignor).IsNull();
    }

    #endregion

    #region ConsumerOptions with Remote Assignor

    [Test]
    public async Task GroupRemoteAssignor_CanBeSetTo_Uniform()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
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
            GroupRemoteAssignor = "range"
        };
        await Assert.That(options.GroupRemoteAssignor).IsEqualTo("range");
    }

    #endregion

    #region Builder Methods

    [Test]
    public async Task WithGroupRemoteAssignor_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithGroupRemoteAssignor("uniform");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithGroupRemoteAssignor_ThenBuild_Succeeds()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupRemoteAssignor("uniform")
            .Build();

        await Assert.That(consumer).IsNotNull();
    }

    [Test]
    public async Task WithGroupRemoteAssignor_Null_ThrowsArgumentNullException()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithGroupRemoteAssignor(null!);

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    #endregion

    #region Builder Chaining

    [Test]
    public async Task FullChain_WithRemoteAssignor_Succeeds()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("my-group")
            .WithGroupRemoteAssignor("uniform")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        await Assert.That(consumer).IsNotNull();
    }

    [Test]
    public async Task FullChain_WithoutRemoteAssignor_Succeeds()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("my-group")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        await Assert.That(consumer).IsNotNull();
    }

    #endregion
}
