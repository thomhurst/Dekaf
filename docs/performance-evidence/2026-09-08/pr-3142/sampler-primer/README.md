# Pool sampler primer: 2026-09-08

**INCONCLUSIVE.** The combined logger primer and longer BDN warmup reduce observed compilation overlap, but do not meet the predeclared no-overlap criterion. Two background compilations overlap actual engine windows. One provably extends into the benchmark clock; the other falls within boundary uncertainty. All U1/T/U2 phases report 0 B/op, and the numerical observer screens pass. No product performance acceptance or protected-metric tradeoff is approved.

## Experiment and identity

[Hosted run 34194281968](https://github.com/thomhurst/Dekaf/actions/runs/34194281968) completes successfully on one `ubuntu-latest` VM. Fresh candidate-only processes run sequentially: untraced U1, traced T, untraced U2. Product remains `4479317a650ea51a2a2ecdc0ccf5a7de8fb51c7d`; main remains `9eec358dad2a081dedbbfc75f02aee743e6bdad9`. Harness and [predeclared plan](https://github.com/thomhurst/Dekaf/blob/3e4ba83fbc76b75f143ca4dca28a2b4c0fa48db5/.github/benchmarks/pool-primer/PLAN.md): `3e4ba83fbc76b75f143ca4dca28a2b4c0fa48db5`. The experimental branch is not proposed for merging.

The [preceding immutable-host trace](../jit-attribution/README.md) identifies 33 overlapping background compilations, largely runtime sampler helpers. This intervention exercises the **actual `RuntimeLogger.WriteLine` callback for at least 20 seconds**, through a primer-only `NoInlining | NoOptimization` helper, before the unchanged 20-second pool workload setup. Each triplet covers `WorkloadWarmup`, `WorkloadActual` and `WorkloadJitting`, verifies three stored samples, and resets only premeasurement storage. Separate per-second primer series retain elapsed time, completed callbacks, JIT, thread-pool, CPU, allocation, GC, heap and RSS. No primer sample enters the later runtime CSV, and no later workload sample is cleared.

BDN workload warmup increases from 30 to **50 one-second iterations**, followed by the same 25 one-second actual iterations, `DontRemove` and `[MemoryDiagnoser]`. Every phase has the same revised fixture. BenchmarkDotNet 0.15.8, `InProcessEmitToolchain`, Release/net10.0, affinity logical CPU 2, workstation GC, tiered compilation/PGO/ReadyToRun enabled; SDK 10.0.400/runtime 10.0.11. Host: Ubuntu 24.04.4, image `20260831.293.1`, AMD EPYC 7763, four logical/two physical cores.

The original candidate host is downloaded from artifact `10041764756` with verified original ZIP digest and all 80 original host bindings. Only the benchmark DLL/PDB are rebuilt and replaced. All **40 candidate host files** are verified against the retained original host; all other product/dependency/runtime-configuration files are byte-identical. The loaded benchmark hash is `CBE0A7876914116D1FED500D3339DD74698223784090EB89B259CE10D73E1D54`. Dekaf remains `FD6FE4549270DA41EF040DF88D5A8293B41A25E6BEC02319A587F0A1C5083CE3`, Reservoir 1.6.7 remains `A486B9BFE9856FC75D44AA4C47125007DC02BB01FE77CB4AB9D4D37353805800`; all nine loaded-assembly bindings match the retained host.

T uses PID-attached `dotnet-trace` 10.0.731102, CLR provider `0x10019:5`, native BDN engine events and no rundown, with the same inspector as the preceding run. A local attached smoke and hosted attached smoke complete without intervention before the full run. All hosted phases exit successfully; no timeout, restart, or identical repeat occurs. The predeclared process ceiling is 240 seconds; the job ceiling is 20 minutes.

## Absolute observer results

| Metric | U1 | T | U2 |
| --- | ---: | ---: | ---: |
| BDN mean, ns/op | 22.856147 | 23.290315 | 23.124594 |
| BDN 99.9% CI, ns/op | 22.805147–22.907147 | 23.262467–23.318163 | 23.001869–23.247319 |
| Standard error, ns/op | 0.013617 | 0.007435 | 0.032767 |
| Maximum iteration result, ns/op | 22.998674 | 23.362461 | 23.354348 |
| MemoryDiagnoser, B/op | 0 | 0 | 0 |
| Actual iterations | 25 | 25 | 25 |
| Logger primer, seconds | 20.000049 | 20.000251 | 20.000269 |
| Primer completed callbacks | 133,623 | 122,724 | 133,263 |
| Separate primer samples | 20 | 20 | 20 |
| Pool setup warmup, seconds | 20.000002 | 20.000003 | 20.000313 |
| Setup completed calls | 190,484,498 | 190,156,710 | 190,715,466 |
| BDN workload warmup, seconds | 49.898653 | 49.869909 | 50.188781 |
| BDN warmup completed calls | 2,037,204,000 | 1,964,964,800 | 2,036,309,600 |
| Actual workload clock, seconds | 24.968486 | 24.513312 | 25.230395 |
| Actual completed calls | 1,018,602,000 | 982,482,400 | 1,018,154,800 |
| JIT count, last warmup to last actual logger sample | 10 | 8 | 9 |
| JIT count, first to last actual logger sample | 5 | 4 | 4 |
| Logger-bracket process CPU, ms | 25,015.379 | 24,578.339 | 25,277.654 |
| Logger-bracket GC count, each generation | 100 | 100 | 100 |
| Maximum sampled thread-pool threads | 0 | 0 | 0 |
| Managed heap at bracket endpoints, bytes | 426,536 / 422,784 | 650,568 / 654,912 | 428,864 / 433,144 |
| RSS at bracket endpoints, bytes | 88,870,912 / 87,822,336 | 91,881,472 / 92,807,168 | 91,488,256 / 90,443,776 |

T versus U1 is **+1.899569%**, T versus U2 **+0.716644%**, and U2 versus U1 drift **+1.174508%**. These meet the predeclared 5% traced-change and 2% control-drift numerical screens. They do not prove zero observer cost. Full precision and all samples are retained in the JSON/CSV files.

The actual clock/operation totals are unadjusted workload measurements; BDN result means subtract overhead. Iteration maxima are not per-message latency maxima. Logger CPU and GC brackets include reporting and cleanup gaps and are not client CPU per completed message or GC inside the clock. This short run does not establish long-run stability. The primer itself performs allocation-heavy `Process` sampling: cumulative process allocations at its last sample are 1,814,900,864 / 1,804,664,040 / 1,827,252,960 bytes. That premeasurement harness work is explicitly separate from the measured pool's 0 B/op result.

## Residual compilation and timing boundaries

T retains **123.2130254 seconds, 12,175 parsed events, zero lost events**, 50 matched warmup windows and 25 matched actual windows. All operation counts match BDN. No JIT-start/method-load pair or suspension event is unmatched. The engine actual windows total 24.515155610 seconds, versus 24.513312343 seconds on the benchmark clock; per-iteration wrapper excess is 63,258–85,160 ns.

There are **zero JIT starts inside actual windows**, but **two compilation intervals start in gaps and finish inside**. All four compilations in the wider first-actual-start to last-actual-stop envelope are retained in [compiles.csv](compiles.csv):

| Method | Tier | Compiler thread | Actual window overlapped |
| --- | --- | ---: | ---: |
| `System.Number.UInt64ToDecStr` | OptimizedTier1 | 2899 | 12 |
| `PortableThreadPool.get_ThreadCount` | OptimizedTier1 | 2900 | none |
| `PortableThreadPool.ThreadCounts.VolatileRead` | OptimizedTier1 | 2900 | none |
| `PortableThreadPool.ThreadCounts.get_NumExistingThreads` | OptimizedTier1 | 2900 | 19 |

The benchmark thread is 2819. `UInt64ToDecStr` starts 1,054.935 µs before window 12 and loads 857.710 µs after its start. The entire window/clock excess is only 74.589 µs, so at least **783.121 µs** extends beyond the possible leading dispatch boundary into measured work. This cannot be dismissed as reporting solely outside the clock.

`get_NumExistingThreads` starts 289.421 µs before window 19 and loads 65.382 µs after its start; that window has 73.908 µs total wrapper excess. Its exact overlap with the internal clock is therefore **uncertain**. The engine overlap remains recorded, with no sample or event removed. See [primer-summary.json](primer-summary.json) for the conservative boundary calculation.

No Dekaf or Reservoir method compiles in the wider actual envelope. No GC starts inside an actual window, and no runtime suspension overlaps one. All 96 GCs between the first and last actual events are induced collections in the 24 inter-iteration gaps. The logger's 100 per-generation collections include the edge collections, as explained in the preceding report. Initiating call stacks were not collected; these method/thread identities do not establish which caller triggered tiering.

The preceding experiment had 33 overlapping intervals and 41 compilations in the wider envelope; this one has 2 and 4. That reduction supports the combined fixture intervention, but separate runs and simultaneous primer/warmup changes do not isolate each intervention's effect. The two runs use the same CPU model on different VMs; they are not a same-VM before/after timing comparison. Original fresh-main A1/B/A2 means remain 48.854649 / 44.213577 / 48.732145 ns/op on a different Intel runner, with their original INCONCLUSIVE verdict unchanged.

## Retention, replay, and next work

[Artifact 10043481837](https://github.com/thomhurst/Dekaf/actions/runs/34194281968/artifacts/10043481837): 81,005,089 bytes, SHA-256 `6d133a3a8a879868ae8122aec75585e70c769fc0f8334af7adde2829ce1742dc`, matching GitHub's digest; 271 extracted files. It retains both original and revised hosts, rebuilt fixture output, exact source/plan/workflow archive, tools, inspector, smoke/full traces, raw BDN reports, runtime series, primer series and process statuses.

Before publication, **395 files / 416,699,527 bytes** of original local and hosted evidence were copied and SHA-256-verified outside removable worktrees:

`C:/git/Dekaf-evidence/pr-3142/4479317a650ea51a2a2ecdc0ccf5a7de8fb51c7d/sampler-primer-34194281968/`

`verified-inventory.json` binds those originals. Final publication/source files are supplemental. Local builds, full executable hosts, smoke traces, reanalysis validation, previous-result replay, and future loaded-evidence design notes are included. [file-bindings.json](file-bindings.json) verifies these report copies without changing their bytes.

Save the three `.py.txt` analyzers as `.py` outside the evidence directory, extract the hosted artifact, and choose new output directories:

```text
python -O compare-observer.py EXTRACTED_ARTIFACT comparison-output
python -O analyze-iterations.py EXTRACTED_ARTIFACT/T/events.json EXTRACTED_ARTIFACT/T/bdn attribution-output
python -O summarize-primer.py EXTRACTED_ARTIFACT attribution-output/attribution.json primer-output
```

The default expected warmup count is 50; `--warmup-count 30` replays the previous experiment. That replay reproduces the retained previous comparison and trace summary. Reusing an existing output directory raises `FileExistsError` without changing its result hashes. Explicit validation remains active under `python -O`. All 75 actual and 150 workload-warmup samples, maxima, raw events and compilation intervals are preserved.

The next narrow startup target is the remaining formatting/thread-count helper tiering, with caller attribution or a source-verified primer before another run. Do not promote this fixture as free of measured startup activity. Any eventual product acceptance still needs a fresh-main same-VM Ubuntu A1/B/A2 with a common finalized fixture, changed cleanup-path timing, loaded completed throughput, actual per-message p50/p99/max latency, client CPU/allocation scope, and sustained stability. The current stress job runs on Ubicloud and its existing comparator does not gate maximum latency; neither can silently substitute for those missing requirements. No paid stress run or further identical experiment follows from this result.

## Replay validator corrections

The top-level replay tools now reject mismatched loaded assembly identities, duplicate or incomplete host inventories, and inexact helper signatures where applicable. Primer output directories are created only after validation succeeds. Original versions of the changed tools are preserved byte-for-byte in `measured/`; `file-bindings.json` points to those historical copies with their original hashes. Raw measurements and historical verdicts are unchanged.
