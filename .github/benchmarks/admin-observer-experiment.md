# Administrative observer preparation

The member-removal pilot in run 34291207945 completed after two pre-workload,
blocking compacting full collections with finalizer waits. Its measured phases
had no JIT growth or delivered compilation events, no thread-pool growth/backlog,
and stable retained heap ranges. All protected point estimates met the existing
limits against both controls. This validates expanding the recorder experiment;
it does not establish full API performance acceptance or population tail bounds.

Use the same recorder for description, member-removal, share-offset and mutation
comparisons. Retain exact-tick histogram buckets as value types in preallocated
65,536-entry interval buffers. Capacity exhaustion invalidates the experiment.
Prepare observer storage before workload warmup, then execute one continuous loop
for 360 seconds of warmup and 60 seconds of measurement. Do not force collection
or serialize reports during either phase. Preserve all measured samples/maxima.

Run the existing control matrix sequentially as A1/B/A2 on one ubuntu-latest VM.
Build both exact products and validate every fixture before timing. New APIs with
no baseline equivalent receive explicitly candidate-only correctness/runtime
measurements; do not invent equivalent work on the baseline. The workflow allows
240 minutes for the full matrix, including candidate-only cases and validation.

The existing limits remain 3% for completed calls/s and process CPU/call, 5% for
actual call p50/p99/max latency, and one additional allocated byte/call. Apply the
same limits to control drift. Review uncertainty, GC/heap/RSS, CPU/latency trends,
JIT and thread-pool activity; point estimates alone are insufficient. These are
cached-transport administrative call measurements, not Kafka network or broker
acceptance. Lifecycle, cancellation, retry and new API correctness remain explicit
cases; local or hosted integration tests are reported separately.

New campaigns pin main b5a11ac3d0080874b5885aaab888bb3b9d88af77 and rebased exact
heads. Harness SDK/package pins now match that main. Record any later main movement
without silently changing revisions during a phase or transferring older-head
acceptance. The earlier pilot used A b19a9240e602ed70eb92ef515ded0eba7e42310a,
B e446a56586cd24938e064a6f4ba7febdbf5a892f and harness
6f7086e825c19ea684f81c726411a69c1f55f880; its conditional compatibility fix and later
dependency rebases require new head-specific evidence.
