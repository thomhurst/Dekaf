# PR #3117 dispatch comparison, 2026-09-08

This task-scoped harness belongs on its comparison branch only. Do not merge its replacement `benchmarks.yml` into the product branch or main. Scheduled benchmarks and stress coverage on main remain unchanged.

## Pinned comparison

Baseline A and candidate B are exact dispatch inputs, recorded in provenance.json. Fetch and pin fresh main, and verify B contains it before dispatch. The workflow records its separate harness SHA, runner image/hardware, SDK/runtime and run URL. Archive and build both exact product revisions before timing. Assert the loaded `Dekaf.dll` and `Dekaf.Abstractions.dll` hashes against each product build. Preserve source archives, complete loaded binaries, raw logs, latency arrays and runtime samples.

One `ubuntu-latest` job runs A1, then B, then A2 sequentially. Each phase includes six fresh-process microbenchmarks, four fresh-process Kafka workloads, and four fresh-process dispatcher shutdown workloads. Before A1, smoke every configuration against both products. Each loaded phase starts a fresh Kafka 4.3.1 container, with separate new topics in a fixed mode order. Detect physical-core topology and keep sibling threads together: consumer owns one physical core; baseline producer and broker use the remaining cores. Record exact affinity. Stop the broker before focused shutdown probes. Stop build servers before validation/timing. No unrelated builds, tests or diagnostics run during timing.

## Microbenchmarks

Use the permanent typed-record fixture's repeated/distinct/pending-pair inputs at batch sizes 1 and 16, capacity 128, concurrency 2, and 262,144 records per partition lifetime. The comparison fixture invokes the same `CreateBatchProcessor` factory on both products so both revisions own handler dispatch, automatic completion and storage release. This replaces direct construction with the candidate-only `automaticCompletion` constructor argument. The candidate must allocate exactly zero bytes in the steady dispatch probe; the baseline records its allocation without the zero assertion. Both validate record count, key order, probe thread, final commit frontier, and actual 16-record pending batches. The pending source completes inline through a reusable `IValueTaskSource`.

One BDN operation is an entire partition lifetime. Report whole-lifetime allocation and its amortized per-record cost separately from the exact steady allocation probe. Do not call rounded whole-lifetime BDN allocation a proof of zero allocation per message.

Every fresh process performs at least 20 seconds of this actual workload before BDN sampling, followed by 30 workload warmup iterations targeting one second each and 25 measured iterations. Verify elapsed warmup, operation counts, and iteration count from raw logs. Use `[MemoryDiagnoser]`, Release, .NET selected by global.json, InProcessEmit, workstation GC and `DOTNET_TieredCompilation=0`. Keep `DontRemove` and all raw samples/maxima. The runtime logger samples JIT compilation count/time, thread-pool size/backlog, process CPU, allocations, GC counts, heap and RSS at each BDN workload boundary. Warmup reports the same counters approximately once per second. These boundary samples are not per-message CPU or latency measurements.

## Loaded workloads

Four modes: synchronous record and batch handlers at 50,000 offered records/s; pending record and batch handlers at 1,000 offered records/s. Every mode uses four partitions, 256-byte payloads, capacity 128, concurrency 2, and maximum batch size 16. Synchronous modes cycle 1,024 integer keys. Pending modes use two keys per partition in groups of 32 records, allowing queued batches of 16; assert this coverage. Producer offers bursts of 128 against an unchanged monotonic schedule, preserving all pacing debt. The producer is always the baseline product in a separate process.

Predeclare 121 seconds of offered warmup and 120 seconds of measured offered load per fresh process. Require at least 120 seconds between the first handler completion and the first measured completion. Record completed warmup operations. Use tiered compilation enabled, workstation GC, and `DOTNET_GCDynamicAdaptationMode=0`, identically in all phases. The longer warmup addresses the preceding experiment's missing startup evidence; its sufficiency remains subject to measured JIT/thread-pool/CPU/latency/GC trends.

Pending handlers await `Task.Delay(1)` once per invocation. This intentionally models pending asynchronous work and includes the timer/state-machine allocation in the consumer process scope. It differs from the reusable-source isolated microbenchmark; do not attribute all loaded allocation to Dekaf. Record all handler batch sizes and measured invocation counts, and report process allocation per completed message and per handler invocation.

Latency begins at scheduled producer offer and ends when handler processing completes, after the pending delay when present. It includes producer pacing debt, broker time and consumer queueing, but excludes automatic frontier bookkeeping after handler return. Save every individual warmup and measured latency before sorting, including maxima. Derive per-second completion counts and latency percentiles from raw scheduled timestamps plus latency; retain any boundary buckets outside the nominal interval. CPU/allocation measurement begins with the first measured completion and ends with the final completion. Its scope is the consumer process including the sampler and handler simulation. Producer/broker CPU is separate. Throughput is completed throughput at the declared rate, not maximum consumer capacity.

Capture one-second JIT count/time, thread-pool growth/backlog, process CPU, allocated bytes, GC counts, heap/RSS, completed counts, scheduled backlog and pending-handler counts. Retain all samples. Check producer acknowledgements, no duplicates/loss/key reordering, complete final drain, no pending handlers, and broker-confirmed committed offset for every partition. Shutdown timing starts after all handler processing completes; it checks final bookkeeping/commit completion and does **not** measure shutdown under load. This two-minute window does not establish long-run leak absence.

