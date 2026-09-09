# Full administrative control follow-up

This plan applies to #3128, #3136 and #3138 after their first full 360/60-second matrix. Those reports remain INCONCLUSIVE; no earlier result is relabeled:

| PR / previous run | Finding requiring a different experiment |
|---|---|
| #3128 / 34294557003 | Delete CPU +5.21%/+1.30% with 3.86% control drift; inventory controls drift 6.06% in throughput. All controlled captures have zero JIT, but a control's within-capture rate also falls about 5%. |
| #3136 / 34294574792 | Legacy costs mostly improve; legacy-32 maximum controls drift 45.74%. Inventory CPU +3.09%/-1.34% with 4.50% control drift. Candidate-only helpers compile after warmup. |
| #3138 / 34294583224 | Mutation controls have adverse point estimates and substantial drift. Every controlled maximum coincides with recurring Gen2 around total second 401; ConditionalWeakTable JIT occurs around 400.09 seconds. |

The separate extended local #3138 baseline capture confirms Gen2 around total seconds 100, 200, 300, 400, 500 and 600. With 360 seconds warmup and 300 seconds collection, its measured JIT count is zero but maximum is 17.6635 ms. These are recurring collections, not removable startup samples. Local values are diagnostic and do not establish equivalence on hosted runners.

Fresh main A is 551d4d0825dca64b7143d6f18fc2b13baaf1fe0a. Each candidate is rebased onto A:

| PR | Exact B |
|---|---|
| #3128 | 800a02ebf92f96a08e5d3a3a8527a69cd1048548 |
| #3136 | 4bc8b71b2f9c43dc6120f16ebdd980fa3bc943e2 |
| #3138 | 546a7df7d5e5a2abe38f73795ceee5040be3fc4d |

Keep each full workload matrix and all correctness validations. Run each workload's A1/B/A2 adjacently on one ubuntu-latest VM. The previous phase-grouped matrix separated corresponding controls by up to an hour. Use identical literal-loopback fixtures, CPU affinity, workstation GC, tiered compilation/PGO, SDK/runtime, Release binaries and observers in all phases. Record exact harness SHA, runner hardware/image, product and binary hashes.

Predeclare 480 seconds continuous workload warmup and 180 seconds collection for every fresh process. This exercises the previously observed helper/ConditionalWeakTable transition before collection while retaining multiple recurring 100-second GC cycles during measurement. It does not select a GC-free window. All samples, maxima, compilation events, GC/thread-pool/CPU/latency/heap/RSS trends and accounting boundaries remain retained. Ongoing startup effects, material control drift or inadequate precision remain INCONCLUSIVE.

Keep the original 3% throughput/CPU, 5% latency and 1 B/call allocation allowance, plus control-drift checks. No tolerance changes or source performance wins are inferred from rebasing. These fixtures measure completed administrative calls and do not claim zero allocation per produced message or end-to-end network stability. A successful workflow does not pass a performance gate.
