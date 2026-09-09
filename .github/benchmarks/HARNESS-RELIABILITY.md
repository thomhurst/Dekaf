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

## CPU isolation

The helper in `.github/scripts/runner_affinity.py` discovers physical core and
socket IDs within the original allowed CPU set. SMT siblings stay together.
One physical core belongs to the loaded client; remaining cores belong to the
broker and driver. Fewer than two physical cores is unsupported by these lanes.
The shared micro suite uses normal runner affinity.

The custom background host-resource sampler and its wrapper are removed.
Loaded workload counters still include their observation cost in process CPU and
allocations. Investigate runtime behavior manually with standard tools in separate
runs; host CPU does not substitute for client CPU per completed message.

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
Kafka behavior, long-run stability, or a product performance PASS. Assess throughput,
latency, CPU, allocations and stability; JIT activity is not a gate.
Do not remove maxima or widen tolerances to turn calibration green.

## Validation

`performance-harness-tests.yml` runs TUnit recorder tests, Python comparison and
affinity tests, and a real Linux child-affinity smoke.
Recorder tests independently verify counts, random interval histograms, exact
long tails, immutable prior intervals, bounded failure, JSON compatibility, zero
record/drain allocations, and the complete probe's report accounting.

These checks validate harness correctness. They are not performance acceptance
for any open product PR. New measurements retain their own product and harness
pins and must not be pooled with previous observer or affinity configurations.
