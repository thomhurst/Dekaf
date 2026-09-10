# Standard performance tooling

All benchmark fixtures belong in `tools/Dekaf.Benchmarks`, use normal project references
and BenchmarkDotNet's out-of-process toolchain with `[MemoryDiagnoser]`. Do not add
standalone harness projects, custom timing or statistics engines, runtime/JIT samplers,
generated project templates or DLL-rebinding scripts.

Gate fixtures are steady-state: no `[IterationSetup]`/`[IterationCleanup]`,
`InvocationCount` or cold-start/monitoring strategies. Keep a class under about 16 expanded
cases (methods × parameter combinations) so its job finishes in roughly 25 minutes; split
larger sweeps into separate classes or leave them out of the component map and run them by
touching the fixture.

The [performance gate](../workflows/performance-gate.yml) applies identical BenchmarkDotNet
settings to every phase: 10 warmup and 15 measured iterations of 500 ms, one launch,
`DontRemove` outliers, full JSON export. Medians are compared. Verdicts and thresholds are
documented at the top of `.github/scripts/performance_gate.py`.

Hosted runners drift by 10% to 30% between processes on nanosecond cases even for identical
binaries. Read a single delta below the tolerance as noise, never as a win or a loss; local
runs are diagnostic only.

Use the original BenchmarkDotNet JSON and logs from the job artifact when investigating.
For loaded Kafka scenarios use the existing stress project and workflow with its lane,
duration and paid-run limits; do not infer per-message p99, CPU or long-run stability from
BenchmarkDotNet iteration statistics.
