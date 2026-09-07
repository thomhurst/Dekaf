# Paused direct-fetch wake investigation

The full hosted comparison of predecessor `e376956c74b544e961d9e508a5d8402e35765c69` is [INCONCLUSIVE with protected latency losses](https://github.com/thomhurst/Dekaf/pull/3117#issuecomment-5599227822). Baseline A is `551d4d0825dca64b7143d6f18fc2b13baaf1fe0a`; harness is `c317df53bb83fc7016b26696c23603ed9bcfd429`; [run and raw data](https://github.com/thomhurst/Dekaf/actions/runs/34316044558).

Synchronous batch p99 increased 31.08%/32.33% against A1/A2; pending-batch p99 increased 9.73%/11.43%. Control p99 drift was -0.94% and -1.53%. CPU and allocation gains do not offset these losses. Every phase used identical batch counts, including 9,000,000 single-record synchronous batch calls and 16,880 pending batch calls. Different batch work does not explain the difference.

The candidate's synchronous-batch maximum occurred around measured second 145, alongside 50 JIT compilations including `DelayPausedDirectFetchAsync`, cancellation, and exception-resource loading. A normal resume cancels an internal delay, which previously threw and caught `OperationCanceledException`. This is a causal candidate for the isolated exception/JIT burst, not a demonstrated explanation for the sustained p99 increase or separate shutdown maximum.

The modern .NET asset now suppresses the delay's exception when resume changes the pause snapshot. Caller cancellation and internal cancellation without a resume still propagate through the original canceled delay. No signal fields, extra callbacks, or per-message work are added. The netstandard asset keeps its original implementation: its `SuppressThrowing` polyfill catches cancellation internally and would add an unnecessary async wrapper. The initial net8 diagnostic detected this, and the final source explicitly avoids that wrapper.

Three new regression cases verify resume and both cancellation paths. The first-chance exception assertion fails on the predecessor (one exception) and passes on the modern asset (zero). Final focused suites pass 72/72 on net10.0 and 72/72 on net8.0, which exercises the netstandard asset. The real-Kafka ordered processing and automatic-commit integration test passes. Initial and final logs are retained; the net8 diagnostic failure is not presented as a passing run.

The local MemoryDiagnoser probe calls the actual private delay through a cached delegate and publishes the same snapshot-version change before canceling the registered source. A compiled field setter avoids reflection allocation inside measurement. It isolates one control-plane wake from partition snapshot creation. Each invocation includes its cancellation source, timer and completed wait. No message is processed, so allocation is per wake, not per message.

| Product | Mean/wake | 99.9% error | Allocated B/wake |
|---|---:|---:|---:|
| Predecessor `e376956c74b544e961d9e508a5d8402e35765c69` | 2.985 microseconds | 0.0422 microseconds | 1,208 |
| Candidate patch retained here | 987.0 ns | 35.44 ns | 712 |

Windows 11, Intel i7-12700K, SDK 10.0.401/runtime 10.0.12, Release, tiered compilation disabled, BenchmarkDotNet 0.15.8 with MemoryDiagnoser, in-process toolchain, three warmup and ten measured 200-ms iterations, no outlier removal. Both processes first run the workload for at least 20 seconds: 3,850,490 and 25,032,215 completed wakes respectively. Sources and full JSON reports are retained. The benchmarked modern source is unchanged by the final conditional compilation guard. Local results are diagnostic only; they do not measure loaded CPU, per-message tail latency or long-run stability.

The before measurement temporarily restored only `KafkaConsumer.cs` from the predecessor, with the candidate restored in a `finally` block. The retained patch identifies the final product and regression-test changes. No data is trimmed and no acceptance tolerance is widened. A fresh hosted A1/B/A2 measurement must name the final pushed head; earlier-head results do not certify this candidate.
