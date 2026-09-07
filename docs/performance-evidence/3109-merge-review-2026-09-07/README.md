Merge review for exact head `7316983bc4485ac1db34dc39c9cbdee38a56adcc`:

The latest Claude review's missing `[InvocationCount(1)]` finding is a false positive. This repository pins BenchmarkDotNet 0.15.8. Its [MakeSettingsUserFriendly implementation](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Jobs/JobExtensions.cs#L365) calls `RunOncePerIteration()` when iteration setup/cleanup exists and neither invocation count nor unroll factor is explicitly supplied. This matches the [official setup/cleanup documentation](https://benchmarkdotnet.org/articles/features/setup-and-cleanup.html).

Verified the unchanged committed benchmark through the built repository runner with no job or invocation overrides:

```text
dotnet tools/Dekaf.Benchmarks/bin/Release/net10.0/Dekaf.Benchmarks.dll --filter *PartitionedShutdownBenchmarks* --artifacts <output>
PartitionedShutdownBenchmarks.DrainFullQueue: Job-CNUJVU(InvocationCount=1, UnrollFactor=1)
WorkloadActual 1: 1 op
```

The benchmark completed successfully, including its cleanup assertions. This local run verifies benchmark execution semantics only; its timing is not acceptance evidence. An explicit invocation attribute would be redundant for the documented invocation. No product or benchmark change is needed for this finding.

The older control-command cancellation finding is already fixed: current `DrainCommandsAsync` checks input cancellation immediately after the awaited cooperative commit, before any subsequent command. Regression tests cover the queued-assignment race. The multi-lane deadline and test-cleanup findings were previously addressed and resolved. No new product correctness finding or changes-requested review exists.

The maintainer has now instructed this session to merge provided there is no additional actionable review feedback, after being shown the CPU investigation and remaining INCONCLUSIVE verdict. This is recorded as the maintainer's merge exception for the known unresolved performance uncertainty, including the older performance-review threads; it is not a measured PASS or a claim that those earlier measurements never existed. The `agent/performance-gate` result and all raw evidence remain unchanged. Required CI must complete successfully before merging.
