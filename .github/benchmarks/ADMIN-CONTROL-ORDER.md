# Administrative control-order diagnosis

Full matrix 34294557003 retains zero measured JIT activity in all nine controlled
captures, but matching A1/A2 throughput drifts by -3.63%, -6.06% and -3.72%.
Candidate delete-call CPU exceeds A1 by 5.21% and A2 by 1.30%. Those losses and
control changes remain INCONCLUSIVE; the maxima and all samples are preserved.

The old order ran every A1 configuration, then every B configuration, then every
A2 configuration. With three or four workloads, matching controls were separated
by roughly 42–56 minutes even though each measured capture lasted one minute.
The retained interval rates also show within-capture change. This supports
testing shorter separation; it does not prove that time separation caused drift.

For the next distinct experiment, run A1, B and A2 consecutively for each control
configuration before advancing to the next configuration. Keep all cases, pinned
products, fixture inputs, build validation, runtime/JIT/GC settings, CPU affinity,
360-second elapsed workload warmup, 60-second measurement, prepared observer heap
and acceptance tolerances unchanged. Each phase remains a fresh process on one
ubuntu-latest VM. Record exact UTC capture start/end times and phase/product/case
identities in capture-order.json. Preserve raw histograms, maxima, runtime events,
binary/source archives and all control results. No post-hoc window selection.

Candidate-only cases still run after the controls. Their recorded thread-pool and
DNS continuation transitions are a separate unresolved question; changing control
order does not waive startup, correctness, changed-path or stability requirements.
The reorder alone is not a performance PASS or authorization for another run.

## Cached-transport DNS boundary

The same run records ClientDnsEndpointResolver.ResolveAsync continuations during
the candidate retry case. The fake connection pool returns in-memory responses,
but metadata recovery still resolves the fixture's localhost hostname through OS
DNS. This adds an external service and asynchronous completion timing to a probe
whose declared scope is cached transport.

Use the literal loopback address 127.0.0.1 consistently in each fixture's bootstrap,
broker/coordinator metadata and fake connection identity. The existing resolver's
literal-address branch returns synchronously without consulting DNS. Apply this
identical input to A1, B and A2, retain every protocol/correctness check, and record
the fixture adaptation in the plan. This characterizes administrative operations
with an already specified endpoint; it does not certify DNS discovery or recovery
performance. Thread-pool transitions from other sources still require assessment.

## Explicit fixture preflight

Description, member-removal and share-offset hosts now handle the same `validate`
command as the mutation host. Previously, that argument fell through to
BenchmarkDotNet's interactive selection and could return success without running
the requested fixture. Hosted admin_refresh.py validation used `probe`, including
InitializeAsync and the retained capture validator, so its completed results are
not invalidated by this local-preflight bug. Local preflight now requires the
explicit `Validated CASE` marker and rejects unsupported cases on both products.
Member-removal and share-offset dispatch also reject unknown candidate modes
instead of silently treating them as ordinary candidate calls.
