# PR #3083: Ubuntu A–B–A

Candidate `0e327370e005254b8f268f6797970638b86a3fb4`; main baseline `a48fafe4121350da7ad83fcdd238f0f8039d6d59`. [Completed Actions run](https://github.com/thomhurst/Dekaf/actions/runs/34145860025).

**Acceptance: INCONCLUSIVE.** Typed traversal is near the later main control; several unchanged controls drift about 9â€“13%.

Typed traversal is 64.573 us per 1,000-record operation versus 71.014/64.259 us. Epoch traversal is 64.172 us versus 70.803/64.513 us. ParseFetch control movement is +10.62% for one batch and +13.00% for 128 batches. Raw traversal control is much steadier, but it does not explain the movement in the other operations. Allocations are unchanged. No consistent product slowdown is isolated by this run, and no complete protected-metric acceptance is established.

The table below reports ns per benchmark operation, using each fixture's OperationsPerInvoke denominator. Shutdown and parser rows are whole operations/batches; sustained dispatch is per message. BDN iteration statistics are not message latency percentiles. CPU/message and long-run stability were not measured by this run. The existing performance gate remains blocking; no prior protected-metric finding is erased by a mean-time improvement.

See [raw provenance](raw/provenance.json), [complete statistics](raw/comparison.json), logs and before/candidate/control reports in [raw](raw). Fixture adapters and scope are documented in the [runner README](https://github.com/thomhurst/Dekaf/blob/06aa795796080ce6879139ac2a253d4f0b4266ea/.github/benchmarks/aba/README.md).

| Case | A1 ns | B ns | A2 ns | B/A1 | B/A2 | A drift | Allocated B: A1 / B / A2 |
|---|---:|---:|---:|---:|---:|---:|---:|
| None / BatchBoundBench / ConstructPollBatch / Batches=1 | 39.042 | 39.365 | 38.426 | +0.83% | +2.44% | -1.58% | 128 / 128 / 128 |
| None / BatchBoundBench / ConstructPollBatch / Batches=128 | 39.949 | 38.442 | 38.724 | -3.77% | -0.73% | -3.07% | 128 / 128 / 128 |
| None / BatchBoundBench / ParseFetch / Batches=1 | 147.440 | 154.013 | 163.095 | +4.46% | -5.57% | +10.62% | 0 / 0 / 0 |
| None / BatchBoundBench / ParseFetch / Batches=128 | 13294.201 | 13802.761 | 15022.252 | +3.83% | -8.12% | +13.00% | 0 / 0 / 0 |
| None / BatchReadBench / RawControl /  | 7407.344 | 7402.659 | 7387.108 | -0.06% | +0.21% | -0.27% | 72 / 72 / 72 |
| None / BatchReadBench / TypedEpoch /  | 70802.703 | 64171.840 | 64513.089 | -9.37% | -0.53% | -8.88% | 128 / 128 / 128 |
| None / BatchReadBench / Typed /  | 71013.695 | 64572.991 | 64258.716 | -9.07% | +0.49% | -9.51% | 128 / 128 / 128 |
