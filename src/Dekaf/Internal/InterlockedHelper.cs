using System.Runtime.CompilerServices;

namespace Dekaf.Internal;

/// <summary>
/// Lock-free helper methods extending <see cref="Interlocked"/>.
/// </summary>
internal static class InterlockedHelper
{
    /// <summary>
    /// Atomically increases <paramref name="location"/> to at least <paramref name="newValue"/>.
    /// If the current value is already greater than or equal to <paramref name="newValue"/>, this is a no-op.
    /// Uses a CAS loop — safe for concurrent callers, never decreases.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RatchetUp(ref int location, int newValue)
    {
        int current;
        do
        {
            current = Volatile.Read(ref location);
            if (newValue <= current) return;
        }
        while (Interlocked.CompareExchange(ref location, newValue, current) != current);
    }
}
