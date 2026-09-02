using System.Runtime.CompilerServices;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit;

/// <summary>
/// Enables diagnostics-only pool counters before any test runs.
/// </summary>
/// <remarks>
/// <see cref="ValueTaskSourcePool{T}.ApproximateCount"/> is maintained only when the
/// <see cref="ValueTaskSourcePool.TrackRetainedCountSwitchName"/> AppContext switch is on; the
/// library reads the switch once into a static readonly field so the awaited produce path pays
/// nothing for it in production. The pool tests assert on that count to observe rent/return
/// behavior, so the switch must be set before the pool type initializes.
/// </remarks>
internal static class PoolDiagnosticsConfiguration
{
    [ModuleInitializer]
    internal static void EnablePoolCountTracking() =>
        AppContext.SetSwitch(ValueTaskSourcePool.TrackRetainedCountSwitchName, true);
}
