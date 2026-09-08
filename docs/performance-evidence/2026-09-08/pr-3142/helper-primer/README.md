# Pool exact-helper primer: 2026-09-08

**Full acceptance: INCONCLUSIVE.** The traced phase meets the predeclared no-compilation/no-suspension-overlap criterion across all 25 actual windows. The numerical observer screens also pass. This supports the targeted helper primer, but U1 retains two unattributed JIT count increments, and required loaded metrics remain missing. No product performance PASS or protected-metric tradeoff is approved.

## Identity and intervention

[Run 34198015349](https://github.com/thomhurst/Dekaf/actions/runs/34198015349) executes fresh candidate-only U1 / traced T / U2 sequentially on one `ubuntu-latest` VM. Product remains `4479317a650ea51a2a2ecdc0ccf5a7de8fb51c7d`; main at dispatch remains `9eec358dad2a081dedbbfc75f02aee743e6bdad9`. Exact harness and [predeclared plan](https://github.com/thomhurst/Dekaf/blob/4c9b8c7543847cefa94cdceb69ded0f730e4d617/.github/benchmarks/pool-primer/HELPER-PLAN.md): `4c9b8c7543847cefa94cdceb69ded0f730e4d617`. The experimental workflow branch is not proposed for merging.

The [preceding sampler-primer experiment](../sampler-primer/README.md) retains two overlapping compilations. The intervention adds a separate 20-second helper primer after the existing 20-second actual logger primer. It exercises BDN `Measurement.ToString`, plus exactly bound `System.Number.UInt64ToDecStr`, `PortableThreadPool.ThreadCount`, `ThreadCounts.VolatileRead`, and `ThreadCounts.NumExistingThreads`. A primer-only `NoInlining | NoOptimization` dispatcher invokes cached delegates and reflection. Value-type helpers operate on an owned zero value, with assertions; the live thread count is read only. JIT settings and product code do not change.

All phases then execute the unchanged 20-second pool setup, 50 one-second BDN workload warmups and 25 actual iterations. Actual elapsed warmup and completed counts are verified. Release/net10.0, BDN 0.15.8, `InProcessEmitToolchain`, workstation GC, affinity logical CPU 2, tiered compilation/PGO/ReadyToRun enabled; SDK 10.0.400/runtime 10.0.11. Runner: AMD EPYC 7763, four logical/two physical cores, Ubuntu 24.04.4, image `20260831.293.1`.

The original artifact digest and all 80 original host bindings are verified before rebuilding the common fixture. Only benchmark DLL/PDB differ among 40 retained candidate host files; product, Reservoir 1.6.7 and host configuration remain byte-identical. All nine loaded assembly bindings match. Helper bindings are identical across phases. Actual loaded Linux CoreLib is archived, SHA-256 `7f905cec6777e546c9f334f23f7552ccaaaa1ac382ae965fbe449ea215df10ed`, MVID `6cd43ac9-c056-406e-aa72-2a19c8286e4b`.

Local and hosted attached smoke runs complete before the full experiment. T attaches `dotnet-trace` 10.0.731102 by PID, CLR `0x10019:5` plus BDN engine events, no rundown. All processes exit successfully; no timeout, intervention or repeat occurs.

## Absolute results

| Metric | U1 | T | U2 |
| --- | ---: | ---: | ---: |
| BDN mean, ns/op | 23.309957 | 22.981674 | 23.147835 |
| Standard error, ns/op | 0.038002 | 0.015233 | 0.022367 |
| Maximum iteration result, ns/op | 23.648734 | 23.303493 | 23.445313 |
| MemoryDiagnoser, B/op | 0 | 0 | 0 |
| Pool setup, seconds | 20.000315 | 20.000003 | 20.000311 |
| Pool setup completed calls | 190,413,297 | 190,750,409 | 189,899,135 |
| BDN workload warmup, seconds | 49.970167 | 49.839312 | 49.964557 |
| BDN warmup completed calls | 2,009,069,600 | 2,021,485,600 | 2,015,755,200 |
| Actual clock, seconds | 25.078247 | 24.903716 | 24.997745 |
| Actual completed calls | 1,004,534,800 | 1,010,742,800 | 1,007,877,600 |
| JIT count, last warmup to last actual logger sample | 7 | 4 | 5 |
| JIT count, first to last actual logger sample | 2 | 0 | 0 |
| Logger-bracket process CPU, ms | 25,131.018000 | 24,972.834000 | 25,047.311000 |
| BDN 99.9% CI, ns/op | 23.167624–23.452291 | 22.924620–23.038727 | 23.064060–23.231609 |
| Logger primer, seconds | 20.00005 | 20.000331 | 20.000323 |
| Logger primer completed callbacks | 132690 | 123879 | 136251 |
| Helper primer, seconds | 20.0002342 | 20.0002241 | 20.0002496 |
| Helper primer completed calls | 21482606 | 20939951 | 20584311 |
| Heap bracket endpoints, bytes | 566872 / 575536 | 762392 / 766872 | 526712 / 535824 |
| RSS bracket endpoints, bytes | 92454912 / 91590656 | 94613504 / 93470720 | 88473600 / 87531520 |

All phases retain 25 actual results and 50 workload warmups, with `DontRemove` and `[MemoryDiagnoser]`. T versus U1 is **-1.408341%**, T versus U2 **-0.717825%**, and U2/U1 control drift **-0.695508%**. These satisfy the unchanged 5% traced-change and 2% control-drift screens. A faster traced mean does not establish negative or zero observer cost.

Every phase has 20 logger-primer samples and 20 helper-primer samples. Cumulative process allocations at the last helper sample are 28,026,298,224 / 27,365,198,976 / 26,945,561,456 bytes. This allocation-heavy, premeasurement fixture activity is separate from the measured pool's **0 B/op**. Per-second CPU, JIT, GC, heap, RSS, thread count and completion series are retained. Actual logger brackets each show 100 collections per generation, zero sampled thread-pool threads and zero pending work. Bracket CPU includes reporting/cleanup gaps; it is not client CPU per completed message. BDN iteration percentiles/maxima are not per-message latency, and short heap/RSS endpoints do not establish sustained stability.

## Trace and remaining uncertainty

T retains **145.5552976 seconds, 17,463 parsed events and zero lost events**. All 50 warmup and 25 actual start/stop pairs match BDN operation counts. No JIT-start/method-load pair or suspension event is unmatched. The actual engine windows total 24.905621790 seconds versus 24.903715842 on the benchmark clock; per-iteration wrapper excess is 71,814–88,636 ns.

There are **zero JIT starts, zero overlapping compilation intervals, and zero compilations anywhere in the wider first-actual-start to last-actual-stop envelope**. No GC starts inside an actual window and no suspension overlaps one. The 96 induced GCs in the wider envelope occur in the 24 inter-iteration gaps; the logger's 100 collections include edge collections. All events and maxima remain retained.

All four targeted methods load `OptimizedTier1` code early, at trace-relative 20,466.037 / 20,467.634 / 20,470.557 / 20,500.374 ms for `VolatileRead`, `NumExistingThreads`, `UInt64ToDecStr` and `ThreadCount`, respectively. [helper-related-compilations.json](helper-related-compilations.json) also retains related reflection stubs and formatting helper compilations. These method identities and timing support the primer intervention; initiating caller stacks were not collected.

**U1 is not proven free of measured startup activity.** Its logger records one additional compiled method at actual sample 3 and one at sample 5, with compilation-time increments 1.5858 and 1.5530 ms. U1 has no trace, so these cannot be assigned to the internal clock or reporting gaps. U2 and T have zero first-to-last-actual logger count increments. All first-actual boundary increments (5 / 4 / 5) remain recorded. The clean T trace cannot erase U1 uncertainty.

The preceding experiment had two overlapping intervals and four compilations in the wider actual envelope; this experiment has zero and zero. Separate runner VMs prevent interpreting cross-run mean differences as a controlled timing improvement. Original fresh-main A1/B/A2 means remain 48.854649 / 44.213577 / 48.732145 ns/op on an Intel runner, with the original INCONCLUSIVE verdict unchanged.

## Retention and replay

[Artifact 10044928179](https://github.com/thomhurst/Dekaf/actions/runs/34198015349/artifacts/10044928179): 90,472,089 bytes, ZIP SHA-256 `bf9c0fa6ea515b4dfaa7e7aaaf6bcec55628543bbae523c280d5cb545bc0d65e`, matching GitHub's digest; 283 extracted files. It includes original/revised executable hosts, the exact harness archive, source/plan/workflow, tools/inspector, actual CoreLib, smoke/full traces, all BDN reports, process statuses and time series.

Before publication, the hosted artifact and analysis are archived and SHA-256-verified: **303 files / 336,959,530 bytes**, under:

`C:/git/Dekaf-evidence/pr-3142/4479317a650ea51a2a2ecdc0ccf5a7de8fb51c7d/helper-primer-34198015349/hosted/`

Sibling `local/` retains 113 verified files / 129,606,686 bytes including local original-host comparisons, source ZIP, builds and attached smoke. `analysis-validation/` retains 15 supplemental files. Each inventory binds copies to originals outside removable worktrees. Publication files are supplemental; [file-bindings.json](file-bindings.json) verifies these report copies byte-for-byte.

Save the four `.py.txt` analyzers as `.py`, extract the artifact and choose new output directories:

```text
python -O compare-observer.py EXTRACTED_ARTIFACT comparison-output
python -O analyze-iterations.py EXTRACTED_ARTIFACT/T/events.json EXTRACTED_ARTIFACT/T/bdn attribution-output
python -O summarize-primer.py EXTRACTED_ARTIFACT attribution-output/attribution.json primer-output
python -O summarize-helpers.py EXTRACTED_ARTIFACT helper-output
```

Validation remains active under `python -O`. The helper validator rejects inconsistent completion counts and existing output directories without modifying retained results. Full precision, all 75 actual and 150 warmup samples, all primer samples, maxima and trace events are retained.

Next work must resolve the untraced control's JIT uncertainty and use the finalized common fixture in a fresh-main, same-VM Ubuntu A1/B/A2. Changed cleanup-path timing, loaded completed producer/consumer throughput, actual per-message p50/p99/max latency, process CPU/allocation scope and sustained stability remain required. PR #3143 repairs the aggregate stress comparator separately; it does not supply these measurements or change runner requirements. No paid stress dispatch or identical repeat follows automatically.
