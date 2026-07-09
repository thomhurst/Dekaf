---
sidebar_position: 15
---

# 24-hour soak tests

The mixed soak runs a paced, idempotent `Acks.All` producer and a live consumer in the same process. CI targets 1,000 1 KB messages per second, enough to exercise producer, consumer, networking, batching, offset, and sequence bookkeeping without turning a shared public runner into a throughput benchmark.

GitHub Actions runs the scenario every Tuesday at 02:00 UTC against both one-broker and three-broker clusters. Public `ubuntu-latest` jobs have a six-hour hard limit, so each topology runs five independent 288-minute segments: 24 cumulative hours of weekly load and five separate post-warmup leak-slope gates. Each segment uploads its JSON, Markdown, and per-minute samples for 90 days.

The segments intentionally restart the process. A single continuous 24-hour process would require a paid or self-hosted runner; this layout keeps the repository on free public GitHub runners.

## Failure gates

Strict message checks remain active for the entire run:

- producer errors or consumer errors fail the run;
- client-accepted throughput must reach at least 95% of the requested pacing rate;
- broker end-offset growth must equal client-accepted messages;
- the consumer must catch up to every broker-delivered message after the producer drains.

After a 30-minute warmup, linear regression is calculated over every one-minute sample. The run fails when any slope crosses its limit:

| Metric | Limit |
|---|---:|
| Working set | +8 MiB/hour |
| Managed GC heap | +4 MiB/hour |
| Large object heap (LOH) | +4 MiB/hour |
| Produced throughput | -5%/hour |
| Consumed throughput | -5%/hour |

Each sample also records cumulative Gen2 collections and process thread count. LOH has no independent collection counter in .NET; LOH collections occur with Gen2, so the result records Gen2 cadence and LOH size separately.

At least 30 post-warmup samples are required. Missing samples fail closed instead of turning a broken monitor into a green soak.

## Run locally

Docker must be running. Full one-broker soak:

```powershell
dotnet run --project tools/Dekaf.StressTests --configuration Release -- `
  --scenario soak --client dekaf --brokers 1 --duration 1440 `
  --soak-messages-per-second 5000 --resource-sample-seconds 60 `
  --soak-warmup-minutes 60 --soak-minimum-samples 30 `
  --producer-delivery-diagnostics --output ./soak-results
```

Functional two-minute smoke (trend limits deliberately permissive; this verifies orchestration and result generation, not leak detection):

```powershell
dotnet run --project tools/Dekaf.StressTests --configuration Release -- `
  --scenario soak --client dekaf --brokers 1 --duration 2 `
  --soak-messages-per-second 100 --resource-sample-seconds 10 `
  --soak-warmup-minutes 0 --soak-minimum-samples 2 `
  --max-working-set-slope-mib-per-hour 100000 `
  --max-gc-heap-slope-mib-per-hour 100000 `
  --max-loh-slope-mib-per-hour 100000 `
  --max-throughput-decay-percent-per-hour 100000 `
  --output ./soak-smoke-results
```