## Focused loaded dispatcher shutdown

After each Kafka phase, use the same actual CreateBatchProcessor factory and
PartitionLane shutdown implementation in fresh processes. Cover batch sizes 1
and 16, with one and two keys. Fill 128 records before starting the dispatcher.
Hold handler completion, then wait on the actual lane buffered count until every
record reaches the coordinator. An UnsafeAccessor reads the same private count
on both pinned products; this is a fixture observation, not a product change.
It prevents the first-handler signal from racing batch formation. Stop begins
with pending handlers and all 128 records still unfinished. Request Drain,
release handlers, and await stop. Verify every record exactly once, key order,
zero pending handlers, the configured maximum batch size, and automatic commit
frontier 128 with leader epoch 7 on every stop.

Warm this complete lifecycle for 120 seconds and measure for 60 seconds, using
one continuous loop across the boundary. Record exact-tick histograms for every
shutdown and every message, including maxima. Message latency starts at queue
fill and ends at handler completion; shutdown latency starts at Drain and ends
when it returns. CPU/allocation and completed throughput cover the whole process
and lifecycle, including fixture synchronization and histogram accounting.
These cold lifecycle allocations are separate from the zero-allocation steady
dispatch probe. Preserve per-second CPU, allocation, mean/max latency, GC, JIT,
thread-pool, heap and RSS trends, plus method-level JIT events. Serialize only
after both captures finish.

This isolates shutdown with unfinished dispatcher work. It does not simulate
Kafka transport; broker-confirmed committed progress remains covered by the
separate public Kafka workload. Neither scope substitutes for the other.
Apply the same 3% throughput/CPU, 5% message and shutdown p50/p99/max, and
1 B/message allocation limits against both controls, including control drift.
Remaining startup transitions, insufficient precision or scope remain INCONCLUSIVE.

## Predeclared decision rules

For each microbenchmark, compare B against both controls: no more than 1% mean-time loss; baseline control drift at most 2%. Require the candidate exact steady allocation probe to remain zero. Report allocation per lifetime and per message, all measured distributions, uncertainty and sample counts. Confidence-interval overlap alone does not establish equivalence.

For each loaded mode, require completed throughput and CPU/message within 3% against both controls; actual message p50/p99/max latency within 5%; process allocation no greater than either control by more than 1 B/message. Throughput/CPU control drift must be within 3%, latency drift within 5%, and allocation drift within the greater of 1 B/message or 3%. These are measurement tolerances, not permission to exchange protected metrics. Require exact correctness, zero final backlog/pending work, complete raw samples, and assess time-series stability without trimming startup or maximum samples. Any unresolved startup transitions, material control drift, insufficient precision or applicable unmeasured metric make acceptance INCONCLUSIVE. Confirmed protected-metric loss is REGRESSION; unrelated gains do not offset it. No automated script changes the PR gate to PASS.

The previous hosted run `34150400216` used candidate `4d904967f9624bf58186ba1cf23d91275ec14ad9`, baseline `c212c575528e055fe628568e19fb521fc54b0ab9`, and harness `16840b679852b2ad99e4d4bef7d55d59e46f5f55`. It was INCONCLUSIVE: BDN warmup only 1.88–2.66 seconds, loaded JIT/thread-pool counters missing, pending-handler completion missing, and p99/max control drift substantial. Batch p99/max were 28.06%/57.59% worse than A1 but better than A2. CPU and allocation gains did not waive those losses. This corrected-head campaign changes fixtures, warmup and scope, so cross-run absolute values are diagnostic context, not like-for-like acceptance controls. This is not an identical repeat.

This campaign supplies current-head normal/pending dispatch evidence. Loaded shutdown/error-path performance and broader long-run stability remain explicitly unproven. Do not claim full PR acceptance or merge solely because this workflow succeeds. Any later supplemental campaign must retain the pinned baseline or explicitly record main movement and the need for fresh-head evidence.

## First hosted preflight and driver correction

Run 34175751899 stopped in the first baseline smoke case before A1 measurement. The driver accessed the measured-iteration variable outside its non-smoke branch. Regression coverage now tests smoke acceptance, complete/incomplete measured iterations, and an earlier incomplete case followed by a complete final case. All 12 validator/driver tests pass after moving the guard into its intended block. Product pins, C# fixtures, workloads, warmup, sampling and tolerances are unchanged; this correction does not repeat a measured experiment.

That failed run's artifact inventory lists 6,273 files. The durable archive verifies 6,267, including all loaded binaries; 101 tracked hidden files were recovered byte-for-byte from the preserved product source ZIPs. Six generated hidden `.NETCoreApp,Version=v10.0.AssemblyAttributes.cs` files were omitted by upload-artifact's default hidden-file exclusion and are explicitly not claimed as retained. The corrected workflow enables `include-hidden-files: true`. Raw failure logs, the original inventory and a detailed retention audit remain at `C:/git/Dekaf-evidence/pr-3117/hosted-current-20260908/run-34175751899/`.
