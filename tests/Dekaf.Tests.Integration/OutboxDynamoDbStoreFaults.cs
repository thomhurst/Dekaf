using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

namespace Dekaf.Tests.Integration;

/// <summary>
/// A seeded run of the faults a DynamoDB table deals a relay in production: bursts of
/// throttling, and writes that are applied while their answer is lost. Only writes are
/// faulted, which is where a store can be left between two states.
/// </summary>
internal sealed class OutboxDynamoDbStoreFaults(
    int seed, double throttleBurstChance = 0.02, int throttleBurstLength = 4, double lostAnswerChance = 0.03)
{
    private readonly object _gate = new();
    private readonly Random _random = new(seed);
    private int _throttleBurstRemaining;
    private int _throttled;
    private int _throttledDeletes;
    private int _lostAnswers;
    private volatile bool _healed;

    /// <summary>Write requests that were refused unapplied.</summary>
    public int Throttled => Volatile.Read(ref _throttled);

    /// <summary>
    /// Refused deletes of published messages. Each leaves a batch in the table that Kafka
    /// already has, so each is a batch of duplicates: the one fault here that costs any.
    /// </summary>
    public int ThrottledDeletes => Volatile.Read(ref _throttledDeletes);

    /// <summary>Write requests that were applied and then reported as failed.</summary>
    public int LostAnswers => Volatile.Read(ref _lostAnswers);

    /// <summary>The table recovers: no request is faulted from here on.</summary>
    public void Heal() => _healed = true;

    /// <summary>What becomes of one request: the callback of a store's client.</summary>
    public ValueTask<OutboxDynamoDbFault> Decide(AmazonWebServiceRequest request)
    {
        if (_healed || request is not (UpdateItemRequest or PutItemRequest or BatchWriteItemRequest))
            return new(OutboxDynamoDbFault.None);

        lock (_gate)
        {
            // Throttling comes in bursts: a partition that is over its budget refuses
            // whatever reaches it until the budget refills.
            if (_throttleBurstRemaining == 0 && _random.NextDouble() < throttleBurstChance)
                _throttleBurstRemaining = throttleBurstLength;
            if (_throttleBurstRemaining > 0)
            {
                _throttleBurstRemaining--;
                _throttled++;
                if (request is BatchWriteItemRequest)
                    _throttledDeletes++;
                return new(OutboxDynamoDbFault.Throttled);
            }

            if (_random.NextDouble() < lostAnswerChance)
            {
                _lostAnswers++;
                return new(OutboxDynamoDbFault.AppliedThenLost);
            }
        }

        return new(OutboxDynamoDbFault.None);
    }
}
