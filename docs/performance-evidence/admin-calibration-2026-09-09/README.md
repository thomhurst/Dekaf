# Administrative calibration attribution, 2026-09-09

The later [sampler-control result](SAMPLER-CONTROL.md) uses precise maximum-call
timestamps and GC-only controls. It measures a different harness and profiling
plan; the historical all-sampled result below remains separate.

**Decision: diagnostic only; product acceptance remains INCONCLUSIVE.** The same
binary still varies across fresh processes. No unchanged triplet or product
campaign is authorized by this experiment, and no tolerance has changed.

## Identity and workload

- [Run 34351058811](https://github.com/thomhurst/Dekaf/actions/runs/34351058811),
  [raw artifact 10105196797](https://github.com/thomhurst/Dekaf/actions/runs/34351058811/artifacts/10105196797).
- A1, B and A2 all use product `a12b5980bb9ca9aa31755f3784c5255d0288be71`.
  Main still points to that SHA at the end of the run.
- Harness and workflow: `e8042153cd071845a942316c0693c2c1a5ece155`.
- One `ubuntu-latest` VM, Ubuntu 24.04 image `20260907.300.1`, AMD EPYC 7763,
  four logical CPUs in two sibling pairs. Workload CPU 3; collector and other
  infrastructure CPUs 0,1. SDK 10.0.401, runtime 10.0.12, Release, workstation GC,
  tiered compilation and Dynamic PGO enabled.
- One compiled baseline binary, three fresh sequential processes, cached
  `legacy-delete:16` calls, the same primer and observer heap preparation, then
  480 seconds continuous workload warmup and 180 seconds measurement per process.
  Each call completes before the next is offered. Latency covers entry to the
  administrative call through completion, without a real network or broker.
- CPU and allocation counters cover the entire client process, including the
  recorder and its in-process observer. The external collector is excluded from
  those process counters but can affect execution. These are completed calls,
  not messages; this is not a claim of zero allocation per message.
- EventPipe providers: `Microsoft-Windows-DotNETRuntime:0x1:4`,
  `Microsoft-DotNETCore-SampleProfiler:0x0:4`,
  `Dekaf-AdminEvidence-Phases:0xFFFFFFFFFFFFFFFF:4`; 128 MiB trace buffer.
  Child-only JIT disassembly covers delete, metadata and capture methods.

All 154 inventory hashes and sizes match. The downloaded ZIP SHA256 is
`c99524487d617848a548d21d271b19e48e893944879f6e8e7ac7b9a19d190f4c`, matching
GitHub's digest. Both 17-file binary trees and every retained loaded-module hash
match. All six warmup/measured histograms and counter denominators replay.

## Absolute results and deltas

| Metric | A1 | B | A2 | B/A1 | B/A2 | A2/A1 drift |
|---|---:|---:|---:|---:|---:|---:|
| Completed calls/s | 747,817.959 | 723,093.167 | 697,250.482 | -3.31% | +3.71% | -6.76% |
| CPU ns/call | 1,256.930 | 1,298.166 | 1,342.741 | +3.28% | -3.32% | +6.83% |
| Allocated B/call | 2,384.021507 | 2,384.022277 | 2,384.023154 | +0.000032% | -0.000037% | +0.000069% |
| p50 ns/call | 922 | 932 | 971 | +1.08% | -4.02% | +5.31% |
| p99 ns/call | 1,733 | 1,793 | 1,853 | +3.46% | -3.24% | +6.92% |
| Maximum ns/call | 3,231,091 | 3,498,079 | 6,979,587 | +8.26% | -49.88% | +116.01% |

Warmup completion counts are 356,750,302 / 347,058,756 / 332,897,793. Measured
counts are 134,607,233 / 130,156,771 / 125,505,146. Each measured phase retains
180 one-second intervals. These large call populations are not independent
fresh-process replications: there are only three processes, and their CPU costs
remain different in the retained 30-second blocks. Do not infer equivalence or
average away this drift. Maxima are preserved without trimming.

GC counts are `[19185,119,2]` / `[18551,116,2]` / `[17887,112,2]` for Gen0/1/2.
No thread-pool workers or pending work items appear. A1/B have no delivered
measured JIT events; A2 has `System.GC.RunFinalizers`. Retained managed heap
ranges overlap at approximately 80–97 MB; A2 RSS is 203–210 MB versus 141–150 MB
in the other processes. Host records show no swap activity or memory pressure,
zero reported steal, and at least 15.3 GB available memory. These observations
do not exclude hypervisor, CPU-frequency, code-placement or scheduling effects.

## Trace findings and limits

Pair suspension phases by **process and requesting thread**. The runtime emits
`GCSuspendEEBegin` before acquiring the thread-store lock and emits restart-end
after releasing it. Process-only pairing can cross-match concurrent requests.
Request-to-restart duration therefore includes waiting and cannot be reported
as entirely stop-the-world time. See the pinned
[runtime implementation](https://github.com/dotnet/runtime/blob/v10.0.12/src/coreclr/vm/threadsuspend.cpp#L5558).

The corrected replay pairs 485,500 / 490,400 / 499,974 suspension sequences.
All three traces report zero event loss, unmatched phases, overlapping requests
on the same thread, or open sequences. The initial process-only diagnostic
output is superseded; it is not the source of this report.

The largest fully suspended GC intervals are 3.143 / 3.304 / 3.389 ms. A1's
maximum call is in the same one-second interval as its largest GC pause. B's
maximum is in second 532; A2's is in second 657, away from their Gen2 collections
near seconds 500 and 600. A2's maximum interval contains a 6.945 ms
`SuspendOther` request, but only 0.042 ms lies between fully-suspended and
restart-begin. This is evidence of a long suspension sequence, **not proof that
the call spent 6.945 ms stopped by the profiler**. Exact call timestamps were
not retained in this historical run.

Six of 35 shared Tier1 instruction bodies differ across processes after
removing comments and diffable addresses. `DeleteTopicsAsync.MoveNext` is
3,812 / 3,794 / 3,794 bytes. This establishes native-code variation, not its CPU
effect. Measured CPU samples have 223,340 / 228,725 / 237,987 records, with no
missing stacks. Managed and external payloads remain separate. Frequent managed
leaves include `Monitor.Enter_Slowpath` and `Thread.PollGC`; external samples
mostly show the EventPipe event dispatcher. Raw sample counts are neither CPU
percentages nor evidence of lock contention duration.

The previous unprofiled calibration
[34333590992](https://github.com/thomhurst/Dekaf/pull/3138#issuecomment-5600417566)
already had 8.55% delete CPU control drift and 54.36% maximum control drift.
Its runner image and harness differ. Source, dependencies and SDK pins are
unchanged between its product and this product, but the exact SHAs differ.
Do not pool those results or treat this profiled run as an unprofiled repeat.

## Rejected local hypotheses and next correction

Local Linux/.NET 10.0.12 diagnostics use shorter 120-second warmup/measurement
phases and are not acceptance evidence. With Dynamic PGO disabled but tiered
compilation retained, all 36 shared Tier1 instruction bodies match, yet CPU
still varies by 3.61%. CPU cost and allocation also worsen: approximately
877–909 ns/call and 2,424 B/call versus 705–745 ns/call and 2,384 B/call with PGO.
Disabling PGO is rejected as a fix.

A separate same-fixture local A–B–A removes retained JIT event history only in
B. Maximum fully suspended GC time is 1.026 / 2.760 / 2.882 ms; maximum call
latency is 7.467 / 8.819 / 10.723 ms. CPU is 795.026 / 805.461 / 808.065 ns/call.
This does not establish improvement against both controls. Removing the observer
is not adopted, and these local observations are not pooled with hosted data.

The next committed correction retains exact maximum-call timestamps, snapshot
timestamps and a bounded EventPipe clock anchor. Replay checks both interval
membership and histogram equality. It reuses the two existing call clock reads,
preserves every sample, and adds no per-call allocation. Its comparison cost is
still observer work, so the changed harness needs fresh calibration.

This fixes an attribution gap, not the unresolved CPU repeatability problem.
No product gate passes from it. Any next hosted experiment must name the causal
variable and preserve the declared controls; another unchanged campaign is not
the next step.

Full precision, per-process details and 30-second CPU blocks are retained in
[hosted-summary.json](hosted-summary.json). The raw artifact contains the traces,
histograms, source archives, binary trees, JIT listings and host time series.

## Replay

Download and extract the linked artifact. Copy `replay/Program.cs.txt` and
`replay/Inspector.csproj.txt` to `Program.cs` and `Inspector.csproj` in a scratch
directory outside the checkout. Build that project in Release using SDK
10.0.401; it pins TraceEvent 3.2.6. For each `A1`, `B`, `A2` directory, run the
resulting `Inspector.dll` with `legacy-delete-16/runtime.nettrace` and
`legacy-delete-16/suspensions-by-thread-review.json` as its two arguments.

Run `python replay/compare_admin_jit.py ARTIFACT/evidence legacy-delete-16`,
then `python replay/summarize.py ARTIFACT/evidence OUTPUT.json` from this report
directory. The summary script verifies the artifact inventory, binary/module
identity, raw accounting and thread-paired suspension integrity. Derived JSON
files are separate from the original inventory. Local experiments are
summarized separately in [local-summary.json](local-summary.json).

`replay/CpuSamples.cs.txt` can replace the scratch project's `Program.cs` to
reproduce raw CPU stack counts. Its arguments are the trace path, output JSON,
and thread-paired suspension JSON for measured-phase boundaries. It retains
managed/external labels; it does not compute CPU percentages.
