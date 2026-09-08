# Thread-pool attribution, 2026-09-08

Exact product `0da5cd6d6515cb20f639512399d77301b19cab2d`; pinned main `9eec358dad2a081dedbbfc75f02aee743e6bdad9`. Harness lineage `bc9618f16b0ae2cf5c189e2bdb3b049dd24b735d` plus retained task-specific fixture/inspector hashes.

**INCONCLUSIVE for acceptance.** One local Windows trace attributes runtime events. No A/B performance comparison, hosted acceptance, repeatability claim, approved tradeoff, sample trimming or primer adoption.

Predeclared hypothesis (not an established conclusion): Previously measured worker growth reflects periodic background thread-pool adjustment, rather than unresolved initial JIT. Capture adjustment reasons, worker lifecycle and enqueue/dequeue events to distinguish this. A single trace can attribute observed events but cannot prove stabilization or repeatability.

Same classic:16 fixture, twenty-second timer primer and sixty-second wake primer as earlier diagnostic. Probe snapshots additionally retain absolute Stopwatch ticks; inspector retains raw trace QPC ticks for exact window correlation on Windows. Runtime provider adds ThreadingKeyword (0x10000) to JitKeyword (0x10); no product or threshold changes.

Setup: Release, SDK 10.0.400, .NET 10.0.11, workstation GC, tiered compilation and TieredPGO enabled. Same classic:16 fixture (sixteen groups, one assigned member per group); twenty-second timer primer, sixty-second wake primer, 128 complete 0.05-second probe entries plus one second, 120 seconds of actual workload warmup and sixty measured seconds. All primer timer callbacks drain before measured work. One synchronous closed-loop caller; per-call latency includes the fixture call and completion. Process CPU/allocation include probe sampling and histogram bookkeeping. No broker runs in this mock administrative workload.

Provider `Microsoft-Windows-DotNETRuntime:0x10010:5,Dekaf-AdminEvidence-Phases:0xffffffffffffffff:5` captures JIT plus thread lifecycle and adjustment events. No enqueue/dequeue events were observed; the managed queue requires an additional provider described below. Inspector rejects lost events. Added snapshot timestamps retain the exact Stopwatch instant used for each snapshot's elapsed time; raw trace QPC timestamps share that clock on this Windows run. The derived trace frequency is 10000000.000000000 ticks/s, matching the worker's 10000000 ticks/s. Both measurement endpoints fall inside their enclosing phase events, and QPC duration matches stored measured duration within one microsecond. Relative phase markers alone are no longer used to classify boundary-adjacent events.

| Warm seconds / calls | Measured seconds / calls | Calls/s | CPU ns/call | B/call | p50 ns | p99 ns | Maximum ns |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 120.0000014 / 36278254 | 60.0000029 / 18157416 | 302623.585 | 3251.943 | 11016.140 | 3000 | 5100 | 1277200 |

Measured JIT counters: 2406 to 2407; compilation time delta 0.191000 ms. Measured JIT-start events: 1. Thread-pool threads: 2 to 2. Pending work endpoints: 0 / 0.

All captured worker starts/stops and adjustment decisions, including setup:

