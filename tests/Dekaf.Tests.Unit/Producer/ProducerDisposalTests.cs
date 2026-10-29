using System.Reflection;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Producer;

public class ProducerDisposalTests
{
    [Test]
    [Arguments(0)]
    [Arguments(1_000)]
    public async Task DisposeWithBudgetAsync_ExhaustedStepBudget_OnlyBoundsConfiguredShutdown(int closeTimeoutMs)
    {
        await using var producer = new KafkaProducer<string, string>(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            CloseTimeoutMs = closeTimeoutMs
        }, Serializers.String, Serializers.String);
        var cleanupGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var method = producer.GetType().GetMethod(
            "DisposeWithBudgetAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        // An exhausted step budget expires synchronously. No sleeps or timer races are needed
        // to prove that CloseTimeoutMs = 0 bypasses it, including on the .NET 8 runtime.
        var cleanup = ((ValueTask)method.Invoke(producer,
            [new ValueTask(cleanupGate.Task), 0, "test cleanup"])!).AsTask();
        try
        {
            await Assert.That(cleanup.IsCompleted).IsEqualTo(closeTimeoutMs > 0);
        }
        finally
        {
            cleanupGate.TrySetResult();
            await cleanup.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Test]
    public async Task DisposeAsync_NoTimeout_WaitsForLingerCompletion()
    {
        var producer = new KafkaProducer<string, string>(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            CloseTimeoutMs = 0
        }, Serializers.String, Serializers.String);
        await producer.StopSenderLoopsForTestingAsync();
        var lingerGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        AccumulatorTestHelpers.SetPrivateField(producer, "_lingerTask", lingerGate.Task);

        var disposal = producer.DisposeAsync().AsTask();
        try
        {
            // The previous linger deadline was capped at one second. Keep the gate
            // closed beyond that boundary so a timed wait cannot satisfy this test.
            await Assert.That(async () => await disposal.WaitAsync(TimeSpan.FromSeconds(2)))
                .Throws<TimeoutException>();
        }
        finally
        {
            lingerGate.TrySetResult();
            await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
