# Local attribution of the admin BenchmarkDotNet transition

The local `delete:16` diagnostic identifies late BenchmarkDotNet and runtime compilations after 120 seconds of direct workload warmup. Increasing the engine workload warmup from six to fifty iterations does not eliminate them. This is method attribution, not performance acceptance. The full hosted comparison remains **INCONCLUSIVE**, including the unresolved standalone delete probe losses documented in [run 34170245379](34170245379.md).

## Inputs and boundaries

Both fresh processes use product source identical to PR #3128 head `e85fcb90bf1ba746eadb604f6f1347670eb04b98`, checked with `git diff` over `src/`. Their build identity is the harness branch based on `b9a90c9b884441805eb5d38919dbc4b55a3f1de9`; these are not exact-product-commit binaries. The second build adds the optional `ADMIN_EVIDENCE_BDN_WARMUP_COUNT` override. Its default remains six; no product code changes.

Windows 11 25H2, Intel Core i7-12700K (20 logical / 12 physical cores), SDK 10.0.400, runtime 10.0.11, BenchmarkDotNet 0.15.8, Release/net10.0, concurrent workstation GC, tiered compilation and tiered PGO enabled. Each process uses the same `delete:16` fixture, 128 complete 50 ms measurement primers, one complete 1 second primer, 120 seconds of direct workload setup warmup, twelve actual iterations targeting 500 ms, one launch, and `DontRemove`. There is no A/B/A product comparison or affinity control. Trace attachment timings differ and tracing can perturb the results.

`dotnet-trace` 9.0.652701 attaches to the verified worker during setup warmup with `Microsoft-Windows-DotNETRuntime:0x10:5`, no rundown, and a 240-second maximum. Both workers exit before that limit. TraceEvent 3.2.6 parses method JIT starts and loads, preserving raw QPC counters and process IDs. The analysis checks the 10 MHz counter scale against relative trace timestamps, worker identity, zero lost events, and trace coverage of the entire host `BeforeActualRun` / `AfterActualRun` interval. Method loads are matched to the preceding JIT start by method ID and thread ID to identify optimization tiers. Event spans are not process CPU measurements.

| Observation | Six engine warmups | Fifty engine warmups |
| --- | ---: | ---: |
| Worker PID | 3580 | 96096 |
| Direct setup warmup, seconds | 120.0000019 | 120.0000032 |
| Direct setup completed calls | 30,714,001 | 30,808,990 |
| Engine workload warmup, actual seconds | 2.8907818 | 25.4166208 |
| Engine workload warmup completed calls | 775,200 | 6,759,200 |
| Actual iterations retained | 12 | 12 |
| Actual workload completed calls | 1,550,400 | 1,622,208 |
| Actual workload timer total, seconds | 5.7525411 | 6.1391240 |
| Conservative host actual phase, seconds | 5.7909182 | 6.1724276 |
| JIT starts inside host actual phase | 24 | 9 |
| QuickJitted / OptimizedTier1 / OptimizedTier1Instrumented | 2 / 21 / 1 | 2 / 7 / 0 |
| Trace duration, seconds | 55.397 | 150.134 |
| Lost events | 0 | 0 |

The two `QuickJitted` methods in both processes are `BenchmarkDotNet.Engines.EngineActualStageSpecific.GetMeasurementList` and `GetShouldRunIteration`, about 17 ms after the host boundary. Decompilation of the retained BenchmarkDotNet assembly confirms that `Engine.Run` calls them after `BeforeMainRun`, before its first `RunIteration`. They are outside the individual workload timer. This explains part of the conservative phase count; it does not justify deleting any actual samples or ignoring concurrent compilation later in the phase.

With six warmups, the remaining starts are tier promotions of runtime formatting/collection helpers and BenchmarkDotNet measurement accessors, mostly about 4.29–4.31 seconds into the phase. They include `Measurement.get_Nanoseconds`, `Measurement.get_Operations`, `System.Double.ToString`, `System.Number.FormatFloat`, `System.Enum` helpers, `StringBuilder` helpers, and `Perfolizer.Horology.Frequency.get_Hertz`. `System.SpanHelpers.ReplaceValueType` is promoted to `OptimizedTier1Instrumented`. The retained `Measurement.ToString` decompilation uses formatting and measurement accessors, but method names alone do not establish the caller for every runtime helper.

With fifty warmups, the remaining seven starts include two generated BenchmarkDotNet constructor lambdas at about 1.02 seconds, followed by `System.Number.UInt32ToDecStr`, two `System.Number.Dragon4` overloads, `System.Number+BigInteger.Multiply`, and `System.Number.UInt64ToDecStr` at about 5.14–5.65 seconds. Neither trace records a Dekaf-named JIT start inside the phase. This does not prove that product code cannot trigger a shared runtime helper, that tracing captures equivalent untraced timing, or that every hosted workload has the same attribution.

## Decision and validation

The longer engine warmup changes the observed method set but fails the hypothesis that it removes the measured transition. Do not dispatch another full admin acceptance campaign merely with this warmup change. A subsequent experiment needs explicit method/iteration attribution on Ubuntu and a justified strategy for engine setup and late tier promotions, validated across all relevant cases before a new same-VM A/B/A campaign. Standalone probe CPU, throughput, latency and control-drift findings remain independent unresolved evidence; this diagnostic does not waive them.

Both diagnostic runs and trace parses finish successfully. The inspector and harness build in Release with repository analyzers; initial diagnostic build failures and corrected build logs are retained. Counter scale, worker identity and phase coverage assertions pass for both traces. All twelve actual samples, raw warmup/runtime series, traces, parsed events, generated workers and loaded binaries are retained. No unit or integration tests are needed for these optional diagnostic-tool fields and configuration; product code is unchanged. The final diff was reviewed for reuse, clarity and scope.

The durable local archive is `C:/git/Dekaf-evidence/pr-3128/bdn-jit-attribution-20260908/`. It contains the first build's binaries captured before the follow-up build, the final build and generated workers, the Git base source archive plus exact modified tool sources, trace commands/worker identities, predeclared plans, raw results, decompilations, correlation scripts, and file manifests verified against their originals. This archive is outside removable worktrees. It is local evidence, not a GitHub Actions artifact or an acceptance run.
