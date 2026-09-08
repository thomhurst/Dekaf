# Top-level replay statistics validation

The top-level A1/B/A2 assessor now validates every emitted timing statistic against retained workload Result rows. It previously checked only original values and mean, then trusted standard error, confidence bounds and maximum. Eight independent corruptions reproduce the gap before this correction.

The maintained self-contained assessor uses the same explicit 25-sample, 99.9% Student t validation as the three observer replayers. It rejects inconsistent or nonfinite statistics before creating output. Historical measured source snapshots remain unchanged.

Validation: all nine test methods pass, including the eight new corruption cases. Replaying the original run 34189269388 with optimized Python verifies 80 host bindings and nine loaded bindings and reproduces assessment.json, loaded-bindings.json and samples.csv byte for byte. validation.json retains the output hashes and red/green/replay log hashes. The test command is `python -m unittest discover -s .github/scripts -p test_pool_evidence_replay.py`.

This corrects evidence validation only. Product source, raw results, tolerances and the INCONCLUSIVE performance verdict remain unchanged. It does not establish current-head performance acceptance.
