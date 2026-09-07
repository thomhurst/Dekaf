# Measurement-entry warmup investigation

**Verdict: INCONCLUSIVE.** This local Windows diagnostic identifies a harness startup mechanism. It does not establish hosted acceptance or a product performance change. No revised hosted campaign has been dispatched from this investigation.

Product: `e85fcb90bf1ba746eadb604f6f1347670eb04b98`; pinned main: `5df2f0d03607389384b5c1466e17812a9084fac9`. The diagnostic harness is based on `a035705ef48b496fc715a8a403f46e68c9a48317` plus retained source patches and the phase-marker/trace-inspector sources. Archives bind each working source snapshot to copied build binaries. Later binary-identity instrumentation is a separate harness change and is not attributed to these earlier traces.

Configuration: Windows 11, Intel Core i7-12700K, .NET SDK 10.0.400/runtime 10.0.11, Release/net10.0, `DOTNET_TieredCompilation=1`, `DOTNET_TieredPGO=1`, `DOTNET_gcServer=0`. `dotnet-trace` 9.0.652701 captures runtime JIT events and explicit phase markers; the inspector uses TraceEvent 3.2.6. The initial traced inventory case and the revised traced inventory case both use 60 seconds of actual workload warmup and ten measured seconds. Tracing changes execution and is diagnostic only.

## Observed mechanism

With one complete one-second primer, the original trace retains 60,867,106 warmup calls and 10,914,655 measured calls. The first measured second contains 12 JIT compilations and 1.8678 ms of compilation time. `Probe.TakeSnapshot` receives instrumented Tier 0 at about 29.320 seconds and Tier 1 at about 59.320 seconds. `AdminFixture.Call` receives Tier 1 at about 61.341 seconds, after the measured phase begins at 61.330 seconds. `Probe.MeasureAsync.MoveNext` has an OSR version early, but its complete entry has not received Tier 1 before measurement.

Inference: a long OSR loop does not sufficiently exercise complete measurement entry and low-frequency sampler paths. Re-entering measurement after a single long warmup advances tier counters and can trigger compilation at the sampling boundary. Increasing the loop's duration alone does not guarantee that these paths are ready.

The revised primer enters and finalizes 128 measurements of at least 50 ms, saves every segment, then executes and saves a complete one-second measurement. It still performs the full subsequent 60-second workload warmup. This moves the observed Tier 1 transitions for `AdminFixture.Call`, `Probe.TakeSnapshot`, and complete `Probe.MeasureAsync.MoveNext` entry to approximately 0.448, 2.135, and 3.975 seconds, respectively, before warmup begins at 8.977 seconds. The revised trace retains 64,860,375 warmup calls and 10,887,069 measured calls, with zero JIT events in the last five warmup seconds and all ten measured seconds. Both traces report zero lost events.

## Limits and follow-up

An untraced inventory process with the same primer completes 65,186,140 warmup calls and 11,390,999 measured calls. Its last five warmup seconds contain no compilations, but the measured series records two compilations: one in the second interval (0.1743 ms), one in the tenth (0.1504 ms). An untraced retry case completes 7,615 warmup calls and 1,283 measured calls, with zero compilations in the last five warmup seconds but four during measurement. These results remain in the evidence; the clean trace cannot substitute for them.

A further diagnostic inventory trace extends measurement to 20 seconds. It retains 62,236,083 warmup calls and 18,423,100 measured calls, with two measured compilations totaling 0.5885 ms and zero lost trace events. The measured methods are `System.Buffer.MemmoveInternal` (instrumented Tier 1 at 69.951 seconds) and `System.SpanHelpers.ClearWithoutReferences` (Tier 1 at 73.019 seconds); measurement begins at 68.940 seconds. The latter had received instrumented Tier 1 at 8.066 seconds. These are additional runtime tier transitions, not the previously identified `AdminFixture.Call` or measurement-entry transitions. The trace does not identify callers, and the methods in the separate untraced processes cannot be inferred with certainty from their counters alone.

Residual activity still needs an assessment of its startup contribution and effect on protected metrics. No acceptance tolerance is widened, no measured sample is discarded, and no passing verdict follows from a clean individual trace. The complete-entry primer is a causal harness improvement, not a waiver of the runtime-activity, latency, CPU, allocation, or stability requirements. An extended elapsed warmup is a possible next causal experiment; a new hosted acceptance campaign remains deferred until the measurement design is justified.

The related #3136 and #3129 hosted campaigns, runs `34161413446` and `34163715940`, were stopped because their shared probe used only 30-second warmup and no complete-entry primer. Their exact-head gates remain nonpassing, and partial artifacts are retained. They are incomplete experiments, not confirmed product regressions.

## Retained evidence and reproduction

Durable root: `C:/git/Dekaf-evidence/pr-3128/warmup-boundary/`. `original-trace/` and `reentry-trace/` each contain 88 copied source/build files verified against their originals with SHA-256, plus parent source ZIP and working patch. The root retains raw `.nettrace` files, capture logs, parsed method/phase events, complete primer segments, warmup and measured histograms, and the untraced results. All evidence is outside removable worktrees.

Capture an owned child process, with the runtime environment above:

```powershell
dotnet-trace collect --providers 'Microsoft-Windows-DotNETRuntime:0x10:5,Dekaf-AdminEvidence-Phases:0xffffffffffffffff:5' --rundown false --show-child-io --output TRACE.nettrace -- dotnet tools/AdminDescriptionEvidence/bin/Release/net10.0/Dekaf.Benchmarks.dll probe inventory:16 OUTPUT_DIRECTORY 60 20
dotnet tools/AdminJitTraceInspector/bin/Release/net10.0/AdminJitTraceInspector.dll TRACE.nettrace EVENTS.json
```

The checked-in inspector preserves JIT method names, signatures, IDs, threads, timestamps, tier payloads and phase markers. It rejects traces with lost events. Acceptance remains an unprofiled, same-VM `ubuntu-latest` A1/B/A2 campaign with exact product/harness pins, predeclared identical settings, and all protected metrics assessed against both controls.
