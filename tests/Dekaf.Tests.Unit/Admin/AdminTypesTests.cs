using Dekaf.Admin;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Admin;

/// <summary>
/// Tests for admin API types.
/// </summary>
public class AdminTypesTests
{
    #region TopicPartitionOffsetSpec Tests

    [Test]
    public async Task TopicPartitionOffsetSpec_CanBeCreated()
    {
        var spec = new TopicPartitionOffsetSpec
        {
            TopicPartition = new TopicPartition("test-topic", 0),
            Spec = OffsetSpec.Latest
        };

        await Assert.That(spec.TopicPartition.Topic).IsEqualTo("test-topic");
        await Assert.That(spec.TopicPartition.Partition).IsEqualTo(0);
        await Assert.That(spec.Spec).IsEqualTo(OffsetSpec.Latest);
        await Assert.That(spec.Timestamp).IsNull();
    }

    [Test]
    public async Task TopicPartitionOffsetSpec_WithTimestamp()
    {
        var spec = new TopicPartitionOffsetSpec
        {
            TopicPartition = new TopicPartition("test-topic", 1),
            Spec = OffsetSpec.Timestamp,
            Timestamp = 1234567890L
        };

        await Assert.That(spec.Spec).IsEqualTo(OffsetSpec.Timestamp);
        await Assert.That(spec.Timestamp).IsEqualTo(1234567890L);
    }

    [Test]
    public async Task TopicPartitionOffsetSpec_AllOffsetSpecs()
    {
        var specEarliest = new TopicPartitionOffsetSpec
        {
            TopicPartition = new TopicPartition("test", 0),
            Spec = OffsetSpec.Earliest
        };
        var specLatest = new TopicPartitionOffsetSpec
        {
            TopicPartition = new TopicPartition("test", 0),
            Spec = OffsetSpec.Latest
        };
        var specMaxTimestamp = new TopicPartitionOffsetSpec
        {
            TopicPartition = new TopicPartition("test", 0),
            Spec = OffsetSpec.MaxTimestamp
        };

        await Assert.That(specEarliest.Spec).IsEqualTo(OffsetSpec.Earliest);
        await Assert.That(specLatest.Spec).IsEqualTo(OffsetSpec.Latest);
        await Assert.That(specMaxTimestamp.Spec).IsEqualTo(OffsetSpec.MaxTimestamp);
    }

    #endregion

    #region ListOffsetsResultInfo Tests

    [Test]
    public async Task ListOffsetsResultInfo_CanBeCreated()
    {
        var result = new ListOffsetsResultInfo
        {
            Offset = 100,
            Timestamp = 1234567890L,
            LeaderEpoch = 5
        };

        await Assert.That(result.Offset).IsEqualTo(100L);
        await Assert.That(result.Timestamp).IsEqualTo(1234567890L);
        await Assert.That(result.LeaderEpoch).IsEqualTo(5);
    }

    [Test]
    public async Task ListOffsetsResultInfo_DefaultTimestamp()
    {
        var result = new ListOffsetsResultInfo
        {
            Offset = 100
        };

        await Assert.That(result.Timestamp).IsEqualTo(-1L);
        await Assert.That(result.LeaderEpoch).IsNull();
    }

    #endregion

    #region ListOffsetsOptions Tests

    [Test]
    public async Task ListOffsetsOptions_Defaults()
    {
        var options = new ListOffsetsOptions();

        await Assert.That(options.TimeoutMs).IsEqualTo(30000);
        await Assert.That(options.IsolationLevel).IsEqualTo(IsolationLevel.ReadUncommitted);
    }

    [Test]
    public async Task ListOffsetsOptions_CanBeConfigured()
    {
        var options = new ListOffsetsOptions
        {
            TimeoutMs = 60000,
            IsolationLevel = IsolationLevel.ReadCommitted
        };

        await Assert.That(options.TimeoutMs).IsEqualTo(60000);
        await Assert.That(options.IsolationLevel).IsEqualTo(IsolationLevel.ReadCommitted);
    }

    #endregion

    #region ElectLeadersOptions Tests

    [Test]
    public async Task ElectLeadersOptions_Defaults()
    {
        var options = new ElectLeadersOptions();

        await Assert.That(options.TimeoutMs).IsEqualTo(30000);
    }

    #endregion

    #region ElectLeadersResultInfo Tests

    [Test]
    public async Task ElectLeadersResultInfo_Success()
    {
        var result = new ElectLeadersResultInfo
        {
            TopicPartition = new TopicPartition("test-topic", 0),
            ErrorCode = ErrorCode.None
        };

        await Assert.That(result.TopicPartition.Topic).IsEqualTo("test-topic");
        await Assert.That(result.TopicPartition.Partition).IsEqualTo(0);
        await Assert.That(result.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(result.ErrorMessage).IsNull();
    }

    [Test]
    public async Task ElectLeadersResultInfo_WithError()
    {
        var result = new ElectLeadersResultInfo
        {
            TopicPartition = new TopicPartition("test-topic", 1),
            ErrorCode = ErrorCode.PreferredLeaderNotAvailable,
            ErrorMessage = "Preferred leader not available"
        };

        await Assert.That(result.ErrorCode).IsEqualTo(ErrorCode.PreferredLeaderNotAvailable);
        await Assert.That(result.ErrorMessage).IsEqualTo("Preferred leader not available");
    }

    #endregion

    #region ElectionType Tests

    [Test]
    public async Task ElectionType_Preferred()
    {
        var electionType = ElectionType.Preferred;
        await Assert.That(electionType).IsEqualTo(ElectionType.Preferred);
    }

    [Test]
    public async Task ElectionType_Unclean()
    {
        var electionType = ElectionType.Unclean;
        await Assert.That(electionType).IsEqualTo(ElectionType.Unclean);
    }

    #endregion
}
