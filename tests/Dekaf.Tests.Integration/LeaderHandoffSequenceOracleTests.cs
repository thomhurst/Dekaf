using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("Producer")]
public sealed class LeaderHandoffSequenceOracleTests
{
    [Test]
    public async Task NonIdempotent_AdjacentRetryCopies_MatchAcknowledgedOffsets()
    {
        var oracle = CreateOracle(false, 1, 2, 4);

        oracle.Observe(0, 0, "0", "0");
        oracle.Observe(0, 1, "0", "0");
        oracle.Observe(0, 2, "1", "1");
        oracle.Observe(0, 3, "2", "2");
        oracle.Observe(0, 4, "2", "2");
        oracle.EnsureComplete();

        await Assert.That(oracle.IsComplete).IsTrue();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task OrderedRecordsWithoutRetries_Complete(bool enableIdempotence)
    {
        var oracle = CreateOracle(enableIdempotence, 0, 1);
        oracle.Observe(0, 0, "0", "0");
        oracle.Observe(0, 1, "1", "1");
        oracle.EnsureComplete();

        await Assert.That(oracle.IsComplete).IsTrue();
    }

    [Test]
    public async Task Idempotent_RetryOffsetGap_Throws()
    {
        await Assert.That(() => CreateOracle(true, 1, 2)).Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments(-1, 1)]
    [Arguments(1, 1)]
    [Arguments(2, 1)]
    public async Task NonIdempotent_InvalidAcknowledgementOrder_Throws(long first, long second)
    {
        await Assert.That(() => CreateOracle(false, first, second)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task MissingAcknowledgement_Throws()
    {
        var acknowledgements = new[] { CreateMetadata(0) };
        await Assert.That(() => new LeaderHandoffSequenceOracle(acknowledgements, 2, false))
            .Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments(0, 1, "0", "0")]
    [Arguments(1, 0, "0", "0")]
    [Arguments(0, 0, "0", "1")]
    [Arguments(0, 0, "1", "1")]
    [Arguments(0, 0, "bad", "bad")]
    [Arguments(0, 0, null, null)]
    public async Task NonIdempotent_CorruptOrReorderedRecord_Throws(
        int partition, long offset, string? key, string? value)
    {
        var oracle = CreateOracle(false, 1, 2);
        await Assert.That(() => oracle.Observe(partition, offset, key, value))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task NonIdempotent_NextInputBeforeAcknowledgedCopy_Throws()
    {
        var oracle = CreateOracle(false, 1, 2);
        oracle.Observe(0, 0, "0", "0");
        await Assert.That(() => oracle.Observe(0, 1, "1", "1"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task DuplicateAfterAcknowledgedCopy_Throws(bool enableIdempotence)
    {
        var oracle = CreateOracle(enableIdempotence, 0, 1);
        oracle.Observe(0, 0, "0", "0");
        await Assert.That(() => oracle.Observe(0, 1, "0", "0"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task NonIdempotent_MissingAcknowledgedCopy_Throws()
    {
        var oracle = CreateOracle(false, 1);
        oracle.Observe(0, 0, "0", "0");

        await Assert.That(oracle.IsComplete).IsFalse();
        await Assert.That(oracle.EnsureComplete).Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ExtraRecordAfterCompletion_Throws(bool enableIdempotence)
    {
        var oracle = CreateOracle(enableIdempotence, 0);
        oracle.Observe(0, 0, "0", "0");
        await Assert.That(() => oracle.Observe(0, 1, "0", "0"))
            .Throws<InvalidOperationException>();
    }

    private static LeaderHandoffSequenceOracle CreateOracle(bool enableIdempotence, params long[] offsets)
    {
        var acknowledgements = offsets.Select(CreateMetadata).ToArray();
        return new LeaderHandoffSequenceOracle(acknowledgements, offsets.Length, enableIdempotence);
    }

    private static RecordMetadata CreateMetadata(long offset) => new()
    {
        Topic = "topic",
        Partition = 0,
        Offset = offset,
        Timestamp = DateTimeOffset.UnixEpoch
    };
}
