# Subscribed hosted-share stress

`hosted-share-telemetry` runs the existing hosted-share live producer and two independent keyed workers with real broker telemetry subscriptions. Select `hosted-share-telemetry-1b` explicitly; it is Dekaf-only and excluded from `lane=all`, `full_run=true` and the runner's `--scenario all`.

```powershell
dotnet run -c Release --project tools/Dekaf.StressTests -- --scenario hosted-share-telemetry --client dekaf --duration 1 --producer-warmup-seconds 20 --producer-delivery-diagnostics --output ./results
```

Docker builds the [shared receiver](../Shared/TelemetryReceiver/README.md) against the stress runner's Kafka image. Unset `KAFKA_BOOTSTRAP_SERVERS`: this lane owns its broker and receiver. The host needs Docker and the repository SDK, not Java. Each worker gets an exact `client_id` ClientMetrics subscription for `org.apache.kafka.consumer.share.` and application gauge `com.example.dekaf.stress.worker`, with a 1,000 ms push interval. Before constructing workers, the harness probes GetTelemetrySubscriptions with fresh instance IDs until the broker reports both requested prefixes and the configured interval.

The workload retains six warmup/drain cycles, the 16,384-record outstanding window, default poll limits, normal acknowledgements, complete drain, correctness checks and shutdown. It adds no per-record observer callback. Application gauges are registered once per worker and read per export. Client export/accounting CPU and allocations remain inside the measured process. The broker copies and Base64-encodes each export under a short receiver lock; these broker costs are outside the client CPU/allocation counters but can affect end-to-end throughput and latency. All revisions use the same receiver and subscriptions.

The receiver retains at most 64 exports, each at most 65,536 decoded bytes. HTTP polling, file writes and OTLP decoding occur after warmup and after asynchronous worker disposal, outside measurement. Each retained payload must identify its worker and contain its application gauge. Periodic evidence must show positive built-in fetch, consumed-record and acknowledgement sums for both workers; acknowledgement error metrics invalidate evidence. Each worker must produce exactly one retained terminating export including fetch accounting. `shareTelemetry` records dimensions, worker IDs, retained byte/count totals and progress checks. These totals describe the bounded final history, not all exports since startup. Raw observations are `telemetry-warmup.tsv` and `telemetry-final.tsv`; failed workloads attempt to retain `telemetry-failure.tsv` without masking the original failure.

The exact-SHA workflow overlays the candidate stress runner and `tools/Shared` into both product revisions. Runtime coverage and A1/B/A2 validation reject missing/incomplete telemetry and mismatched subscription dimensions. Existing throughput, latency, CPU/message, allocation/message and stability thresholds remain unchanged. No paid run is necessary merely to land this harness.

Once both product revisions can complete this workload, use one relevant acceptance run, at most 15 minutes per sample:

```powershell
gh workflow run stress-tests.yml --ref <candidate-branch> -f lane=hosted-share-telemetry-1b -f baseline_sha=<fresh-main-40-character-SHA-contained-in-candidate> -f duration_minutes=5 -f producer_warmup_seconds=180 -f dispatch_shape=cheap
```

Record the run URL, lane, shape, duration and verdict in the product PR. A baseline that fails acquisition, drain, telemetry or shutdown supplies no valid comparison. Preserve that failure; do not increase MaxPollRecords, reduce the outstanding window or disable checks to obtain acceptance. Follow the repository's stress policy for PASS, REGRESSION and INCONCLUSIVE. This lane supplies tooling for #3033; it does not itself establish loaded product acceptance or resolve acquisition work in #3248.
