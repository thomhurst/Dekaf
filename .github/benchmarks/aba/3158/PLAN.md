# ShareFetch response ownership microbenchmarks

Compare fresh-main A with the exact #3158 candidate, on one ubuntu-latest VM in
A1/B/A2 order. Use the common fixed-fixture runner: Release, MemoryDiagnoser,
workstation GC, tiered compilation disabled, 50 elapsed-verified workload warmup
iterations (minimum 20 seconds), 25 retained actual samples per fresh process.
Report exact runtime/image/hardware, source/binary hashes and runtime time series.

Six cases cover empty, 32-KiB and 64-KiB record fields in a valid ShareFetch v1
response. Stable-frame decode uses identical externally-owned storage that cannot
be invalidated by returning a pool rental; it isolates protocol decode and owner
bookkeeping. Pooled-frame decode additionally includes an identical input copy
and managed buffer rental/return. It is strictly sequential, with no subsequent
rental before the record field is checked. These fixtures avoid the baseline's
known native-frame correctness failure without modifying either product.

The record field is opaque at this protocol boundary; this is response decode,
not per-message deserialization or Kafka delivery. Report ns/response and
MemoryDiagnoser B/response. Topic/partition response-object allocations are
amortized per response; do not call them per-message allocations. Do not infer
end-to-end CPU, latency or stability from these measurements. Compare each mean
and confidence bounds against both controls with the common 3% time/control
limit, retaining allocation changes separately. Any unexplained allocation
increase or failed time/control limit prevents acceptance of this scope.

The candidate's public IDisposable is called after reading the response; A has
no disposal API. Native-frame delivery remains covered by regression tests that
fail on A and pass on B. Its baseline-relative performance is unmeasurable until
the baseline is corrected. This microbenchmark supplies focused evidence for
the shared repair; it does not certify full PR performance acceptance.
