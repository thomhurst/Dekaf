# Outbox failure, lease-loss and loaded shutdown comparison

Use `suite=outbox-recovery` for PR #3085 or #3171 after their normal-path
comparisons. This extends the maintained Kafka workload and recorder; it adds
fault injection and correctness checks, not a benchmark engine or profiler.

Four independent jobs cover `legacy-failure` and `renewal-loss`, each with
listeners off/on for #3085. PR #3171 uses only two jobs, one for each failure mode
with listeners off, since that product does not introduce the metrics API.
Every job validates both products, then runs A1, B, A2
sequentially on one Ubuntu VM with exact pins and a fresh broker for each phase.
The same three-partition, 500-row, 1,000-byte payload batch is used throughout.
Each process has a 20-second primer, 180-second warmup and 180-second measurement.
The fixture requires a fault and recovery in every phase. The dry preflight uses
20/2/2 seconds and establishes correctness only.

Every 32nd publication attempt injects one failure. In `legacy-failure`, the
publisher returns zero acknowledgements and an error before sending that attempt.
In `renewal-loss`, the real publisher sends and receives all Kafka acknowledgements,
but its result is withheld until the renewal store rejects the next renewal.
Renewals are scheduled every 100 milliseconds; lease duration remains 90 seconds,
so this exercises explicit lease loss rather than lease expiry. Both modes use
10-millisecond error backoff. The same retained row instances are then retried;
no new logical batch begins before all 500 rows are acknowledged and deleted.

Completed throughput counts acknowledged rows deleted from the outbox, not failed
attempts or duplicate Kafka acknowledgements. A batch's latency starts at its first
fetch and ends at successful deletion, including failure, renewal and retry delay.
All 500 rows share that completion boundary. The existing raw cycle records,
process CPU/allocation counters, heap/RSS/GC/backlog series and broker counters
remain unchanged. Fault counts are retained per phase. No samples are removed.

Telemetry is checked against actual semantics: failed publications increment the
failure counter; successful Kafka acknowledgements remain acknowledgements even
when subsequent lease loss retains the rows. Replay acknowledgements are counted
again. They must not be mistaken for additional unique completed rows.

After the measured phase, a final 500-row Kafka batch is acknowledged while its
publisher completion remains blocked at the relay boundary. Stop begins with that
operation still in flight. Releasing it must let shutdown finish, observe the
publisher, retain exactly those 500 durable rows, and delete none of them. These
rows are outside the measured throughput denominator. Final pending rows are
therefore expected durable replay work, not an unbounded backlog. Shutdown
elapsed time and acknowledged/retained/in-flight counts are reported separately;
one shutdown observation per process is diagnostic, not a shutdown percentile.

Predeclared protected-metric tolerance remains 5% against both controls for
completed throughput, CPU per completed row and p50/p99 completion latency. Require
no new per-message allocation; identify existing payload and amortized batch costs
separately, using the focused MemoryDiagnoser evidence for the relay fast path.
Inspect all phase trends and failure/recovery counts for stability. Max latency
and the single final shutdown duration are diagnostic. The existing 30-second
shutdown correctness deadline remains enforced. A successful collection proves
coverage and correctness, not performance acceptance. Retain each raw screen and
apply the main agent instructions to the combined normal and recovery evidence.

For #3171, the candidate resolves its real notifier through public DI registration
and passes it to the relay. The synthetic store sends one notification when a new
logical batch becomes available, including the final shutdown batch. Retrying a
retained batch does not count as another commit. Repeated notifications remain
pending/coalesced during the busy relay and failure backoff. Recorded notification
counts must equal completed logical batches plus the one retained shutdown batch;
a missing/wrong notifier binding fails validation. The baseline uses its existing
relay constructor because it has no notification API. Both sides keep the same
10 ms polling override, payloads, faults, timing boundaries and work counts.
This controls recovery behavior independently of the requested default polling
tradeoff. EF transaction observation, idle discovery and normal per-row latency
belong to #3171's separate shared SQLite/Kafka experiment.
