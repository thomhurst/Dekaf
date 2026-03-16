using System.Runtime;
using Microsoft.Extensions.Logging;

namespace Dekaf.Internal;

/// <summary>
/// One-time diagnostic check for GC configuration. Warns when Workstation GC is used
/// in a high-throughput Kafka client, since Server GC provides dramatically better
/// throughput stability on multi-core machines.
/// </summary>
internal static partial class GcConfigurationCheck
{
    private static int s_warned;

    internal static void WarnIfWorkstationGc(ILogger logger)
    {
        if (GCSettings.IsServerGC)
            return;

        // Log once per process to avoid spamming when many clients are created.
        if (Interlocked.CompareExchange(ref s_warned, 1, 0) != 0)
            return;

        LogWorkstationGcDetected(logger);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message =
        "Workstation GC detected. High-throughput Kafka workloads perform significantly better with Server GC. " +
        "Add <ServerGarbageCollection>true</ServerGarbageCollection> to your project file to enable it.")]
    private static partial void LogWorkstationGcDetected(ILogger logger);
}
