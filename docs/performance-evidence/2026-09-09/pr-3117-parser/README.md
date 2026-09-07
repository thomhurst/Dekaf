# Strict allocation-record validation

The maintained parser rejects trailing data on allocation records and rejects an extra malformed allocation record after a valid one. Previously, a single allocation record with trailing data was accepted. The retained red log records that failure; all nine parser scenarios pass after the fix. The valid input still reproduces the historical CSV exactly.

The test script constructs each malformed input deterministically from the retained original benchmark log. Run `pwsh -File ../../2026-09-07/pr-3117/permanent-dispatch-coverage/test-summarize-run.ps1` from this directory. Its default output is an ignored `.artifacts` folder beside the test script.

Only evidence parsing changes. Product source, fixture source, and historical measured samples are unchanged. This does not grant performance acceptance. The in-flight comparison 34333339531 measures historical head 4e516d176d488fdc9e2420b4db1b26cdcf74ce38 against 551d4d0825dca64b7143d6f18fc2b13baaf1fe0a; main and the PR have since been rebased.

`inventory.json` contains SHA-256 digests of the two raw validation logs.
