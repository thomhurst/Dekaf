# Attribute failed identical-product calibration

Run 34333590992 used the same compiled baseline binary in all six processes.
The legacy-delete control still varied from 1,130.880 to 1,266.083 and 1,227.539
CPU ns/call. The difference persists across 30-second blocks, while all six
retained maxima occur in intervals containing a Gen2 collection. The existing
JIT counters and one-second GC counts cannot distinguish code generation,
GC pause duration, and other process or host effects. The result remains
[inconclusive](https://github.com/thomhurst/Dekaf/pull/3138#issuecomment-5600417566).

## Bounded diagnostic

Use `suite=admin-profile`, `pr=3138`, and identical fresh-main baseline/candidate
SHAs. Pin the harness independently. This mode measures only
`legacy-delete:16`, using the same compiled baseline binary in A1, B, and A2 on
one `ubuntu-latest` VM. Each fresh process retains the existing primer,
480-second continuous warmup, and 180-second measurement. There is one triplet
and no automatic repeat. Other administrative suites retain their existing
workloads and settings.

The collector runs on the infrastructure CPU mask. The child retains the
existing single client CPU. The pinned `dotnet-trace` version comes from
`.config/stress-diagnostics/dotnet-tools.json`. It records GC events, sampled
managed stacks, and phase markers from startup through finalization. Only the
child receives JIT disassembly settings, covering delete, metadata refresh,
metadata management and the probe loop. Missing trace or JIT output fails
collection. Full settings and source/binary hashes remain in the artifact.

This is **diagnostic only**. EventPipe and JIT output affect execution. Neither
a successful workflow nor a favorable comparison grants product acceptance.
The runtime, JIT/PGO settings, observer allocation scope, raw histograms, maxima,
and existing comparison tolerances remain explicit. No startup samples or
maxima are removed.

## Predeclared analysis

1. Verify binary identity, raw accounting, trace event loss, phase boundaries,
   and paired GC suspension/restart events before interpreting results.
2. Match maximum-latency intervals to actual GC pause intervals and reasons.
   A one-second coincidence alone is not causal attribution. Retain unmatched
   pauses/events and report any timing-resolution limitation.
   Pair suspension phases by process and requesting thread. A suspension request
   is emitted before acquiring the thread-store lock; request-to-restart time
   includes waiting and is not entirely time with managed threads suspended.
3. Compare retained Tier1 code across fresh processes and inspect sampled CPU
   stacks. Distinguish stable code-generation differences from ongoing JIT
   transitions. Sample counts are not CPU time and native/wait samples are not
   silently reclassified as managed CPU work.
4. Compare the new profiled observations with the previous unprofiled control
   only as diagnostic context. Do not pool their metrics or claim equivalence
   across changed harness, product, profiling or host settings.
5. Choose a causal correction from the evidence. If attribution is insufficient,
   report that limitation and design the next distinct experiment. Do not run
   another unchanged triplet or resume all product campaigns.

After a correction, establish unprofiled identical-product repeatability before
running a frozen representative PR through its complete acceptance scope.

## Exact maximum-call timing

Each interval now retains the first call attaining its maximum, including the
original `Stopwatch` start/end timestamps. All histogram samples still survive.
Snapshots retain their clock timestamp too, and replay rejects a maximum outside
its interval or a duration that differs from the histogram. This adds no clock
read or allocation per call, but the extra comparison remains observer work.

Before workload warmup, the probe emits a `Clock` event whose payload matches
`TraceClock.BeforeTimestamp`. The trace event's timestamp maps to a stopwatch
value between `BeforeTimestamp` and `AfterTimestamp`; use this entire bracket
when testing overlap. A preceding event primes serialization and is not the
retained anchor. Never silently treat synchronization uncertainty as zero.
The timing fields improve attribution only; they do not relax acceptance or
make earlier captures retroactively precise.

The child-launch mechanism follows the
[official dotnet-trace documentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace).
