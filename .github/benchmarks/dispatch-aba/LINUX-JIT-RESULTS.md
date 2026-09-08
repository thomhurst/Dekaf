# PR #3117 local Linux JIT diagnostic — 2026-09-08

**Incomplete diagnostic; no warmed JIT attribution or performance acceptance.** The first full untraced workload fails at final offset commit, and a separate four-second preparation seed reproduces the same failure. The planned traced/untraced comparison stops. Hosted run 34176568856 remains INCONCLUSIVE with its original protected-metric losses intact.

## Observations

| Process | Workload | Outcome |
|---|---|---|
| Original traced smoke | 2 s warmup + 2 s measured, 1,000 records/s | 4,000 records and final commits verified; trace/PID/clock validation passes |
| U1, untraced | 121 s offered warmup + 120 s measured, 50,000 records/s | All 12,050,000 producer records acknowledged and handled; final partition-0 OffsetCommit throws NotCoordinator; no successful commit verification or final metric summary |
| Prepared smoke seed, untraced | 2 s warmup + 2 s measured, 1,000 records/s | All 4,000 records acknowledged; raw handler latency slots populated; final partition-0 OffsetCommit throws NotCoordinator |
| Original T/U2 and all prepared measured phases | Not started | No measurement or trace exists |

Both failed consumers exit 134 with an unhandled `Dekaf.Errors.GroupException`; Docker reports `OOMKilled=false`. Their broker and client inspection/log files are retained. Cleanup occurs only after the consumer exits; the broker is not stopped by the driver while the consumer is committing. All diagnostic containers and their own anonymous volumes are removed.

The first error is:

```text
OffsetCommit failed for dispatch-jit-d693d130392e-0: NotCoordinator
```

The stack traverses `ConsumerCoordinator.CommitOffsetsAsync`, `RetryHelper.WithRetryAsync`, `KafkaConsumer.CommitAsync`, `PartitionedConsumerRuntime.CommitLaneAsync`/`StopLaneAsync`/`StopAllAsync`, and the fixture's final drain. The prepared seed fails on `dispatch-jit-e7e1eafc8bf9-seed-0` through the same path. The coordinator and retry-helper source is identical in retained A and B; this is not evidence that the PR introduced the failure, nor proof that baseline behavior is unaffected.

U1 retains the complete 96,400,000-byte all-message latency array and runtime series. The producer reports Sent=Acknowledged=12,050,000, Failed=0; the final sampled consumer state reports Completed=12,050,000, Measured=6,000,000 and Backlog=0. All latency slots are positive. This does not establish committed progress: execution aborts before that check. The prepared seed retains its complete 4,000-entry latency array and producer acknowledgements, but likewise has no verified final commit. No missing CPU/allocation boundaries or final metrics are reconstructed from partial samples.

## Broker-state hypothesis and bounded revision

The hosted phase executes synchronous records before synchronous batches on the same broker. Successful prior commits initialize the broker's offsets/coordinator state. This local isolation instead begins synchronous batches on a fresh broker. In U1, `__consumer_offsets` creation begins at approximately 03:18:56 UTC and coordinator partitions finish loading around 03:18:59 UTC, coinciding with the final commit failure. The shorter prepared seed also triggers initial offsets loading, which finishes around 03:23:33 UTC just before its recorded process exit.

A revised plan introduces a separate seed process and requires successful broker-confirmed commits before starting any fresh measured process. The seed itself fails, so that preparation is not accepted and no revised measurement runs. The earlier failed U1 is retained. This short hypothesis test does not repeat the full performance experiment for green.

Initial offsets creation/loading is an observed difference and a candidate explanation. There is no retained request/response-level retry timeline proving the cause, no baseline reproduction yet, and no proof that longer retries or broker precreation are the correct product fix. The traced smoke's success does not prove tracing repairs the failure; timing and broker initialization can differ.

## Trace validation and exact inputs

The original smoke trace spans 5.353 seconds, contains 15,811 parsed events and has zero lost events. The recorded consumer PID is 25. Raw QPC scale is exactly 1,000,000 ticks/ms, matching the fixture's 1 GHz Stopwatch. Trace events cover measurement boundaries 408505072238813–408507122839143; event coverage extends from 408502828742384 to 408508170703114. There are 431 JittingStarted events inside the smoke measurement and a runtime counter delta of 430. Start events and compiled-method counters have different completion/boundary semantics; they are reported separately. This short startup trace is not warmed attribution.

Exact retained Linux binaries from hosted run 34176568856 are reused without rebuild: producer A=`5df2f0d03607389384b5c1466e17812a9084fac9`, consumer B=`2a550007ccb091e8f2bf9275a7646277e984dff8`, fixture=`7614406221b63c0b758f25e0722c8a3bf3ef9d0f`. All 97 copied host/tracer/inspector input files match their archived or installed originals. Initial local plan/driver commit is `b469eb8f48c809b877cffffb1bd91094839f6290`; the final revision adds the failed preparation hypothesis and report. Shell wrappers are retained unchanged and published alongside the driver. This harness branch must not be merged into the product.

Environment: Ubuntu 24.04 container, SDK 10.0.400, runtime 10.0.11, local Docker VM with 20 logical CPUs, workstation GC, tiered compilation enabled and dynamic GC adaptation disabled. Broker uses CPU 0, baseline producer CPU 1, candidate consumer CPUs 2/3, trace collector CPU 4. The SDK and Kafka 4.3.1 image IDs are pinned in LINUX-JIT-PLAN.md and `images.txt`. Local hardware and isolated fresh-broker state differ from the hosted experiment.

The retained dotnet-trace 9.0.652701 tool runs with a tool-only `--roll-forward Major` argument. Collection enables CLR JIT events (`Microsoft-Windows-DotNETRuntime:0x10:5`), a 128 MB buffer, no rundown, a seven-minute cap and child launch. AdminJitTraceInspector from the earlier pinned #3128 diagnostic parses the trace with exact PIDs and raw QPC timestamps.

## Retention and next action

Durable archive: `C:/git/Dekaf-evidence/pr-3117/linux-jit-20260908/`. It contains all three attempted workloads, raw failures, successful smoke trace and parsed events, exact input binaries, source snapshots, plan, driver, shell wrappers, runtime/latency arrays, broker logs, inspection and cleanup records. Copies are verified by SHA-256 before the worktree is released; the input manifest separately binds the 97 binaries/files to their original sources. Core files printed as “core dumped” by the shell are not claimed retained.

Before another warmed trace or full comparison, isolate the first manual-commit failure with coordinator request/response timing and baseline/candidate controls. A successful broker preparation must be proven rather than inferred from topic-list readiness. Preserve the existing commit/error behavior and never suppress the failure to complete a performance run. No product code, retry policy, acceptance threshold or performance gate changes in this diagnostic.
