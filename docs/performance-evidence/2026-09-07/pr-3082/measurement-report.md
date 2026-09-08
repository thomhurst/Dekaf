# PR #3082 binary-key hashing investigation

Raw files referenced below remain in the [pinned evidence directory](https://github.com/thomhurst/Dekaf/tree/96a8694db9178379f8ee0b1844850bc971974116/docs/performance-evidence/2026-09-07/pr-3082/evidence), retained by branch `evidence/pr-3082-96a8694`. The historical report body and measured values below are unchanged.

Published baba5bcd7307d89741e2fc130f32df048f7545ff; prefeature baseline 602b8c76ac1e895fe8d897821f65bb62da47afde. Local candidate replaces TryGetValue/Add with CollectionsMarshal.GetValueRefOrAddDefault on modern .NET, removing one full-content hash on lane insertion. The existing gate protects the dictionary reference. Entry publication precedes storage retention; cleanup tolerates an empty entry after allocation failure. netstandard2.0 retains the original path. Content equality, XxHash3, storage lifetime and key-mutation checks are preserved.

One shared reservation covers preparation, both experiments and tests; ownership verified before and after timing. No competing .NET workloads or Kafka containers observed during timing. Windows 11/i7-12700K, SDK 10.0.400, runtime 10.0.11, BDN 0.15.8. Five jobs in each experiment: baseline → published → candidate → published → baseline. Five warmups, 15 iterations, 200 ms iteration target.

Initial default-tiering/all-CPU matrix: 45 measured cases, nine scenarios. Repeated binary-key and string-control candidate times fall within or near unchanged controls. Large distinct-key controls vary drastically: byte[] 38.826/23.363 us per record, ReadOnlyMemory<byte> 25.770/42.934 us. These results are inconclusive; the candidate's apparent large byte[] gain is not treated as causal. Raw JSON, logs and reports remain under timing/.

Focused experiment changes CPU affinity to mask 4 and sets DOTNET_TieredCompilation=0, using the same product inputs. Fifteen measured cases: 65,536-byte distinct keys plus unchanged string control. These results are separate from the initial configuration:

| Scenario | Published | Candidate | Published control | Old baseline controls |
|---|---:|---:|---:|---:|
| Distinct byte[] | 6.838 us | 5.775 us | 7.088 us | 1.283 / 1.294 us |
| Distinct ReadOnlyMemory<byte> | 7.300 us | 5.833 us | 6.830 us | 1.303 / 1.301 us |
| Repeated string control | 453.7 ns | 440.9 ns | 432.5 ns | 437.2 / 444.6 ns |

The large-key candidate improves approximately 15–20% against both published controls. The string result remains between controls. Distinct-key allocation is unchanged at approximately 2.13/2.22 KB per record; this old dispatcher allocates lanes and work items, and does not meet zero per-message allocation. The old reference-equality baseline does not scan key bytes and is still faster for distinct large keys, but it fails to group separately fetched equal binary keys. Full-content hashing retains that necessary cost. Prior sampled-hash experiments also exposed adversarial collisions; this candidate does not weaken the hash.

The controlled run's CSV, Markdown, HTML and full per-iteration log are under controlled/. Its invocation omitted the JSON exporter; no measurement was repeated solely to produce that format. summary.json contains the initial 45 cases; controlled/results/*.csv contains the other 15.

Validation: 80 partitioned/key unit cases pass on each net10.0/net8.0, and six Kafka key-comparer integration cases pass on net10.0. Both Dry matrices pass. Scoped simplification review and git diff --check pass.

Decision: retain the local single-probe patch as a measured partial improvement on modern .NET. Whole-PR Pareto acceptance remains unresolved: required full-key hashing still costs more than the incorrect baseline, existing dispatch allocations remain, and end-to-end CPU/tail/stability metrics are unproven. No push, merge or status override.
