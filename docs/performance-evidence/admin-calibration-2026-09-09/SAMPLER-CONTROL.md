# Administrative sampler control, 2026-09-09

**Decision: the sampled phase has materially higher CPU cost and lower completed
throughput than both GC-only controls. Product acceptance remains INCONCLUSIVE.**
This experiment intentionally changes profiling, not product code. It supports
keeping CPU sampling out of acceptance measurements and using it only when stack
attribution is needed. It does not explain the earlier unprofiled CPU drift.

## Revisions and measurement

- [Run 34358968465](https://github.com/thomhurst/Dekaf/actions/runs/34358968465),
  [raw artifact 10108596847](https://github.com/thomhurst/Dekaf/actions/runs/34358968465/artifacts/10108596847).
- Product A and B: `a12b5980bb9ca9aa31755f3784c5255d0288be71`, also main at run end.
- Harness/workflow: `e62b3f1dac3311665454e5fc0a2be36224e053f2`.
- One `ubuntu-latest` VM, Ubuntu 24.04 image `20260907.300.1`, AMD EPYC 7763.
  Workload CPU 3; infrastructure/collector CPUs 0,1. SDK 10.0.401, runtime
  10.0.12, Release, workstation GC, tiered compilation and Dynamic PGO enabled.
- One compiled baseline binary; A1, B, A2 run in fresh sequential processes.
  Each uses the same primer, observer heap preparation, 480-second continuous
  workload warmup and 180-second measurement of cached `legacy-delete:16` calls.
  Calls complete sequentially. Latency spans administrative entry through call
  completion; there is no network/broker. CPU and allocation counters cover the
  entire client process and its recorder, excluding the external collector.
- A1/A2 retain GC and clock/phase tracing. B adds only
  `Microsoft-DotNETCore-SampleProfiler:0x0:4`. All phases retain identical JIT
  disassembly settings and exact maximum-call timestamps. Full provider strings,
  runtime settings, counts and durations are in [sampler-summary.json](sampler-summary.json).
- This follows the predeclared [sampler-control plan](../../../.github/benchmarks/ADMIN-ATTRIBUTION.md).
  There is one triplet, no automatic repeat, no trimmed sample and no tolerance change.

All 154 inventory hashes/lengths, both identical 17-file binary trees, loaded
modules and all six raw capture histograms/counter denominators verify. ZIP SHA256
`782a3c23a464846128da7ffe75deb6aef44273cba83bbcc82672934ee35615dd` matches GitHub.
Warmup completes 411,518,016 / 347,061,040 / 418,175,936 calls; measurement completes
155,849,742 / 132,258,008 / 159,703,652 calls, retaining 180 intervals per phase.

## Absolute metrics

| Metric | A1, GC only | B, CPU sampled | A2, GC only | B/A1 | B/A2 | A2/A1 drift |
|---|---:|---:|---:|---:|---:|---:|
| Completed calls/s | 865,831.887 | 734,766.435 | 887,242.502 | -15.14% | -17.19% | +2.47% |
| CPU ns/call | 1,154.789 | 1,296.629 | 1,126.922 | +12.28% | +15.06% | -2.41% |
| Allocated B/call | 2,384.017256 | 2,384.021785 | 2,384.016941 | +0.000190% | +0.000203% | -0.000013% |
| p50 ns/call | 932 | 1,001 | 911 | +7.40% | +9.88% | -2.25% |
| p99 ns/call | 1,532 | 1,592 | 1,502 | +3.92% | +5.99% | -1.96% |
| Maximum ns/call | 3,726,392 | 3,715,888 | 3,686,130 | -0.28% | +0.81% | -1.08% |

GC-only control point deltas fall within the existing 3% throughput/CPU, 5%
latency and 1 B/call allocation allowances. This does not establish statistical
equivalence: there are two control processes, not hundreds of millions of
independent replications. B's six 30-second CPU blocks remain above both controls'
blocks, but native-code differences and host effects limit attribution of the
entire difference specifically to suspension overhead. No product verdict follows.

## Exact maximum-call attribution

Thread-aware replay pairs 82,017 / 586,212 / 83,494 complete suspension sequences,
with zero lost events, unmatched phases or open sequences. Clock uncertainty
brackets are 9.384 / 8.873 / 9.715 microseconds. Every maximum-call interval overlaps
the corresponding GC fully suspended interval for every alignment inside that
bracket. Fully suspended GC durations are 3.633934 / 3.609298 / 3.586390 ms;
maximum calls last 3.726392 / 3.715888 / 3.686130 ms. The GC pause accounts for most,
but not all, of each call duration. These are recurring collections, not grounds
for deleting maxima or shortening the measured interval to avoid GC.

B also has an overlapping `SuspendOther` request-to-restart sequence, but that
sequence's 0.008283 ms fully suspended portion ends before the maximum call.
Reporting its complete 1.495028 ms request duration as a pause inside the call
would therefore be wrong. Request waiting and fully suspended time stay separate.

During measurement, `SuspendOther` counts are 181 / 139,532 / 181; summed fully
suspended times are 0.134863 / 1,150.798932 / 0.246226 ms. CPU-sampler provider
events are absent in both controls and present in B. Not every `SuspendOther`
event is a sampler event. B retains 278,703 measured CPU samples, zero missing
stacks, with managed/external labels kept separate. Sample counts are not CPU time.

## Remaining uncertainty and next decision

Of 35 shared Tier1 methods, seven instruction bodies differ somewhere in the
triplet; five differ between the GC-only controls. `DeleteTopicsAsync.MoveNext`
is 3,785 / 3,794 / 3,785 bytes. A1/B have no delivered measured JIT events; A2
has `System.GC.RunFinalizers` and `System.Buffer.ZeroMemoryInternal`. Thus longer
elapsed warmup cannot be assumed to make every low-frequency runtime path steady.
The retained data must be inspected before treating another campaign as acceptance.

Gen0/1/2 counts are `[22212,138,2]` / `[18850,118,2]` / `[22761,143,2]`.
Managed heap remains approximately 80–97 MB. RSS ranges are 163–167 / 143–149 /
136–141 MB; workers and pending thread-pool items remain zero. Host series reports
zero steal, swap activity and memory pressure. CPU `some` pressure rises from
about 0.23% in controls to 11.00% in B. These host counters do not exclude
hypervisor, frequency or scheduling effects. This short cached workload cannot
establish delivered-message behavior or long-run stability.

The existing unprofiled acceptance path already omits CPU sampling. This finding
therefore changes how diagnostic traces are interpreted; it is not a newly fixed
cause of that path's previous variation. Do not run an unchanged repeat, label
the controls a product PASS, or resume all product campaigns from this result.
Any further experiment must address a specific remaining cause with matched
controls. Earlier results remain separate because profiling, recorder and
native-code behavior differ. No tradeoff or merge is approved by this report.

## Replay

Use the thread-aware inspector and build instructions in [README.md](README.md)
to produce `suspensions-by-thread-review.json` for each raw trace. From this
report directory, run:

```text
python replay/analyze_admin_sampler_control.py ARTIFACT/evidence
python replay/compare_admin_jit.py ARTIFACT/evidence legacy-delete-16
python replay/review_runner_resources.py ARTIFACT
python replay/test_sampler_clock_bounds.py
```

The analyzer resolves validators from this checkout, checks original inventory
and timing, and writes `sampler-control-review.json` beside the original evidence.
The summary additionally retains native-code hashes, CPU blocks, runtime/heap
trends, host data and B's measured stack counts. Use `replay/CpuSamples.cs.txt`
with the B trace and phase JSON to reproduce those counts. The analysis never
grants acceptance; raw traces, every histogram sample and all maxima remain in
the linked artifact.
