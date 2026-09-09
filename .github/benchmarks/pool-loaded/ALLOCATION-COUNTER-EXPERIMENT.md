# Fixed-heap server-GC accounting experiment

The exact repeat 34292171029 of 34282852320 was not accepted. With the same
products/harness and 660-second warmup/300-second measurement, B and A2 reported
negative allocated-byte totals for the 1,000-byte/three-partition workload.
Their approximate per-second counters remained increasing. Negative allocation
is invalid evidence; those totals must not be clamped, substituted, or credited
as an improvement. Both runs independently have protected tail/control losses.
The repeat used Intel Xeon 6973P-C; the first used AMD EPYC 7763. Cross-run
absolute differences cannot be assigned solely to the product.

This matches the mechanism documented by
[dotnet/runtime #131069](https://github.com/dotnet/runtime/pull/131069): DATAS
can decommission server-GC heaps whose accumulated allocation counts were
excluded from the total. The precise API exposes decreases; the approximate API
masks them with a high-water mark. The old recording does not retain exact heap
transitions, so this is a supported hypothesis rather than a proved attribution.

For the next causal comparison, explicitly set DOTNET_GCDynamicAdaptationMode=0
for every smoke/A1/B/A2 process. Keep server GC, tiered compilation/PGO/ReadyToRun,
workloads, connection/batch/partition settings, warmup, sampling, durations,
profiling and tolerances unchanged. Record the setting in plan.json and each
client configuration. This tests fixed-heap server GC; it does not establish
equivalence under DATAS or silently replace the recorded default-GC results.

Preserve exact start/end CPU and allocation counters. Validate nonnegative
totals and recompute every throughput/CPU/allocation denominator before treating
collection as valid. Keep negative counters in raw reports if they recur, then
stop the comparison. Validation does not promote a gate. Compare all protected
metrics against both controls and retain every histogram, interval, maximum and
runtime/broker sample. Use fresh main and descendant candidate pins for any new
acceptance campaign. No third unchanged paid run is permitted after the two
inconclusive results above.
