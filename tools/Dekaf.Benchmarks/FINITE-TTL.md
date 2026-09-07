# Finite-TTL cache comparison

`SchemaResolutionFiniteTtlBenchmarks<TValue>` measures the completed-cache work that a finite migration-plan TTL triggers. `Refresh` invalidates and resolves one entry in a long-lived, prewarmed cache. `ResolveHit` and `LookupHit` are controls for the unchanged cached paths. Both integer and reference values are covered.

The fixture excludes cache construction, registry I/O, wall-clock expiration checks, and migration-plan construction. It uses a completed resolver task and checks invalidation, synchronous resolution, publication, and entry count in setup. Refresh allocations are per invalidation/resolution, not per cached message. Results must not be presented as whole-deserializer latency or throughput.

## Build exact inputs

Build the schema registry project in two isolated worktrees at the exact product SHAs under comparison. Save each output separately:

```powershell
# Run each command in its corresponding source worktree.
dotnet build src/Dekaf.SchemaRegistry -c Release -f net10.0 -o C:/tmp/dekaf-ttl-before
dotnet build src/Dekaf.SchemaRegistry -c Release -f net10.0 -o C:/tmp/dekaf-ttl-after
```

For #3080 the immediately preceding and last accepted cache implementation is `0ff5e783794c5e9c3b24237b5901425caa4540c0`; the zero-TTL policy addition is `f585252137031e1d8e99d1c578cf10a6069e0c5b`. Compare those first. Their core Dekaf dependency is unchanged. The script isolates the Schema Registry DLL and uses the candidate's adjacent `Dekaf.dll`; do not use it to compare unrelated core changes.

## Validate, then measure

Run from the worktree containing this fixture. Use a quiet host, stop your own builds/tests before measurement, and choose an available physical core. `Affinity` is a logical-processor bitmask; the default `4` selects logical processor 2. It does not reserve that processor.

```powershell
pwsh scripts/Compare-SchemaResolutionFiniteTtl.ps1 -BeforeDll C:/tmp/dekaf-ttl-before/Dekaf.SchemaRegistry.dll -AfterDll C:/tmp/dekaf-ttl-after/Dekaf.SchemaRegistry.dll -Dry
pwsh scripts/Compare-SchemaResolutionFiniteTtl.ps1 -BeforeDll C:/tmp/dekaf-ttl-before/Dekaf.SchemaRegistry.dll -AfterDll C:/tmp/dekaf-ttl-after/Dekaf.SchemaRegistry.dll -Filter '*<Int32>.Refresh*'
```

Omit `-Filter` to run all six fixture cases against all three jobs (18 cases). Dry results validate execution only.

The script creates a unique directory under `.artifacts`, including the exact generated harness, input DLL and fixture SHA-256 hashes, setup/build/run logs, and full BenchmarkDotNet exports. Its nested solution prevents BenchmarkDotNet from accidentally selecting the main benchmark project with the same friend-assembly name. Failed generation or benchmark execution returns a failure instead of treating BenchmarkDotNet's empty report as success.

Measurement uses separate processes with `DOTNET_TieredCompilation=0`, no outlier removal, 20 measured iterations, and `--apples` to use the same baseline-calibrated invocation count for every job. The jobs are before, candidate, and a repeated before control. Inspect the emitted job configuration: BenchmarkDotNet 0.15.8's apples mode overrides the configured eight warmups to one and disables overhead subtraction. These settings differ from the earlier in-process investigation and its absolute timings are not directly comparable.

Check the generated executables' Schema Registry DLL hashes against `inputs.json`. Compare candidate results with both baseline jobs and the unchanged hit controls. Report means, confidence intervals, allocations, drift, and limitations; retain all results. Overlapping intervals, uncontrolled host activity, or a noisy baseline do not establish equivalence. No further identical run should be used to obtain a favorable verdict.

The fixture can also run against the current source in the maintained suite:

```powershell
dotnet run --project tools/Dekaf.Benchmarks -c Release -- --filter '*SchemaResolutionFiniteTtl*' --job Dry
```
