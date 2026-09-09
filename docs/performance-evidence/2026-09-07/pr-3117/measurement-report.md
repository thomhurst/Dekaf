# PR #3117 dispatch investigation

Published head d89815dfe0eab056eacfd4e1e1a0f85e5e212de3. Historical predecessor 9f9523cdc308b04ede8bd73371294e93fd4b06d6; accepted baseline 2575669b350a1a9a499a84707bfb43253b08ad7b. One shared performance reservation covers all preparation, pilots, full timing, profiles and validation. No competing .NET workload observed during measurement.

Candidate 1 replaces TryGetValue/Add with CollectionsMarshal.GetValueRefOrAddDefault, eliminating one dictionary probe on new lanes. Lane ownership is published before storage retention; cleanup tolerates an empty slot if allocation fails. Checked removal and mutable-key failure handling remain intact. A netstandard2.0 fallback retains the original implementation because the ref API is unavailable there. This fallback was added after measurement and does not change the compiled net10.0 method body; final binaries and patch have separate hashes. Final performance acceptance is not claimed.

Default unprofiled BDN, affinity 4, TieredCompilation=0; ns/message:

| Scenario | Published | Candidate 1 | Published control | Historical predecessor controls |
|---|---:|---:|---:|---:|
| Distinct records | 152.22 | 151.37 | 155.96 | 160.10 / 155.11 |
| Distinct batches | 151.37 | 149.59 | 156.29 | 156.34 / 153.78 |
| Paired records | 176.52 | 173.09 | 173.44 | 167.20 / 165.57 |
| Paired batches | 165.98 | 167.17 | 169.06 | 170.08 / 175.37 |

All candidate warmed probes are exactly 0 bytes and BDN reports 0 B/message. Accepted baseline controls remain 518–754 ns/message with 928–2440 B/message depending on scenario. The earlier large storage-reuse gain remains, but the remaining guard regression is not resolved.

Candidate 2 also removed a duplicate clear of PendingRecord.Record on successful completion. It is **rejected**, archived in `clear-once-v2-rejected.patch`, and reverted from the active source. In a separate fixed-iteration experiment (5 warmups/15 iterations/250ms), paired-record candidate 2 is 174.99 ns versus published 163.01/165.31 and candidate 1 162.69 ns. Paired batches are 175.53 versus published 169.53/171.80. Distinct-batch gains cannot compensate for those regressions.

Profiles are separate from unprofiled timing. Candidate 1's first paired-batch profile shows CPU 217.45 ns/message versus published 191.69/187.09, and max latency 781.8 us versus 190.6/301.6. Other cases improve or vary, including control tail spikes. These results prevent Pareto acceptance. `profile-metrics.json` and `profile-v2-metrics.json` preserve all absolute CPU, throughput, p50, p99, max and allocation metrics; 52 raw latency streams remain. They are synthetic dispatch measurements, not broker throughput or long-run stability.

116 measured cases total: 12 pilot, 28 default, 28 profile, 24 candidate-2 timing, 24 candidate-2 profile. Dry runs validate every scenario. Final candidate 1 passes 84 partitioned unit cases on each net10.0/net8.0 and six Kafka integration cases on net10.0. Tests cover mutable-key failure, detached completion observation, slot growth and borrowed storage lifetime. Initial netstandard build failure is preserved; final fallback validation passes. Diff checks pass.

Decision: candidate 1 retained locally as a provisional optimization; no merge, push or gate override. Candidate 2 rejected. No protected-metric tradeoff accepted. Results from different measurement configurations are retained separately.
