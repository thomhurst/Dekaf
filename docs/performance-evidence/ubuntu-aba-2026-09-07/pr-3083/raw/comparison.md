# PR #3083: Ubuntu A–B–A

A: `a48fafe4121350da7ad83fcdd238f0f8039d6d59`; B: `0e327370e005254b8f268f6797970638b86a3fb4`.

Measurement completion is not performance acceptance. No automatic performance-gate override.

| Case | A1 ns | B ns | A2 ns | B/A1 | B/A2 | A drift | Allocated B: A1 / B / A2 |
|---|---:|---:|---:|---:|---:|---:|---:|
| None / BatchBoundBench / ConstructPollBatch / Batches=1 | 39.042 | 39.365 | 38.426 | +0.83% | +2.44% | -1.58% | 128 / 128 / 128 |
| None / BatchBoundBench / ConstructPollBatch / Batches=128 | 39.949 | 38.442 | 38.724 | -3.77% | -0.73% | -3.07% | 128 / 128 / 128 |
| None / BatchBoundBench / ParseFetch / Batches=1 | 147.440 | 154.013 | 163.095 | +4.46% | -5.57% | +10.62% | 0 / 0 / 0 |
| None / BatchBoundBench / ParseFetch / Batches=128 | 13294.201 | 13802.761 | 15022.252 | +3.83% | -8.12% | +13.00% | 0 / 0 / 0 |
| None / BatchReadBench / RawControl /  | 7407.344 | 7402.659 | 7387.108 | -0.06% | +0.21% | -0.27% | 72 / 72 / 72 |
| None / BatchReadBench / TypedEpoch /  | 70802.703 | 64171.840 | 64513.089 | -9.37% | -0.53% | -8.88% | 128 / 128 / 128 |
| None / BatchReadBench / Typed /  | 71013.695 | 64572.991 | 64258.716 | -9.07% | +0.49% | -9.51% | 128 / 128 / 128 |
