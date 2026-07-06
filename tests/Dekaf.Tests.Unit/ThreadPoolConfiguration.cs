using System.Runtime.CompilerServices;

namespace Dekaf.Tests.Unit;

/// <summary>
/// Pre-sizes the thread pool before any test runs.
/// </summary>
/// <remarks>
/// <para>
/// The unit suite runs hundreds of tests in parallel, and several exercise the library's
/// synchronous (sync-over-async) paths that block a pool thread while an inner async operation
/// completes. With the default minimum worker count (= processor count), the pool only injects
/// additional threads via hill-climbing at roughly one to two per second.
/// </para>
/// <para>
/// During that ramp, continuations for timing-sensitive coordination tests — e.g. the schema
/// registry warmup tests that hand a shared fetch off between waiters via
/// <see cref="TaskCompletionSource"/> continuations — can be starved for several seconds on
/// slower CI runners, intermittently blowing their safety timeouts even though the code under
/// test is correct. (Reproduces locally by constraining the pool; passes with headroom.)
/// </para>
/// <para>
/// Raising the minimum removes the starvation window so those continuations are scheduled
/// promptly regardless of how many sibling tests are momentarily blocking pool threads. This is
/// a test-host concern only; it does not change any behavior of the library under test.
/// </para>
/// </remarks>
internal static class ThreadPoolConfiguration
{
    [ModuleInitializer]
    internal static void EnsureAdequateThreadPool()
    {
        ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
        ThreadPool.SetMinThreads(
            Math.Max(workerThreads, 128),
            Math.Max(completionPortThreads, 128));
    }
}
