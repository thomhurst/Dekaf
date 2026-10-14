using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace Dekaf.Tests.Unit;

/// <summary>
/// Busy-wait primitives for tests whose threads spin on each other's progress.
/// </summary>
/// <remarks>
/// Use these instead of <see cref="Thread.SpinWait(int)"/> in wait loops. On .NET 8 that call is an
/// FCall that spins in cooperative mode without polling for a GC or leaving a return address the
/// runtime can hijack, so a loop around it is never stopped for a GC. Once a GC parks the thread
/// that would make progress, the spinner waits for a write that cannot arrive and the GC cannot
/// finish suspending the runtime: every other thread in the test process, timers included, stays
/// blocked until the spinner happens to be caught. (.NET 9+ adds a GC poll after the call.)
/// The loops here make no calls, so the JIT compiles them fully interruptible and a GC can stop
/// them at any instruction.
/// </remarks>
internal static class GcSafeSpin
{
    /// <summary>Spins for <paramref name="iterations"/> processor pause hints.</summary>
    public static void Pause(int iterations = 1)
    {
        for (var i = 0; i < iterations; i++)
        {
            if (X86Base.IsSupported)
                X86Base.Pause();
            else if (ArmBase.IsSupported)
                ArmBase.Yield();
        }
    }

    /// <summary>Spins until <paramref name="location"/> is at least <paramref name="value"/>.</summary>
    public static void UntilAtLeast(ref int location, int value)
    {
        while (Volatile.Read(ref location) < value)
        {
            if (X86Base.IsSupported)
                X86Base.Pause();
            else if (ArmBase.IsSupported)
                ArmBase.Yield();
        }
    }
}
