# Evidence replay validation corrections

The replay tools now reject different or duplicate loaded assembly identities across U1/T/U2, duplicate or incomplete fixture-binding inventories, and helper methods whose complete signatures differ from the captured methods. Failed primer validation leaves its output path available for a corrected retry.

Original changed tools remain byte-for-byte in each experiment's `measured/` directory. The historical `file-bindings.json` entries point to those copies and keep their original SHA-256 values. No raw measurements, result tables, acceptance tolerances or historical verdicts changed.

Validation uses six regression tests in `.github/scripts/test_pool_evidence_replay.py`. They exercise corrupted evidence and successful replays under `python -O`, so rejection does not depend on assertions. Running the same suite against the original tools reproduces 16 failing subcases with no unexpected errors; the corrected tools pass all six tests.

```text
python -O -m unittest discover -s .github/scripts -p test_pool_evidence_replay.py -v
```

The corrected tools also replay the retained real captures from runs 34191811875, 34194281968 and 34198015349. All nine generated JSON outputs compare equal to their published originals: three metric comparisons, three loaded-binding reports, two primer summaries and one helper summary. All 81 historical file hashes verify across the three report inventories (19, 25 and 37 files).

These are validation-tool corrections. Product source remains identical to `7a28f823bc8bc684a782cfdfd91c7e11882d6da2`. The latest pool recovery measurements and the unresolved loaded performance assessment remain separate; successful replay does not establish a performance PASS.
