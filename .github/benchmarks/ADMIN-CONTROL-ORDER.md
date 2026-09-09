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

## Periodic collection is not removable startup noise

Run 34294583224 records Gen2 collection near total workload seconds 100, 200,
300 and 400 in all six controlled captures. Every measured maximum occurs in
the interval containing the fourth collection. ConditionalWeakTable Enumerator
MoveNext compiles near warmup second 1.55 and again near total second 400.09;
the later compilation is a runtime transition, while full collection itself is
already recurring during warmup. These observations do not establish which part
of the interval causes each pause.

Do not choose a later 60-second measurement window merely to miss the next
collection. A longer-warmup experiment must also measure multiple collection
cycles, retain every pause and establish that compilation has stabilized.
Changing the number of retained histogram intervals changes observer heap size,
which must be recorded and assessed. The current hosted defaults remain 360
seconds of warmup and 60 seconds of measurement; no new hosted run follows this
diagnosis alone. A local diagnostic extends measurement to 300 seconds with the
same 360-second warmup to inspect later cycles; it cannot provide acceptance.
