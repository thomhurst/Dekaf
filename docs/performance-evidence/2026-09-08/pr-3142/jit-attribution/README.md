# Pool benchmark JIT attribution: 2026-09-08

**Performance acceptance remains INCONCLUSIVE.** The immutable candidate host shows background compilation of runtime sampling and BenchmarkDotNet helpers overlapping actual workload iterations. The previous runtime counter increases cannot be dismissed as reporting activity wholly outside measurement. No Dekaf or Reservoir method compiles in the actual-iteration envelope, and no GC or runtime suspension overlaps its individual workload windows.

This experiment identifies compilation methods and timing. It does not capture initiating call stacks, demonstrate zero observer cost, or supply the missing loaded completion latency, client CPU per completed message, failure-path timing, and long-run stability evidence required by #3142. The [original A1/B/A2 verdict](../README.md) is unchanged.

## Pinned inputs and experiment

- [Successful hosted run 34191811875](https://github.com/thomhurst/Dekaf/actions/runs/34191811875), one `ubuntu-latest` VM, sequential fresh processes **U1 / T / U2**: untraced candidate, traced candidate, untraced candidate. All three execute the same candidate product; these are observer controls, not main-versus-candidate acceptance phases.
- Product `4479317a650ea51a2a2ecdc0ccf5a7de8fb51c7d`, original fixture `b26ffdc9070cc68ceaca07bf4193f334df6b2744`, trace harness [`4eea4b281f91e1fd352df51d12b5b11075a5f5f2`](https://github.com/thomhurst/Dekaf/tree/4eea4b281f91e1fd352df51d12b5b11075a5f5f2/.github/benchmarks/pool-jit). Main at dispatch remained `9eec358dad2a081dedbbfc75f02aee743e6bdad9`. The experimental workflow branch is not proposed for merging.
- The workflow downloads original artifact `10041764756`, verifies ZIP SHA-256 `ede8e8e1beb3b6016ff2f779bae516720ab7cca465ff4172e10524e6bb08b3c5`, verifies all 80 original host file bindings, and copies its candidate host unchanged. Neither product nor benchmark is rebuilt. Only the trace inspector is built.
- The three processes log matching loaded assembly hashes: benchmark `57468E237A629DF64B417061E617E28143A1A0038686DF5B0569AD6CF061F981`, Dekaf `FD6FE4549270DA41EF040DF88D5A8293B41A25E6BEC02319A587F0A1C5083CE3`, Reservoir 1.6.7 `A486B9BFE9856FC75D44AA4C47125007DC02BB01FE77CB4AB9D4D37353805800`. See [bindings](loaded-bindings.json).
- Ubuntu 24.04.4, runner image `20260831.293.1`, AMD EPYC 7763, four logical/two physical cores; SDK 10.0.400, runtime 10.0.11. The original A1/B/A2 runner used Intel Xeon Platinum 8573C. The lower absolute means in this run are not evidence of a further product improvement across those different machines.
- BenchmarkDotNet 0.15.8, Release, `InProcessEmitToolchain`, CPU affinity 2, workstation GC, tiered compilation/PGO/ReadyToRun enabled. One rent/reset/return operation, 20 seconds of actual setup workload, 30 one-second workload warmups, 25 one-second actual iterations, `DontRemove`, `[MemoryDiagnoser]`. Every recorded sample, including maxima, is retained.
- `dotnet-trace` 10.0.731102 attaches to the owned benchmark PID during setup. Providers: `Microsoft-Windows-DotNETRuntime:0x10019:5` and `BenchmarkDotNet.EngineEventSource:0xffffffffffffffff:5`; rundown disabled. Inspector uses TraceEvent 3.2.6. The untraced controls use the same host launch path without attachment.
- The [predeclared plan](https://github.com/thomhurst/Dekaf/blob/4eea4b281f91e1fd352df51d12b5b11075a5f5f2/.github/benchmarks/pool-jit/PLAN.md) screens 2% U1/U2 mean drift and 5% traced mean change against either control. These numerical observer screens do not replace the product acceptance requirements. No identical repeat or paid stress lane was dispatched.

## Observer comparison

| Metric | U1 | T | U2 |
| --- | ---: | ---: | ---: |
| BDN mean, ns/op | 23.178611 | 23.379358 | 23.236516 |
| BDN 99.9% CI, ns/op | 23.138667–23.218555 | 23.235527–23.523189 | 23.190842–23.282190 |
| Standard error, ns/op | 0.010665 | 0.038402 | 0.012195 |
| Maximum iteration result, ns/op | 23.310023 | 23.906706 | 23.478009 |
| MemoryDiagnoser, B/op | 0 | 0 | 0 |
| Actual iterations | 25 | 25 | 25 |
| Setup warmup, seconds | 20.000403 | 20.000004 | 20.000426 |
| Setup completed calls | 190,963,101 | 190,415,570 | 190,945,878 |
| BDN workload warmup, seconds | 28.971156 | 30.088487 | 30.058910 |
| BDN warmup completed calls | 1,146,767,040 | 1,193,106,720 | 1,204,862,880 |
| Actual workload clock, seconds | 23.735331 | 24.898409 | 24.992037 |
| Actual completed calls | 955,639,200 | 994,255,600 | 1,004,052,400 |
| JIT count, last warmup to last actual logger sample | 57 | 47 | 43 |
| JIT count, first to last actual logger sample | 47 | 36 | 34 |
| Logger-bracket process CPU, ms | 23,835.416 | 25,014.192 | 25,073.503 |
| Logger-bracket GC count, each generation | 100 | 100 | 100 |
| Maximum sampled thread-pool threads | 0 | 0 | 0 |
| Managed heap at bracket endpoints, bytes | 422,744 / 427,608 | 653,192 / 650,400 | 423,256 / 428,688 |
| RSS at bracket endpoints, bytes | 85,745,664 / 87,678,976 | 89,198,592 / 88,793,088 | 87,293,952 / 88,285,184 |

T versus U1 is **+0.866087%**, T versus U2 **+0.614732%**, and U2 versus U1 drift **+0.249820%**. These meet the predeclared numerical observer screens, but do not prove no observer effect. BDN result means subtract overhead; actual workload seconds and operations above come from the unadjusted workload measurements. Iteration maxima are not individual message latency maxima. Logger CPU/GC brackets include work between iterations and must not be interpreted as client CPU per message or GC inside the benchmark clock. Heap/RSS endpoints from this short diagnostic are not long-run stability evidence. Full precision is in [comparison.json](comparison.json), and every logger sample is retained in the three CSVs.

## What compiles, and where

The traced process yields 87.8712381 seconds, 10,414 parsed events, **zero lost events**, 30 matched warmup windows and 25 matched actual windows. Every engine window matches the corresponding BDN operation count. All JIT-start/method-load pairs and suspension events match; there are no unmatched events in those categories.

The individual engine actual windows total 24.900306639 seconds for 994,255,600 operations; the actual benchmark clock totals 24.898409016 seconds. The engine envelope includes dispatch around `Measure`, so each window exceeds its benchmark clock by 69,000–98,194 ns. These event boundaries are not identical to the clock boundaries.

There are **30 JIT starts inside individual actual engine windows**, and **33 compilation intervals overlap them**: three start in the preceding gaps and complete inside. All 33 execute on background compiler thread 2721; the benchmark thread is 2709. There are 41 matched compilations within the wider first-actual-start to last-actual-stop envelope. [compiles.csv](compiles.csv) retains all 41 with method, signature, tier, thread, timestamps and overlapping iteration numbers.

The methods include `RuntimeLogger.Sample..ctor`, `EngineWarmupStageSpecific.GetShouldRunIteration`, thread-pool/queue count helpers, `Process` and procfs sampling, file and directory enumeration, span/string helpers, `SharedArrayPool<char>.Return`, and `Environment.get_CpuUsage`. Most compile to `OptimizedTier1`; the CSV preserves the exact tier. The `Process`/procfs burst occurs during actual iteration 10, well inside its envelope. This directly establishes concurrent background compilation during workload execution. Method identities correspond to the retained runtime sampler and BDN source; without initiating call stacks, attribution beyond that correspondence remains an inference.

No Dekaf or Reservoir method compiles in the wider envelope. **No GC starts inside an individual actual window, and no runtime suspension overlaps one.** All 96 GCs in the wider envelope occur in the 24 gaps between actual iterations and have reason `Induced`. Decompiled BDN 0.15.8 `Engine.RunIteration` calls `GcCollect` before and after measurement; `ForceGcCollect` performs two collections. The runtime logger's count of 100 includes the edge collections as well.

The source order is iteration setup, forced GC, engine start event, `Measure`, engine stop event, iteration cleanup, forced GC, then measurement formatting and logging. `Measure` starts the clock, invokes the workload delegate, and reads elapsed time. This matters: a local smoke trace captures `WorkloadActualStop` itself compiling after the clock has stopped but before its event is emitted. That wrapper compilation is retained, not relabeled as a product-method compilation or removed from data. The hosted background bursts well inside actual windows cannot be explained by that narrow boundary effect.

## Collection validation and retention

Initial local launch-through-collector validation completed its workload but stalled during export: the SDK `dotnet --version` grandchild inherited a startup diagnostic port and remained suspended. A retained `dotnet-stack` capture identifies the benchmark waiting in `DotNetCliCommandExecutor.GetDotNetSdkVersion`. After verifying ownership and parent PID, only that SDK child was stopped; export then completed. That 303.0825336-second trace and the intervention record remain archived. It is diagnostic collection debugging, not acceptance timing.

The corrected collector starts the benchmark directly, removes inherited diagnostic-port/suspend settings, and attaches by PID. Corrected local smoke completes without intervention (9.052734 seconds, 8,834 events, zero lost), as does hosted smoke (10.5294041 seconds, zero lost). The full hosted U1/T/U2 run has no intervention, failure, timeout, or process restart. Local inspector build-error logs are also retained; the corrected API is `LoaderModuleLoad`.

The [hosted artifact](https://github.com/thomhurst/Dekaf/actions/runs/34191811875/artifacts/10042580737) contains the original benchmark host, trace tool including its hidden store, inspector host and sources, plan/workflow source archive, raw reports, logs and traces. Artifact ZIP: 55,754,523 bytes, SHA-256 `13a884c5388f99023325d955ecc76082ebbca1ed490b8506ec123fea21a2ffaa`, matching GitHub's digest; 181 extracted files. The complete local and hosted archive is also retained outside removable worktrees at:

`C:/git/Dekaf-evidence/pr-3142/4479317a650ea51a2a2ecdc0ccf5a7de8fb51c7d/jit-attribution-34191811875/`

Before this report was published, all **433 files / 379,191,899 bytes** were copied and SHA-256-verified against their originals. `verified-inventory.json` records each file. It includes original local hosts, tools, raw smoke traces and failures, exact harness source, full hosted ZIP/extraction, full parsed events, and all derived analysis. [file-bindings.json](file-bindings.json) binds the report copies to that archive. Original file bytes are preserved.

To reanalyze the extracted hosted artifact, save the two `.py.txt` files as `.py` outside the evidence directory and run:

```text
python -O compare-observer.py EXTRACTED_ARTIFACT comparison-output
python -O analyze-iterations.py EXTRACTED_ARTIFACT/T/events.json EXTRACTED_ARTIFACT/T/bdn attribution-output
```

The observer reanalysis matches both retained output objects exactly under `python -O`; validation uses explicit exceptions. The iteration analyzer also validates the local and hosted smoke captures with `--smoke`. No samples are trimmed. Full parsed events and all compilation intervals, not just the selected CSV, remain in the durable archive.

## Next experiment

The new causal target is harness warmup: exercise the actual runtime logger callback before measurement, retaining separate primer duration/count/time-series evidence, and extend BDN workload warmup so its warmup-stage compilation occurs before actual samples. A primer must prevent callback inlining/elision and reset only its own premeasurement sample storage. Keep the product DLL immutable, preserve all later workload samples, and repeat U1/T/U2 with the same native engine events to test whether compilation still overlaps actual work. This is a proposed fixture intervention, not a product optimization or performance acceptance result.
