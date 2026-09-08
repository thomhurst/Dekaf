# First manual-commit exchange diagnostic

Run two bounded four-second fixtures, once for retained baseline A and once for candidate B. Use exact hosted `Loaded-A`/`Loaded-B` binaries, constant baseline producer A, same fixed topic/group name, four partitions, 1,000 records/s, two seconds warmup and two seconds measured. Each gets a fresh Kafka 4.3.1 broker. There is no performance claim and no long comparison rerun.

Enable `kafka.request.logger=DEBUG` through the broker's dynamic logger API before each client starts. Preserve the logger update/describe output, all broker logs, request logs, client outputs, producer counters and final commit assertions. This instrumentation may perturb the race; passing runs do not explain the earlier failures. Do not discard or retry any client failure.

Record FindCoordinator and OffsetCommit requests/responses, group identities, API versions and broker timestamps. Check whether the baseline reproduces the same error and whether broker offsets-topic loading overlaps the failing exchange. No production retry policy, shutdown contract or correctness assertion changes. Remove only these task-owned containers after log capture.

Product pins remain A=5df2f0d03607389384b5c1466e17812a9084fac9 and B=2a550007ccb091e8f2bf9275a7646277e984dff8, fixture=7614406221b63c0b758f25e0722c8a3bf3ef9d0f. Main now includes tooling-only 9eec358dad2a081dedbbfc75f02aee743e6bdad9. This is diagnostic reuse, not fresh-main acceptance.
