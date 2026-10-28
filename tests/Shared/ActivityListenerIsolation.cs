using System.Diagnostics;
using System.Reflection;

namespace Dekaf.Tests;

/// <summary>
/// Temporarily detaches TUnit's HTML trace collector, which otherwise requests at least
/// PropagationData from every source and prevents testing ActivitySamplingResult.None.
/// Only use in [NotInParallel] tests. Restore the same listener to preserve report collection.
/// </summary>
internal sealed class ActivityListenerIsolation : IDisposable
{
    private readonly ActivityListener? _reportListener;

    public ActivityListenerIsolation()
    {
        // TUnit 1.76.28 exposes no public per-source opt-out. Keep the version-sensitive
        // lookup explicit so a framework change fails these tests instead of hiding sampling bugs.
        var collectorType = Type.GetType("TUnit.Engine.Reporters.Html.ActivityCollector, TUnit.Engine", throwOnError: true)!;
        var collector = collectorType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static)!.GetValue(null);
        if (collector is not null)
        {
            _reportListener = (ActivityListener?)collectorType
                .GetField("_listener", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(collector);
            _reportListener?.Dispose();
        }
    }

    public void Dispose()
    {
        if (_reportListener is not null)
            ActivitySource.AddActivityListener(_reportListener);
    }
}
