# Attribute loaded runtime transitions before another acceptance experiment

Hosted runs 34287951725 (#3117) and 34294127203 (#3083) retain measured JIT
counter growth after 120 seconds of completed workload warmup. The public Kafka
executable lacked method attribution. Some maximum-latency intervals overlap
counter growth; others do not. One-second overlap is not proof of causality.

The public executable now records the same bounded CompilationLog used by the
other fixtures, with initialize, warmup, measured, drain and finalize markers.
The exact runtime accounting timestamps remain authoritative for selecting
events. Event delivery timestamps are diagnostic and may lag compilation.
Record loss/overflow must be reported. The observer is identical for A1/B/A2 and
does not remove samples, change load, alter runtime settings, or extend warmup.
Source snapshots and the observer remain part of the exact harness identity.

This instrumentation alone does not remove startup bias or establish acceptance.
First identify late methods and distinguish periodic maintenance from startup.
Design a causal follow-up with predeclared settings only after that review;
preserve the existing loss/control-drift verdicts. No additional acceptance run
has been dispatched for this diagnostic change; successful fixture validation
does not supply performance acceptance evidence.
