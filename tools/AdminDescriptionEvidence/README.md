# PR #3128 administrative performance experiment

This harness branch is separate from the product PR. Product SHAs are pinned before dispatch.

## Work and boundaries

One GitHub-hosted `ubuntu-latest` VM runs A1 (fresh main), B (PR head), A2 (the same main), sequentially. Both products build and validate before any timed phase. Fixtures are copied byte-for-byte, with only the documented `CANDIDATE` compilation symbol enabling APIs absent on main. No product source is adapted.

Controls: cached classic-consumer `DescribeConsumerGroupsAsync` and `InMemoryAdminClient.ListGroupsAsync`, each with 16 groups; and an in-memory administrative cycle that recreates offsets then deletes 16 groups (preparation included in its boundary). These operations retain identical inputs and observable results across all phases. New capability characterization (B only): 16 successful consumer groups; 15 Connect successes plus one authorization error; a transient coordinator move for one of 16 groups; malformed consumer assignments with negative collection counts; and one successful consumer group. The new API does not exist on A, so no equivalent before/after claim is made for those scenarios.

The synthetic connection returns prebuilt wire response objects. Broker/network latency and CPU are excluded. The boundary is public admin method invocation to complete result observation. No message delivery changes: per-message throughput/latency/allocation and producer/consumer sustained load are not applicable; the unit is a completed administrative call. Correctness against Kafka is covered separately by integration CI.

Each fresh probe and BDN process warms its actual configured workload for **30 seconds** before sampling. The probe then measures **60 seconds** at closed-loop concurrency one (next call starts after the previous call completes). There is no queue or dropped offered work. BDN measures 12 iterations at 500 ms nominal iteration time, one launch, after its explicit 30-second warmup and normal pilot/warmup. All phases use Release/net10.0, workstation GC, tiered JIT and dynamic PGO enabled. No profiling agent is attached.

Probe latency histograms retain every completed call's exact Stopwatch tick count, including maxima; no tail clipping, discarded startup samples, percentile averaging, or sampling is permitted. Histograms preserve the distribution, while one-second rows preserve temporal CPU, throughput, p50/p99/max, GC, heap/RSS, JIT method/time, thread-pool threads/pending work, and completion counts. CPU/allocation scope is the whole probe process, including histogram/counter bookkeeping. MemoryDiagnoser independently records inclusive managed allocation per administrative call. No zero-byte per-message claim is made for allocated admin result objects.

## Criteria declared before hosted measurement

Correctness requires all expected results and zero unexpected exceptions, timeouts, or leftover asynchronous work. Candidate control throughput must be no more than 3% below **each** baseline; CPU/call no more than 3% higher; p50/p99/max latency no more than 5% higher; allocation/call must not increase beyond a 1-byte measurement allowance. A1/A2 control drift must meet those same bounds. All measurements and absolute deltas are retained even when inconclusive. BDN confidence bounds and per-second sample counts must be reported; overlapping bounds alone cannot prove equivalence.

Warmup is insufficient if the final five warmup seconds or measured series retain material JIT/thread-pool startup transitions or systematic CPU/latency drift. Such cases are INCONCLUSIVE and require a revised experiment across every phase. Heap/RSS/GC series must show bounded repeated-call behavior for this measured interval; this does not establish long-run leak absence. Candidate-only cases characterize cost and correctness, not a Pareto comparison against a nonexistent baseline.

A full PASS requires all applicable control criteria and credible startup/stability evidence. REGRESSION requires a confirmed protected-metric loss; control noise, missing data or insufficient precision remain INCONCLUSIVE. The evaluator emits measurements and a provisional control assessment only; it cannot update a GitHub gate or approve a merge. One exact repeat is the maximum automatic repeat after INCONCLUSIVE, then experiment changes or maintainer direction are required.

Retain product/harness SHAs, SDK/runtime, runner image/hardware, all sources, binaries, build/validation/BDN logs, histograms, time series and SHA-256 inventory in the uploaded artifact and durable PR archive.
