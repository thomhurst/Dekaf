# Stress runner

For the manual EF outbox workload, see [idle and active outbox coverage](Outbox.md). For public key-ordered processing, see [keyed consumer replay](KeyedConsumer.md).

`hosted-share` is an explicit manual scenario. It runs an idempotent live producer and two instances of the same hosted share service under distinct DI keys in one Kafka share group. The `hosted-share-1b` workflow lane is excluded from both `lane=all` and `full_run=true`; the existing full matrix remains 12 jobs.

```powershell
dotnet run -c Release --project tools/Dekaf.StressTests -- --scenario hosted-share --client dekaf --duration 5 --producer-warmup-seconds 180
```

The scenario requires Kafka with share groups enabled. Its single-broker Testcontainers environment configures the share coordinator automatically. When using `KAFKA_BOOTSTRAP_SERVERS`, configure the external broker yourself. The scenario requires one broker, one producer connection, no compression, and messages of at least 16 bytes. It changes only its unique group's `share.auto.offset.reset` to `earliest`.

The feeder reuses its serialized payload and permits at most 16,384 outstanding sequence slots. A missing record prevents that slot's reuse. Both workers complete records synchronously with `ValueTask`, and the harness counts each sequence once across redelivery and worker races. Persistent failure state reports polling, processing, acknowledgement and producer-delivery failures. Every phase stops ingress, flushes the producer and waits up to 30 seconds for every produced record to finish processing. Shutdown stops both services, observes their execution tasks and awaits asynchronous disposal.

Warmup runs the same workload and observers through the existing six-cycle scheduler. The measured phase records unique processing throughput, admission-to-processing latency for every unique record, process CPU, process allocations, runtime observations and throughput stability. Producer request diagnostics reset at each phase and are retained in the result for the existing A/B/A diagnostics contract; workflow runs enable full producer delivery diagnostics. CPU and allocation figures include the live producer, both consumers, serialization and measurement overhead. These figures cannot be compared to the pre-seeded consumer replay lanes. A processing completion is not a broker-confirmed acknowledgement; final service shutdown submits acknowledgements, and any callback failure invalidates the run. Shutdown is validated outside the measured phase.

Use `baseline_sha` for acceptance. The workflow overlays the candidate stress harness onto both product revisions, then runs baseline, candidate and baseline on one VM. Existing warmup, stability and performance gates apply unchanged. Every product revision must support the hosted-share APIs and pass this workload. A baseline that fails cannot supply a valid comparison. This fast-handler lane exercises normal polling, acknowledgement and shutdown under sustained load; delayed-handler renewal and fault routing retain their dedicated unit/integration coverage.
