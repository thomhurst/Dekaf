#if NET8_0
using System.Runtime.Loader;

namespace Dekaf.Tests.Unit.Compatibility;

public sealed class NetStandardPeriodicTimerTests
{
    [Test]
    public async Task Dispose_IsIdempotentAndCompletesPendingWaitWithFalse()
    {
        var loadContext = new AssemblyLoadContext("netstandard-periodic-timer", isCollectible: true);
        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory,
                "compatibility", "netstandard2.0", "Dekaf.dll"));
            var timerType = assembly.GetType(
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
        finally
        {
            loadContext.Unload();
        }
    }
}
#endif
