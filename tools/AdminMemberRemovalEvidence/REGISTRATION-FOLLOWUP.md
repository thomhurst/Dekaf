# Registration regression follow-up

The full hosted comparison [34294566300](https://github.com/thomhurst/Dekaf/actions/runs/34294566300) found a repeatable registration-1 p99 regression: A1/B/A2 621/901/622 ns, with six candidate ten-second blocks slower than every control block. Registration-32 CPU was 9220.958/9584.562/9118.774 ns per call. Product B was cab0b5c506fac7bdc2ca1f6e0c05199b047d869a; main A was b5a11ac3d0080874b5885aaab888bb3b9d88af77.

The candidate removes the redundant Distinct iterator/set before construction of the final HashSet for dynamic in-memory member registration. HashSet still uses the default comparer, deduplicates subscriptions and snapshots caller data. The unit test checks duplicate subscriptions and clearing the input list for both dynamic and static members.

A local Windows diagnostic compared the previous exact product binary against this source change, A1/B/A2, using identical fixture sources and 20 seconds warmup plus 10 seconds measurement. This is causal diagnostic evidence, not acceptance:

| Registration workload / metric | Previous A1 | Candidate | Previous A2 |
|---|---:|---:|---:|
| 1 member / CPU ns per call | 248.150 | 182.491 | 220.537 |
| 1 member / allocated B per call | 856.001 | 472.001 | 856.001 |
| 1 member / p99 ns | 800 | 400 | 500 |
| 1 member / max ns | 459500 | 688700 | 761900 |
| 32 members / CPU ns per call | 4366.188 | 3280.254 | 4425.263 |
| 32 members / allocated B per call | 22304.013 | 10016.010 | 22304.014 |
| 32 members / p99 ns | 7600 | 4500 | 9600 |
| 32 members / max ns | 497500 | 666800 | 1526400 |

The local maximum loses against A1 in both cases; control drift prevents acceptance. Raw captures, binary hashes and source patch are retained locally in registration-direct-set-diagnostic. They are not claimed to be uploaded artifacts.

## Predeclared hosted follow-up

Fresh main A is 551d4d0825dca64b7143d6f18fc2b13baaf1fe0a. Rebased candidate B is 57799ebd21dbecd3be8f27e4cd6a47bb21ccd8ec and contains A. Build and 110 focused unit tests pass after rebase. Exact workflow checkout supplies the harness SHA recorded in plan.json.

Keep all four controlled workloads and all 15 candidate-only cases. On one ubuntu-latest VM, run each workload's A1/B/A2 adjacently. The previous phase-grouped matrix separated corresponding controls by up to an hour. Use the already-validated literal-loopback fixture, explicit preflight correctness checks, workstation GC, tiered compilation/PGO and identical CPU affinity/recorders in every process.

Every fresh process receives 480 seconds continuous workload warmup and 180 seconds measurement. Prior measured helper/ConditionalWeakTable JIT occurred around total seconds 382-400. The extended capture includes multiple recurring 100-second GC cycles; it does not move measurement into a GC-free gap. Retain all samples, extrema, JIT events, thread-pool/GC/heap/RSS series and operation/CPU/allocation boundaries. Runtime transitions still affecting capture make the result INCONCLUSIVE.

The comparison criteria remain 3% throughput/CPU, 5% latency, and 1 B/call allocation allowance against each control, with the existing control-drift checks. Administrative allocation is per call, not per produced message. The full matrix takes about 297 minutes of warmup/measurement plus build, preflight and artifacts; the job timeout is 350 minutes. No unchanged third repeat or tolerance increase is authorized by this plan. The previous head's result remains a regression regardless of the follow-up outcome.
