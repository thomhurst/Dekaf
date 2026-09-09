# Performance harness reliability

The maintained harness supports open PR performance investigations through
[`performance-comparison.yml`](../workflows/performance-comparison.yml). It does not change product source, historical
verdicts, acceptance tolerances, scheduled stress lanes, or paid-run limits.

## Observer storage

The four administrative probes previously reserved 65,536 16-byte histogram
entries for each interval. A 480-second warmup plus a 180-second measurement
reserved 662 MiB for those entries alone. Each interval also scanned up to one
million dense tick counters, even when only a few hundred values occurred.

The recorder now shares an archive capped at 4,194,304 entries (64 MiB) across all
intervals, and drains only touched dense counters plus preallocated overflow
buckets. The dense array, touched indices and overflow dictionary add separate,
fixed storage. The per-interval limit remains 65,536 distinct ticks. No allocation
is required while recording or draining. Exhausting either limit aborts the
capture; it never rounds, drops, overwrites, or clips samples. The archive remains
alive until reports are serialized. JSON retains exact tick/count arrays and all
maxima. Both warmup and measured intervals use the same recorder.

The existing copies of `Probe.cs` stay self-contained because historical drivers
copy individual fixture directories into product checkouts. A test verifies that
all four deployed copies match the recorder tested by TUnit.

## CPU isolation and host evidence

The wrapper in `.github/scripts/runner_resources.py` discovers physical core and
socket IDs within the original allowed CPU set. SMT siblings stay on the same
side. One physical core belongs to the client; remaining cores belong to harness
infrastructure and Kafka. A runner with fewer than two physical cores is rejected.
This preserves the existing loaded-workload allocation instead of silently changing
concurrency or offered load.

The wrapper and Python drivers run on infrastructure CPUs. Their subprocesses,
including `docker stats`, inherit that mask. Measured processes explicitly select
the client mask. Pool BenchmarkDotNet affinity no longer overrides the topology
with hard-coded CPU 2. Original allowed CPUs are retained across nested drivers.
In-process runtime samplers remain included in client CPU/allocation scope; this
change does not claim to remove their cost or isolate the host kernel, Docker
daemon, interrupts, or other VM activity.

Every wrapped suite retains `runner-resources/plan.json`, raw one-second
`series.jsonl`, and the child's exit code. Series include `/proc/stat` CPU and steal
counters, memory and swap counters, CPU/memory/I/O pressure stall information,
load, and available disk. These are host observations, not client CPU per message.
Missing required counters fail before starting work; a later sampling failure
terminates the workload and fails collection. A nonzero workload exit stays
nonzero. Artifacts upload even when execution fails.

## Identical-product calibration

Dispatch `performance-comparison.yml` from `main`, setting `harness_sha` to the exact harness revision, with
`suite=admin-calibration`, an existing administrative PR number (3128, 3129, 3136,
or 3138), and identical full `baseline_sha` and `candidate_sha` values. Pin fresh
main before dispatch. The selected PR determines the existing control matrix.

Calibration builds the baseline fixture once without `CANDIDATE`, validates it,
and uses that exact binary path for all A1/B/A2 launches. New APIs with no baseline
equivalent are excluded. Each control runs consecutively in three fresh processes
on one `ubuntu-latest` VM, with 480 seconds continuous workload warmup and 180
seconds measurement per process. Both product inputs must match or collection
fails before building. Budget 33 measured/warmup minutes per control case, plus
builds and fixture validation. There is no automatic repetition.

`calibration.json` reports the existing 3% throughput/CPU, 5% p50/p99/max, and
1 B/call allocation screens, both control deltas, and control drift. A failed
screen demonstrates that this capture cannot distinguish a product change at
those limits. A successful screen is only a repeatability observation for that
configuration; one triplet does not establish maximum-latency precision, loaded
Kafka behavior, long-run stability, or a product performance PASS. JIT/GC and
thread-pool transitions still need attribution using the retained runtime series.
Do not remove maxima or widen tolerances to turn calibration green.

## Validation

`performance-harness-tests.yml` runs TUnit recorder tests, Python comparison and
resource-wrapper tests, and a real Linux child-affinity/resource-capture smoke.
Recorder tests independently verify counts, random interval histograms, exact
long tails, immutable prior intervals, bounded failure, JSON compatibility, zero
record/drain allocations, and the complete probe's report accounting.

These checks validate harness correctness. They are not performance acceptance
for any open product PR. New measurements retain their own product and harness
pins and must not be pooled with previous observer or affinity configurations.
