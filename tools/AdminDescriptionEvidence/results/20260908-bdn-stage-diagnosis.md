# BDN stage startup diagnosis, 2026-09-08

**Diagnostic only. Product performance acceptance remains INCONCLUSIVE.**

Four successive method-attribution experiments identify and remove background JIT transitions from the measured BDN stage in one local administrative workload. This is not fresh-main performance acceptance and does not resolve the protected-metric losses or control drift in hosted run [34170245379](https://github.com/thomhurst/Dekaf/actions/runs/34170245379).

## Provenance and boundaries

The product source equals `e85fcb90bf1ba746eadb604f6f1347670eb04b98` byte-for-byte under `src/`. These diagnostic binaries were built from auxiliary harness checkout `b9a90c9b884441805eb5d38919dbc4b55a3f1de9`; the generated Dekaf AssemblyInformationalVersion embeds that harness SHA. They are not binaries built from an exact product checkout. Both source snapshots, build metadata, modified fixtures, loaded binaries, generated BDN workers, traces, and analysis scripts are retained. No product source or product runtime settings changed.

Windows x64, Intel i7-12700K, .NET SDK 10.0.400/runtime 10.0.11, Release/net10.0, BenchmarkDotNet 0.15.8, workstation GC, tiered compilation and PGO enabled. Each experiment runs three fresh processes sequentially: untraced, traced, untraced. Every process exercises `delete:16`, the existing cached in-memory administrative delete cycle. Broker/network work is outside this boundary. Each operation is an administrative call, not a per-message hot path.

All processes retain the 128 complete 50-ms probe primer entries, one complete one-second primer, 120 seconds of actual setup workload warmup, and twelve nominal 500-ms actual BDN iterations with `DontRemove`. Raw measured samples, including maxima, are preserved. The original fixture uses six normal BDN warmup iterations (about three seconds); revised fixtures use fifty and the diagnostic driver verifies at least twenty elapsed workload seconds from raw BDN measurements. Setup workload warmup alone did not warm the later BDN engine/reporting path.

The trace attaches only after verifying that the worker PID belongs to the launched BDN host. CLR JIT/load events use dotnet-trace 9.0.652701. No trace reports lost events. The two host signals include UTC/Stopwatch calibration brackets. Clock precision does not bound interprocess signal delay; method names, optimization tiers, and BDN source order distinguish background compilation from pre/post-measurement work.

## Successive causal experiments

1. **Original:** calibration and method tracing only. Background tier transitions occur during the measured stage.
2. **Revised:** prepare the two identified fixed-stage methods before workload warmup; use fifty normal BDN warmup iterations. Generated empty iteration callbacks and number-formatting helpers still tier during measurement.
3. **Primed:** additionally exercise the actual generated empty callbacks and `Measurement.ToString()` for ten seconds before workload warmup. The tracer sees callback optimization only 494 ms and 10 ms before measurement; the final untraced process still records one interior transition.
4. **Callbacks:** call those empty delegates through a cold `NoInlining | NoOptimization` helper during the ten-second primer. This prevents the primer from removing delegate invocations through inlining. The actual BDN callback and workload paths remain unchanged. All three processes record zero JIT transitions in wholly interior measured runtime intervals.

The callback primer reflects the pinned BDN generated fields and requires each callback body to be exactly a single `ret` instruction. It fails if a field/type/body changes; it never primes nonempty user iteration hooks. The preparation and reflection are diagnostic harness setup, outside measured operations.

## Absolute results

BDN means and confidence intervals describe iteration-level timing, not actual per-call p50/p99/max latency. These diagnostic experiments do not establish CPU, completed-throughput, loaded latency, or long-run stability equivalence.

| Experiment | Process | Mean ns/call | 99.9% CI lower | 99.9% CI upper | B/call | BDN workload warmup s | JIT overlap / interior |
|---|---|---:|---:|---:|---:|---:|---:|
| original | untraced1 | 3727.217513 | 3596.378487 | 3858.056539 | 7568 | 3.018882 | 323/29 |
| original | traced | 3849.023028 | 3719.455346 | 3978.590710 | 7568 | 3.047364 | 49/29 |
| original | untraced2 | 3860.773501 | 3722.064725 | 3999.482277 | 7568 | 3.000707 | 321/30 |
| revised | untraced1 | 3805.134858 | 3686.480488 | 3923.789229 | 7568 | 25.708914 | 293/4 |
| revised | traced | 3962.595787 | 3804.685578 | 4120.505996 | 7568 | 24.545270 | 288/4 |
| revised | untraced2 | 3651.409492 | 3603.764165 | 3699.054819 | 7568 | 25.433129 | 22/3 |
| primed | untraced1 | 3766.639471 | 3648.874294 | 3884.404648 | 7568 | 25.062562 | 21/0 |
| primed | traced | 3850.173611 | 3706.151759 | 3994.195464 | 7568 | 25.243287 | 22/0 |
| primed | untraced2 | 3749.850521 | 3626.176827 | 3873.524216 | 7568 | 24.782444 | 19/1 |
| callbacks | untraced1 | 3854.727049 | 3755.081649 | 3954.372449 | 7568 | 24.999867 | 19/0 |
| callbacks | traced | 3973.035295 | 3837.977019 | 4108.093571 | 7568 | 25.563053 | 284/0 |
| callbacks | untraced2 | 3795.687103 | 3644.945652 | 3946.428554 | 7568 | 25.209148 | 285/0 |

Every row contains twelve actual workload iterations. Runtime intervals are approximately one second; wholly interior intervals omit ambiguous boundary overlap for attribution only. No measured sample or raw event is removed.

| Experiment | Process | Setup workload warmup s | Setup completed calls | Engine primer s / callback pairs | Actual completed calls |
|---|---|---:|---:|---:|---:|
| original | untraced1 | 120.000004 | 30264266 | not present | 1649280 |
| original | traced | 120.000003 | 31419109 | not present | 1556544 |
| original | untraced2 | 120.000002 | 31328312 | not present | 1574400 |
| revised | untraced1 | 120.000002 | 31038490 | not present | 1644096 |
| revised | traced | 120.000003 | 29949720 | not present | 1451136 |
| revised | untraced2 | 120.000003 | 30729156 | not present | 1653504 |
| primed | untraced1 | 120.000004 | 30736838 | 10.000000 / 24675788 | 1603200 |
| primed | traced | 120.000004 | 29830309 | 10.000001 / 24870990 | 1563264 |
| primed | untraced2 | 120.000001 | 30447287 | 10.000000 / 22779670 | 1580160 |
| callbacks | untraced1 | 120.000003 | 30913231 | 10.000000 / 24238400 | 1582848 |
| callbacks | traced | 120.000000 | 30856514 | 10.000000 / 23502687 | 1608384 |
| callbacks | untraced2 | 120.000002 | 28263766 | 10.000000 / 22906230 | 1611840 |

| Experiment | Untraced control drift | Traced vs first control | Traced vs second control |
|---|---:|---:|---:|
| original | +3.583% | +3.268% | -0.304% |
| revised | -4.040% | +4.138% | +8.522% |
| primed | -0.446% | +2.218% | +2.675% |
| callbacks | -1.532% | +3.069% | +4.672% |

The controls do not establish zero trace observer effect. Differences across revised harnesses are not product optimization results.

## Method attribution

The original trace contains 29 background tier transitions from 0.523 to 5.055 seconds after the host before-run signal. They include BDN Measurement getters, Perfolizer Frequency, enum/reflection helpers, number formatting, StringBuilder, and span helpers. Two fixed-stage methods first compile before the first timed iteration. Sixteen additional methods belong to post-workload statistics collection, despite appearing before the host receives the after-run signal. No Dekaf method starts compilation inside this traced host window.

With the final callback primer, the 6.433869-second host window contains sixteen JIT starts at 6.419472–6.421952 seconds, all in the post-workload engine/statistics path. No background tier transition appears during the measured stage. The final five seconds before measurement contain only `EngineActualStage.GetWorkload` and the fixed-stage constructor, synchronously selected before the before-run signal; no background tier transition appears there. Empty callback transitions may precede tracer attachment, so the trace does not claim their precise final-tier timestamps.

The [BDN engine](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Engines/Engine.cs), [fixed actual stage](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Engines/EngineActualStage.cs), and [measurement formatting](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Reports/Measurement.cs) define these boundaries. The traced callback experiment captures 175.834 seconds and 4,556 JIT/load events, with zero lost events.

## Standalone probe attribution

A separate single-process `classic:16` trace retains the same 128-entry primer, 120-second actual workload warmup, and 60-second measured probe. It makes no timing comparison and grants no acceptance. The trace captures 188.946 seconds, 4,321 events, and zero lost events.

Warmup completes 34,528,329 calls in 120.000002 seconds. The measured stage completes 17,410,354 calls in 60.000013 seconds: 290,172.504 calls/s, 3,372.634 process CPU ns/call, 11,016.150 allocated B/call, actual call p50 3,100 ns, p99 5,300 ns, and maximum 1,939,500 ns. These are traced absolute diagnostics, not control comparisons. Latency starts immediately before the public fixture call and ends when its result is observed; setup and probe histogram maintenance are outside individual latency samples, while process CPU/allocation include probe work.

Five measured JIT starts occur at +21.857622 s (`System.Array.Clear`), +25.038344 s (`System.SpanHelpers.ClearWithReferences`), and +26.333085/+26.334025/+26.334359 s (`PortableThreadPool.AdjustMaxWorkersActive`, `ThreadInt64PersistentCounter.get_Count`, and `PortableThreadPool.HillClimbing.Update`). All reach OptimizedTier1. JIT time rises by 3.4179 ms. Thread-pool count is two throughout the measured interval, but warmup cycles between zero and two/three workers and still compiles methods in its final second. The method trace identifies compilation, not the calling stacks or its precise effect on latency.

Measured GC counts increase by 14,676 Gen0 and six Gen1 collections, with no Gen2 increase. Endpoint managed heap changes from 16,087,920 to 14,761,256 bytes; RSS changes from 63,041,536 to 66,936,832 bytes. These endpoints do not establish long-run stability; full per-second series and every latency bucket remain archived. This probe independently fails the startup check, so the successful BDN-specific local repair does not justify another full campaign.

## Decision and remaining work

The callback primer is a supported local harness-repair candidate for `delete:16`. It has not been adopted by the shared comparison harness, validated across all cases and revisions, or tested on the required Ubuntu runner. Standalone probes previously recorded separate JIT/thread-pool transitions after their setup warmup; those need independent attribution before another full acceptance campaign. No identical paid campaign was repeated. Product gates remain failed/INCONCLUSIVE.

## Retention

Durable archive: `C:/git/Dekaf-evidence/pr-3128/bdn-stage-20260908/`. Per-experiment verified inventories cover raw reports, every retained worker, loaded binaries, trace tool, inspector, modified fixture, and scripts. A series inventory additionally covers this report, both exact source snapshots, SDK/build metadata, and the summary. File hashes are checked against originals before publication. Raw JSON contains warmup durations, completed operations, CPU/GC/heap/RSS/thread-pool series, and all actual measurements. These diagnostic series do not substitute for missing acceptance metrics.

The [reviewable diagnostic patch](20260908-bdn-stage-candidate.patch) applies to harness `b9a90c9`; it is not enabled in the shared comparison runner. Its exact modified sources were built and exercised in the final three local BDN processes.
