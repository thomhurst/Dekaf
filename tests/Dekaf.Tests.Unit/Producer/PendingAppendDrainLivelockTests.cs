using Dekaf.Producer;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Regression tests for the pending-append drain livelock (#2207): the drain owner's own
/// failed reserve released its admission slot, which re-requested a drain from inside the
/// scan, making drainRequested true on every pass. With one unadmittable queued append the
/// owner spun at millions of passes; when the BrokerSender send loop owned the spin, the
/// producer wedged until max.block.ms (release-gate run 29612262949). Pre-fix, this test
/// hangs in DrainPendingAppends; post-fix the owner exits after a bounded number of passes.
/// </summary>
public class PendingAppendDrainLivelockTests
{
    private const string Topic = "drain-livelock-topic";

    [Test]
    [Timeout(30_000)]
    public async Task DrainPendingAppends_UnadmittableQueuedAppend_ExitsWithoutSpinning(
        CancellationToken cancellationToken)
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-producer",
            BufferMemory = 4096, // Tiny buffer so a queued append cannot be admitted
            BatchSize = 1000,
            LingerMs = 60_000 // Keep the batch open — nothing drains memory during the test
        };
        await using var accumulator = new RecordAccumulator(
            options,
            resolveLeaderId: static (_, _) => 1);

        // Fill the buffer until an append queues as a PendingAppend (pending ValueTask).
        ValueTask<bool> blockedAppend = default;
        var blocked = false;
        for (var i = 0; i < 200 && !blocked; i++)
        {
            var append = AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic);
            if (append.IsCompleted)
            {
                await Assert.That(await append).IsTrue();
            }
            else
            {
                blockedAppend = append;
                blocked = true;
            }
        }

        await Assert.That(blocked).IsTrue();

        // Pre-fix: the drain owner's own reserve failures re-request the drain and it
        // never exits (test times out here). Post-fix: no progress + no external request
        // ends the scan after very few passes.
        var passesBefore = accumulator.PendingAppendDrainPassCountForTest;
        _ = accumulator.DrainPendingAppends();
        var passes = accumulator.PendingAppendDrainPassCountForTest - passesBefore;

        await Assert.That(passes).IsGreaterThanOrEqualTo(1);
        await Assert.That(passes).IsLessThanOrEqualTo(4);

        // Dispose fails the queued append; observe its exception so nothing goes unobserved.
        await accumulator.DisposeAsync();
        try
        {
            _ = await blockedAppend;
        }
        catch (Exception)
        {
            // Expected: dispose fails the queued operation.
        }
    }
}
