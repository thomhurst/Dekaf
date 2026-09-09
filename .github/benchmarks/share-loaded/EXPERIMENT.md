# Share-consumer completed acknowledgement comparison

Compare the existing record API on both products, then compare the candidate's
borrowed batch API with the baseline record API performing the same complete
work. Use one Kafka 4.3.1 partition, integer keys, 256-byte payloads, explicit
acknowledgements, MaxPollRecords 128, and an explicit commit every 128 processed
records. Configure the group to begin at the earliest offset before producing.
The producer is always the pinned baseline in a separate process. Run 5,120
offered messages/s for 210 seconds of warmup and 180 seconds of measurement per
fresh process; 128-message offered bursts and commit boundaries align across
both phases. The warmup budget includes share-group join time; first-to-first
measured acknowledgement must establish at least 180 seconds of actual loaded
warmup. All actual warmup time and operations are
recorded. This initial experiment assesses runtime transitions before expansion.

A completed message is a successful broker acknowledgement reported through
ShareAcknowledgementCommitCallback, not a yielded record or queued ack. Latency
starts at scheduled producer offer and ends at that successful callback. Record
every latency, maximum, completion count and duplicate/failure; verify each
payload's sequence, partition and offset. CPU/allocation scope is the whole
consumer process, including the identical observer. Broker and producer are
separate. Preserve one-second JIT, thread-pool, CPU, GC, heap/RSS, backlog and
completion trends. Final CloseAsync must complete with every produced record
processed and acknowledged. Post-drain close latency is separate from shutdown
under ingress and is not presented as that measurement.

Build and validate both products before A1/B/A2 on one ubuntu-latest VM. Reset
broker, topic, group, process and workload state consistently. Pin product and
harness SHAs, runner/runtime and physical-core assignments. Retain all samples,
raw binaries and hashes. Limits are 3% throughput/CPU, 5% actual p50/p99/max
latency and one additional allocated byte/message against each control. Control
drift uses the same limits. Require exact correctness and no leftover work;
unresolved transitions, drift, insufficient precision or missing scope remain
INCONCLUSIVE. Point estimates alone do not produce PASS.

This adds loaded evidence to the parsing microbenchmarks. It does not cover
implicit acknowledgement, release/reject/renewal or shutdown with unfinished
records. Existing correctness tests cover those contracts; their performance
scope still requires explicit assessment. No baseline adapter exposes borrowed
memory or changes the work being acknowledged. Candidate batches and baseline
records both read each payload once and acknowledge each record once.
