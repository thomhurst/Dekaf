namespace Dekaf.StressTests.FaultInjection;

internal sealed record FaultWindowVerification(
    bool Succeeded,
    long UnexplainedLossCount,
    long DuplicateCount,
    long OracleCountMismatch,
    IReadOnlyList<long> MissingIds,
    IReadOnlyList<long> DuplicateIds,
    IReadOnlyList<long> UnexpectedIds);

internal static class FaultWindowVerifier
{
    internal static FaultWindowVerification Verify(
        long acceptedMessageCount,
        long brokerDeliveredCount,
        IEnumerable<long> deliveryErrorIds,
        IEnumerable<long> consumedIds,
        bool requireZeroDuplicates)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(acceptedMessageCount);
        ArgumentOutOfRangeException.ThrowIfNegative(brokerDeliveredCount);
        ArgumentNullException.ThrowIfNull(deliveryErrorIds);
        ArgumentNullException.ThrowIfNull(consumedIds);

        var errors = deliveryErrorIds.ToHashSet();
        if (errors.Any(id => id < 0 || id >= acceptedMessageCount))
        {
            throw new ArgumentOutOfRangeException(nameof(deliveryErrorIds),
                "Delivery-error IDs must identify accepted messages.");
        }

        var seen = new HashSet<long>();
        var duplicateIds = new HashSet<long>();
        long consumedCount = 0;
        foreach (var id in consumedIds)
        {
            consumedCount++;
            if (!seen.Add(id))
            {
                duplicateIds.Add(id);
            }
        }

        var missingIds = new List<long>();
        for (var id = 0L; id < acceptedMessageCount; id++)
        {
            if (!errors.Contains(id) && !seen.Contains(id))
            {
                missingIds.Add(id);
            }
        }

        var unexpectedIds = seen
            .Where(id => id < 0 || id >= acceptedMessageCount)
            .Order()
            .ToArray();

        var expectedMinimum = acceptedMessageCount - errors.Count;
        var unexplainedLoss = Math.Max(0, expectedMinimum - brokerDeliveredCount);
        var duplicateCount = consumedCount - seen.Count;
        var oracleCountMismatch = Math.Abs(brokerDeliveredCount - consumedCount);
        var succeeded = unexplainedLoss == 0
            && missingIds.Count == 0
            && unexpectedIds.Length == 0
            && oracleCountMismatch == 0
            && (!requireZeroDuplicates || duplicateCount == 0);

        return new FaultWindowVerification(
            succeeded,
            unexplainedLoss,
            duplicateCount,
            oracleCountMismatch,
            missingIds,
            duplicateIds.Order().ToArray(),
            unexpectedIds);
    }
}
