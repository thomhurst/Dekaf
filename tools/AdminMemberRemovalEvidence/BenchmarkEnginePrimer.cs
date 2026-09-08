using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Dekaf.Benchmarks;

// Warm BDN 0.15.8 engine callbacks and reporting outside measured work. The
// product workload still needs its own elapsed warmup and runtime assessment.
internal static class BenchmarkEnginePrimer
{
    internal readonly record struct Result(double Seconds, long CallbackPairs, long FormattedMeasurements);

    internal static Result Warm(AdminEvidenceBenchmark benchmark)
    {
        Prepare();
        var setup = GetEmptyCallback(benchmark, "iterationSetupAction");
        var cleanup = GetEmptyCallback(benchmark, "iterationCleanupAction");
        var timer = Stopwatch.StartNew();
        long iterations = 0;
        do
        {
            InvokeEmptyCallbacks(setup, cleanup);
            var measurement = new Measurement(0, IterationMode.Workload, IterationStage.Actual,
                (int)(iterations % 1000) + 1, 100_000 + (iterations & 0xffff),
                500_000_000.125 + (iterations & 0x3ff));
            GC.KeepAlive(measurement.ToString());
            iterations++;
        } while (timer.Elapsed.TotalSeconds < 10);
        return new Result(timer.Elapsed.TotalSeconds, iterations, iterations);
    }

    // Empty targets must execute while priming; OSR/PGO must not inline them away.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void InvokeEmptyCallbacks(Action setup, Action cleanup)
    {
        setup();
        cleanup();
    }

    private static Action GetEmptyCallback(AdminEvidenceBenchmark benchmark, string name)
    {
        var field = benchmark.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Generated BDN callback is missing: {name}");
        var callback = field.GetValue(benchmark) as Action
            ?? throw new InvalidOperationException($"Generated BDN callback has an unexpected type: {name}");
        var body = callback.Method.GetMethodBody()?.GetILAsByteArray();
        if (body is not { Length: 1 } || body[0] != 0x2a)
            throw new InvalidOperationException($"Only an empty generated BDN callback may be primed: {name}");
        return callback;
    }

    private static void Prepare()
    {
        var type = typeof(BenchmarkSwitcher).Assembly.GetType(
            "BenchmarkDotNet.Engines.EngineActualStageSpecific", throwOnError: true)!;
        foreach (var name in new[] { "GetMeasurementList", "GetShouldRunIteration" })
        {
            var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                ?? throw new InvalidOperationException($"BDN engine method is missing: {name}");
            RuntimeHelpers.PrepareMethod(method.MethodHandle);
        }
    }
}