| Seconds relative to actual measurement start | Event | OS thread | Payload |
|---:|---|---:|---|
| -209.262400 | ThreadPoolWorkerThread/Start | 6080 | ActiveWorkerThreadCount=1, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -209.262195 | ThreadPoolWorkerThread/Start | 83644 | ActiveWorkerThreadCount=2, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -209.262014 | ThreadPoolWorkerThreadAdjustment/Adjustment | 6080 | AverageThroughput=0, NewWorkerThreadCount=20, Reason=Initializing, ClrInstanceID=0 |
| -207.268104 | ThreadPoolWorkerThreadAdjustment/Adjustment | 83644 | AverageThroughput=5.485326855420353E-05, NewWorkerThreadCount=21, Reason=Warmup, ClrInstanceID=0 |
| -203.024822 | ThreadPoolWorkerThreadAdjustment/Adjustment | 83644 | AverageThroughput=63.15831198853402, NewWorkerThreadCount=20, Reason=Warmup, ClrInstanceID=0 |
| -199.034041 | ThreadPoolWorkerThreadAdjustment/Adjustment | 83644 | AverageThroughput=63.64667375967919, NewWorkerThreadCount=21, Reason=Warmup, ClrInstanceID=0 |
| -199.033876 | ThreadPoolWorkerThread/Start | 110268 | ActiveWorkerThreadCount=3, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -194.810608 | ThreadPoolWorkerThreadAdjustment/Adjustment | 110268 | AverageThroughput=63.455365815330026, NewWorkerThreadCount=20, Reason=Warmup, ClrInstanceID=0 |
| -190.823854 | ThreadPoolWorkerThreadAdjustment/Adjustment | 110268 | AverageThroughput=63.71158138340567, NewWorkerThreadCount=21, Reason=ClimbingMove, ClrInstanceID=0 |
| -170.816599 | ThreadPoolWorkerThreadAdjustment/Adjustment | 6080 | AverageThroughput=5.895345631230996, NewWorkerThreadCount=20, Reason=ThreadTimedOut, ClrInstanceID=0 |
| -170.816596 | ThreadPoolWorkerThread/Stop | 6080 | ActiveWorkerThreadCount=2, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -157.815391 | ThreadPoolWorkerThreadAdjustment/Adjustment | 83644 | AverageThroughput=3.8474972892033716, NewWorkerThreadCount=21, Reason=ClimbingMove, ClrInstanceID=0 |
| -157.815232 | ThreadPoolWorkerThread/Start | 110072 | ActiveWorkerThreadCount=3, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -137.801820 | ThreadPoolWorkerThreadAdjustment/Adjustment | 110268 | AverageThroughput=0.8267869334365674, NewWorkerThreadCount=20, Reason=ThreadTimedOut, ClrInstanceID=0 |
| -137.801797 | ThreadPoolWorkerThread/Stop | 110268 | ActiveWorkerThreadCount=2, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -108.764921 | ThreadPoolWorkerThread/Stop | 83644 | ActiveWorkerThreadCount=1, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -108.764797 | ThreadPoolWorkerThread/Stop | 110072 | ActiveWorkerThreadCount=0, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -98.732601 | ThreadPoolWorkerThread/Start | 98484 | ActiveWorkerThreadCount=1, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -98.732429 | ThreadPoolWorkerThread/Start | 5604 | ActiveWorkerThreadCount=2, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -91.969685 | ThreadPoolWorkerThread/Start | 109296 | ActiveWorkerThreadCount=3, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -71.962346 | ThreadPoolWorkerThread/Stop | 98484 | ActiveWorkerThreadCount=2, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -71.915829 | ThreadPoolWorkerThread/Stop | 109296 | ActiveWorkerThreadCount=1, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -71.915809 | ThreadPoolWorkerThread/Stop | 5604 | ActiveWorkerThreadCount=0, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -68.730897 | ThreadPoolWorkerThread/Start | 42316 | ActiveWorkerThreadCount=1, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -68.730752 | ThreadPoolWorkerThread/Start | 50228 | ActiveWorkerThreadCount=2, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -41.811059 | ThreadPoolWorkerThread/Stop | 42316 | ActiveWorkerThreadCount=1, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -41.811018 | ThreadPoolWorkerThread/Stop | 50228 | ActiveWorkerThreadCount=0, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -38.417163 | ThreadPoolWorkerThread/Start | 95328 | ActiveWorkerThreadCount=1, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -38.416908 | ThreadPoolWorkerThread/Start | 75164 | ActiveWorkerThreadCount=2, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -31.951210 | ThreadPoolWorkerThread/Start | 69132 | ActiveWorkerThreadCount=3, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -11.944557 | ThreadPoolWorkerThread/Stop | 75164 | ActiveWorkerThreadCount=2, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -11.789751 | ThreadPoolWorkerThread/Stop | 69132 | ActiveWorkerThreadCount=1, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -11.789561 | ThreadPoolWorkerThread/Stop | 95328 | ActiveWorkerThreadCount=0, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -8.355538 | ThreadPoolWorkerThread/Start | 113768 | ActiveWorkerThreadCount=1, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| -8.355266 | ThreadPoolWorkerThread/Start | 110364 | ActiveWorkerThreadCount=2, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| +18.243715 | ThreadPoolWorkerThread/Stop | 113768 | ActiveWorkerThreadCount=1, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| +18.243733 | ThreadPoolWorkerThread/Stop | 110364 | ActiveWorkerThreadCount=0, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| +21.695216 | ThreadPoolWorkerThread/Start | 98224 | ActiveWorkerThreadCount=1, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| +21.695339 | ThreadPoolWorkerThread/Start | 109864 | ActiveWorkerThreadCount=2, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| +48.336464 | ThreadPoolWorkerThread/Stop | 109864 | ActiveWorkerThreadCount=1, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| +48.337183 | ThreadPoolWorkerThread/Stop | 98224 | ActiveWorkerThreadCount=0, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| +51.722823 | ThreadPoolWorkerThread/Start | 47600 | ActiveWorkerThreadCount=1, RetiredWorkerThreadCount=0, ClrInstanceID=0 |
| +51.723007 | ThreadPoolWorkerThread/Start | 113692 | ActiveWorkerThreadCount=2, RetiredWorkerThreadCount=0, ClrInstanceID=0 |

