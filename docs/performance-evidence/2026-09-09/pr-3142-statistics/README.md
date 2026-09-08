# Observer replay statistics validation

The three maintained `compare-observer.py.txt` replayers previously checked sample counts but trusted the report's `Statistics` block. A stale mean, standard error, maximum or confidence interval could therefore change the derived observer comparison while the actual result samples remained intact.

They now require 25 workload Result rows with finite elapsed values and positive operation counts, cross-check every `OriginalValues` entry, and recompute mean, sample standard error, maximum and the 99.9% confidence interval. The supported sample count and confidence level are explicit. The critical value is Student t with 24 degrees of freedom at 0.9995, following [Perfolizer's interval estimator](https://github.com/AndreyAkinshin/perfolizer/blob/v0.6.4/src/Perfolizer/Perfolizer/Mathematics/Common/ConfidenceIntervalEstimator.cs). A mismatch fails before creating analysis output. Validated serialized values retain their original floating-point representation so valid historical JSON outputs do not change.

Validation:

- The updated tests fail in all 27 corruption cases before the fix: nine independent corruptions across three replayers. The old synthetic “valid” fixture was itself inconsistent; its Result rows and summary now describe the same constant measurements.
- All seven test methods pass after the fix, including those 27 corruption cases, using optimized Python (`-O`) subprocesses.
- Six real replay commands reproduce all nine previously published JSON outputs exactly, including nonconstant measurement samples and confidence intervals. All 81 historical inventory hashes still verify.

Run `python -m unittest discover -s .github/scripts -p test_pool_evidence_replay.py` from the repository root. The three maintained scripts remain self-contained for historical replay. Recorded copies inside `measured/` remain immutable source snapshots; the corrected scripts at each experiment root are the maintained entry points.

This changes only evidence validation and tests. It does not rerun measurements, alter raw samples, change any tolerance, resolve missing loaded metrics or convert the performance verdict to PASS. The product implementation remains the one in parent head `7061db2088d3d655f5e12c9eb019bd6341194069`.
