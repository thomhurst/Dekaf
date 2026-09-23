using System.Runtime.CompilerServices;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Enables the accumulator's per-record test hooks for this test assembly. The switch is read
/// into a static readonly field when <see cref="RecordAccumulator"/> is first initialized, so it
/// must be set before any test touches the type; a module initializer runs first.
/// </summary>
internal static class AccumulatorTestHooks
{
    [ModuleInitializer]
    internal static void EnablePerRecordTestHooks()
        => AppContext.SetSwitch(RecordAccumulator.PerRecordTestHooksSwitchName, true);
}