JIT starts within five seconds before measurement through 100 ms after its exact end:

| Relative seconds | In actual window | Method | Following load tier |
|---:|---|---|---|
| +48.445014 | True | `System.Threading.LowLevelLock.TryAcquire_NoFastPath` | OptimizedTier1Instrumented |
| +60.010037 | False | `System.Globalization.NumberFormatInfo.<GetInstance>g__GetProviderNonNull\|58_0` | OptimizedTier1 |
| +60.010293 | False | `System.Text.Json.Serialization.Metadata.JsonPropertyInfo`1[System.Int32].GetMemberAndWriteJson` | OptimizedTier1 |
| +60.012445 | False | `System.Text.Json.Serialization.JsonConverter`1[System.Int32].TryWrite` | OptimizedTier1 |
| +60.012899 | False | `System.Text.Json.Serialization.Converters.Int32Converter.Write` | OptimizedTier1 |
| +60.013121 | False | `System.Text.Json.Serialization.Metadata.JsonPropertyInfo`1[System.Double].GetMemberAndWriteJson` | OptimizedTier1 |
| +60.014897 | False | `System.Text.Json.Serialization.JsonConverter`1[System.Double].TryWrite` | OptimizedTier1 |
| +60.016351 | False | `System.Text.Json.Serialization.Converters.DoubleConverter.Write` | OptimizedTier1 |
| +60.017409 | False | `System.Number.FormatFloat` | OptimizedTier1 |
| +60.019572 | False | `Dekaf.Benchmarks.Probe+Result.get_Seconds` | QuickJitted |
| +60.019671 | False | `Dekaf.Benchmarks.Probe+Result.get_Completed` | QuickJitted |
| +60.020489 | False | `Dekaf.Benchmarks.AdminFixture.DisposeAsync` | QuickJitted |
| +60.020596 | False | `System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder.Start` | QuickJitted |
| +60.020631 | False | `System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start` | QuickJitted |
| +60.020715 | False | `Dekaf.Benchmarks.AdminFixture+<DisposeAsync>d__12.MoveNext` | QuickJitted |
| +60.020896 | False | `Dekaf.Admin.AdminClient.DisposeAsync` | QuickJitted |
| +60.020973 | False | `System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder.Start` | QuickJitted |
| +60.021000 | False | `System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start` | QuickJitted |
| +60.021089 | False | `Dekaf.Admin.AdminClient+<DisposeAsync>d__165.MoveNext` | QuickJitted |
| +60.021336 | False | `Dekaf.Admin.AdminClientOptions.get_RequestTimeoutMs` | QuickJitted |
| +60.021367 | False | `Dekaf.Telemetry.ClientTelemetryManager.StopAsync` | QuickJitted |
| +60.021482 | False | `System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder.Start` | QuickJitted |
| +60.021515 | False | `System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start` | QuickJitted |
| +60.021589 | False | `Dekaf.Telemetry.ClientTelemetryManager+<StopAsync>d__27.MoveNext` | QuickJitted |
| +60.022091 | False | `Dekaf.Telemetry.ClientTelemetryManager.DisposeAsync` | QuickJitted |
| +60.022206 | False | `System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder.Start` | QuickJitted |
| +60.022238 | False | `System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start` | QuickJitted |
| +60.022317 | False | `Dekaf.Telemetry.ClientTelemetryManager+<DisposeAsync>d__28.MoveNext` | QuickJitted |
| +60.022509 | False | `Dekaf.Telemetry.ClientTelemetryManager..cctor` | QuickJitted |
| +60.022797 | False | `Microsoft.Extensions.Logging.EventId..ctor` | QuickJitted |
| +60.022833 | False | `Microsoft.Extensions.Logging.LogDefineOptions..ctor` | QuickJitted |
| +60.022856 | False | `Microsoft.Extensions.Logging.LogDefineOptions.set_SkipEnabledCheck` | QuickJitted |
| +60.022878 | False | `Microsoft.Extensions.Logging.LoggerMessage.Define` | QuickJitted |
| +60.022984 | False | `Microsoft.Extensions.Logging.LoggerMessage+<>c__DisplayClass8_0..ctor` | QuickJitted |
| +60.023007 | False | `Microsoft.Extensions.Logging.LoggerMessage.CreateLogValuesFormatter` | QuickJitted |
| +60.023156 | False | `Microsoft.Extensions.Logging.LogValuesFormatter..ctor` | Optimized |
| +60.025135 | False | `Microsoft.Extensions.Logging.LogValuesFormatter.FindBraceIndex` | QuickJitted |
| +60.025291 | False | `Microsoft.Extensions.Logging.LogValuesFormatter.get_ValueNames` | QuickJitted |
| +60.025327 | False | `Microsoft.Extensions.Logging.LogDefineOptions.get_SkipEnabledCheck` | QuickJitted |
| +60.025349 | False | `Microsoft.Extensions.Logging.LoggerMessage.Define` | QuickJitted |
| +60.025480 | False | `Microsoft.Extensions.Logging.LoggerMessage+<>c__DisplayClass10_0`1[Dekaf.Protocol.ErrorCode]..ctor` | QuickJitted |
| +60.025517 | False | `Microsoft.Extensions.Logging.LoggerMessage.Define` | QuickJitted |
| +60.025593 | False | `Microsoft.Extensions.Logging.LoggerMessage+<>c__DisplayClass10_0`1[System.Boolean]..ctor` | QuickJitted |
| +60.025624 | False | `Microsoft.Extensions.Logging.LoggerMessage.Define` | QuickJitted |
| +60.025717 | False | `Microsoft.Extensions.Logging.LoggerMessage+<>c__DisplayClass12_0`2[System.Int32,System.Int32]..ctor` | QuickJitted |
| +60.025744 | False | `Microsoft.Extensions.Logging.LoggerMessage.Define` | QuickJitted |
| +60.025811 | False | `Microsoft.Extensions.Logging.LoggerMessage+<>c__DisplayClass10_0`1[System.Int32]..ctor` | QuickJitted |
| +60.025839 | False | `System.Threading.Tasks.TaskCompletionSource`1[System.Boolean].get_Task` | QuickJitted |
| +60.025869 | False | `Dekaf.Metadata.MetadataManager.DisposeAsync` | QuickJitted |
| +60.025946 | False | `System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder.Start` | QuickJitted |
| +60.025974 | False | `System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start` | QuickJitted |
| +60.026056 | False | `Dekaf.Metadata.MetadataManager+<DisposeAsync>d__104.MoveNext` | QuickJitted |
| +60.026317 | False | `Dekaf.Metadata.MetadataManager.WaitForInitializationToDrainAsync` | QuickJitted |
| +60.026391 | False | `System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder.Start` | QuickJitted |
| +60.026419 | False | `System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start` | QuickJitted |
| +60.026492 | False | `Dekaf.Metadata.MetadataManager+<WaitForInitializationToDrainAsync>d__105.MoveNext` | QuickJitted |
| +60.026626 | False | `Dekaf.Internal.SemaphoreHelper.ReleaseSafely` | QuickJitted |
| +60.026667 | False | `Dekaf.Testing.InMemoryAdminClient.DisposeAsync` | QuickJitted |

Measured intervals with JIT or worker-count changes:

| Interval seconds | Completions | CPU ns/call | Max ns | JIT delta | Worker count | Pending work |
|---|---:|---:|---:|---:|---|---|
| 18.000-19.000 | 275064 | 3578.713 | 621600 | 0 | 2 to 0 | 0 to 0 |
| 21.000-22.000 | 311491 | 3210.366 | 330300 | 0 | 0 to 2 | 0 to 0 |
| 48.000-49.000 | 273714 | 3596.363 | 580800 | 1 | 2 to 0 | 0 to 0 |
| 51.000-52.000 | 312941 | 3145.561 | 258400 | 0 | 0 to 2 | 0 to 0 |

Observed outcome: the measured worker population repeatedly retires from two to zero and restarts from zero to two. There is no measured worker-count adjustment decision; a hill-climbing sample/statistics pair occurs at +54.038 seconds without changing the goal. The earlier third-worker growth did not recur, so its cause remains unproven. The retained 1.2772 ms maximum falls in the 38-39 second interval, with no JIT-count increment. The single measured compilation is LowLevelLock.TryAcquire_NoFastPath at +48.445014 seconds (OptimizedTier1Instrumented), approximately 108 ms after the second retirement pair. That timing is an association, not proof of causation. Formatting, JSON serialization and disposal compilations after +60.010 seconds are definitively outside this run's actual 60.0000029-second window.

Managed enqueue/dequeue attribution remains missing: ThreadPoolWorkQueue logs through [FrameworkEventSource](https://github.com/dotnet/runtime/blob/v10.0.11/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Tracing/FrameworkEventSource.cs), whose provider is System.Diagnostics.Eventing.FrameworkEventSource and ThreadPool keyword is 0x2. The native runtime ThreadingKeyword alone does not supply those managed queue events. A next causal trace would add System.Diagnostics.Eventing.FrameworkEventSource:0x2:5 and inspect its dynamic events, while preserving these QPC boundaries and the same workload/primers. Keep the separate ThreadTransfer keyword disabled because its timer events could add per-request tracing overhead. Work IDs are object hash codes, so collision/type attribution limits must remain explicit. No such second capture was dispatched in this experiment.

All one-second samples, latency histograms/maxima, CPU, allocations, GC, heap/RSS and pending-work trends are retained. The snapshot timestamp precedes reading CPU/GC/thread-pool counters, as before; tiny boundary differences between those reads and trace events remain observational limits. Added tracing may itself alter thread scheduling and JIT activity; this trace cannot establish uninstrumented steady state.

Runtime source references: [thread-pool hill-climbing decisions](https://github.com/dotnet/runtime/blob/v10.0.11/src/libraries/System.Private.CoreLib/src/System/Threading/PortableThreadPool.HillClimbing.cs), [worker lifecycle](https://github.com/dotnet/runtime/blob/v10.0.11/src/libraries/System.Private.CoreLib/src/System/Threading/PortableThreadPool.WorkerThread.cs), [event keywords and payloads](https://github.com/dotnet/runtime/blob/v10.0.11/src/coreclr/vm/ClrEtwAll.man). These are retained alongside the trace.

Full exact-head hosted acceptance remains missing. No broader stability conclusion follows from one sixty-second local measurement. Earlier control drift and protected-metric losses remain in their original reports.

Durable archive: `C:/git/Dekaf-evidence/pr-3128/threadpool-attribution-20260908/`. Publication verified 180 file hashes and 7 loaded-assembly identities. Includes complete fixture and inspector builds, exact product/main/harness source ZIPs, the lossless 269.817-second trace, all runtime events and measurement samples, inputs/hashes, scripts, runtime source references and SDK/hardware details. The original inspector compile failure is retained; the final inspector builds cleanly and its trace validation succeeds.

The adjacent experimental patch contains the sampler timestamp change and complete standalone inspector source. It is a diagnostic patch, not a change to the hosted workflow or its acceptance criteria.
