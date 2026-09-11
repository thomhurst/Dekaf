# EF outbox stress workload

`outbox` is a manual Dekaf-only scenario using an isolated SQLite WAL database, the real EF Core outbox store and relay, an idempotent Kafka publisher, and a verifying Kafka consumer. The `outbox-1b` workflow lane is excluded from `lane=all` and `full_run=true`.

```powershell
dotnet run -c Release --project tools/Dekaf.StressTests -- --scenario outbox --client dekaf --brokers 1 --duration 5 --producer-warmup-seconds 180 --message-size 1000 --producer-delivery-diagnostics --output ./outbox-results
```

The duration is **total measured time**: 25% idle and 75% active. Five minutes means 75 measured idle seconds and 225 seconds of active admission, followed by complete drain. Each phase must finish its own duration; extra idle time or drain cannot replace active admission. Each phase also has a separate warmup of `producer_warmup_seconds`; active warmup retains the existing six drain/reuse cycles. Setup, warmups and final drain add to wall time.

Idle runs the real empty relay with no writer or Kafka reader. Boundary and approximately one-second observations retain CPU, managed allocations, heap, working set, collections, GC pauses, thread-pool activity and store counters. CPU is milliseconds per elapsed second; allocation is bytes per elapsed second. No synthetic messages enter either denominator. All samples, including CPU spikes, remain in JSON. Aggregate idle costs use the complete boundary deltas.

The writer commits batches of at most 32 records. Relay defaults remain eight buckets, 500 records per batch, one-second polling, 30-second leases and ten-second renewal. At most 4,096 sequence slots can be outstanding; admission stops when the next reusable slot is still missing. Payloads contain run identity, logical sequence and enqueue timestamp. The reader requires consecutive Kafka offsets within each partition and one unique completion per committed logical record. Logical records can arrive out of order across relay buckets. Repeated logical records at new Kafka offsets are counted separately as legitimate at-least-once duplicate publications. They do not inflate throughput or latency samples.

A transaction inserts a poison record and rolls back before active work. The database must then be empty; publishing the poison record fails the identity oracle. Every warmup cycle and measured active phase drains committed rows, successful store marks, producer delivery and Kafka end offsets. Store and publisher exceptions remain sticky failures even if the relay normally retries. The result returns only after reader and relay shutdown is observed. Success deletes only that invocation's database; failure retains it in the result directory.

Active metrics cover the complete writer/store/relay/publisher/reader process, including drain. These are end-to-end outbox costs. Payload creation, EF tracking and persistence allocate per message; observation adds work per store operation or runtime sample. Both product revisions receive the identical maintained harness. Producer diagnostics are mandatory. Existing latency sample floors, active steady-state checks and comparison tolerances remain unchanged.

For acceptance, dispatch one lane with `baseline_sha` set to fresh main contained in the candidate. The workflow overlays the candidate harness onto both revisions, performs one-minute correctness preflights, then measures baseline, candidate and baseline on one VM. Five-minute samples with 180-second warmups budget approximately 47 minutes across preflights and measurements, plus setup and drains. Idle coverage and steady-state CPU, allocation and memory trends are checked independently. The comparator adds idle CPU and allocation rows using the existing adverse and control-drift policy. Missing, contradictory or unsettled evidence is `INCONCLUSIVE`. Discarding a spike cannot hide an aggregate loss.

Root SDK/package inputs and baseline ancestry retain the existing workflow guards. Historical revisions with incompatible root inputs need a separately reviewed experiment. This infrastructure does not resolve the notification allocation overhead or historical idle CPU regression tracked by #3232.
