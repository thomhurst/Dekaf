# Producer harness warmup diagnostics — 2026-09-08

These local correctness/startup diagnostics are **INCONCLUSIVE for performance acceptance**. They are not an A1/B/A2 comparison and do not establish a performance result for the final PR head. All measured samples, including maxima, remain in the raw JSON files.

The product was main `9eec358dad2a081dedbbfc75f02aee743e6bdad9`; only the harness changed between these uncommitted development snapshots. The machine ran Windows 10.0.26200 on an Intel Core i7-12700K (20 logical processors), using SDK 10.0.400, net10.0 Release, and default runtime/JIT settings. Kafka ran in local Docker. No CPU affinity or hosted-runner isolation was applied. Client-wide counters include harness work, and Confluent's native allocations are outside managed GC accounting.

| Diagnostic | Harness state | Measured JIT method-count increase |
| --- | --- | --- |
| `separate-loop-20s.json` | Separate warmup load loop; 20-second warmup; both clients share one broker | Dekaf 504; Confluent 164 |
| `warmup-60s.json` | Shared warmup/measurement loop; elapsed-clock timer; 60-second warmup; both clients share one broker | Dekaf 138; Confluent 30 |
| `warmup-180s.json` | Shared loop; 180-second warmup; Dekaf only | Dekaf 99 |

These are different experiments, not a controlled before/after speed comparison. The first experiment also overlapped briefly with local unit testing. A separate attempted 60-second run stopped during warmup when `CancelAfter` returned before a full cycle duration; no measured result was produced. That failure motivated the monotonic elapsed-time recheck.

The 180-second run completed 180.0576931 seconds of active warmup, 39,817,949 warmup messages and six drain/reuse cycles. Measurement accepted and broker-confirmed 11,482,523 messages with zero loop or delivery errors. Its process recorded 99 additional JIT compilations and 23.9191 ms of compilation time during measurement. The validator rejects JIT activity in the warmup tail before attempting metric comparison; see `warmup-validation.json`. Longer elapsed warmup alone did not establish steady state. Method-level traces are needed to attribute the remaining compilation before acceptance can proceed.

The 180-second command was:

```text
dotnet tools/Dekaf.StressTests/bin/Release/net10.0/Dekaf.StressTests.dll --duration 1 --producer-warmup-seconds 180 --scenario producer --client dekaf --brokers 1 --message-size 1000 --output <artifact-directory>
```

`warmup-180s-binaries.json` records the binaries used by that run. It preceded two final harness corrections: restoring the profiler's measured-phase log marker and disposing the resource observer's `Process` objects. It must not be treated as final-head performance evidence. Final source validation includes the stress harness unit suite, Python script tests and workflow lint.

Future acceptance uses the exact-SHA Ubuntu workflow described in [the warmup contract](../../../tools/Dekaf.StressTests/Warmup.md): identical fixtures, fresh brokers, fixture correctness checks, A1/B/A2 on one VM, retained runtime observations, and a declared warmup duration shared by every phase. An invalid warmup prevents the metric comparator from publishing a misleading pass. This does not waive any protected metric or replace separate consumer, round-trip, BenchmarkDotNet, recovery or stability evidence.
