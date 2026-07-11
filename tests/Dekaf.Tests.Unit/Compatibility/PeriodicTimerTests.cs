#if NET8_0
namespace Dekaf.Tests.Unit.Compatibility;

public sealed class NetStandardPeriodicTimerTests
{
    [Test]
    public async Task Dispose_IsIdempotentAndCompletesPendingWaitWithFalse()
    {
        var timerType = typeof(Kafka).Assembly.GetType(
            "System.Threading.PeriodicTimer",
            throwOnError: true)!;
        using var timer = (IDisposable)Activator.CreateInstance(
            timerType,
            TimeSpan.FromMinutes(1))!;
        var waitForNextTick = timerType.GetMethod(
            "WaitForNextTickAsync",
            [typeof(CancellationToken)])!;
        var pendingTick = ((ValueTask<bool>)waitForNextTick.Invoke(
            timer,
            [CancellationToken.None])!).AsTask();

        timer.Dispose();
        timer.Dispose();

        await Assert.That(await pendingTick).IsFalse();
    }
}
#endif
