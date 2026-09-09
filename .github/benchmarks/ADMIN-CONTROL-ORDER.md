# Administrative control-order diagnosis

Full matrix 34294557003 retains zero measured JIT activity in all nine controlled
captures, but matching A1/A2 throughput drifts by -3.63%, -6.06% and -3.72%.
Candidate delete-call CPU exceeds A1 by 5.21% and A2 by 1.30%. Those losses and
control changes remain INCONCLUSIVE; the maxima and all samples are preserved.

The old order ran every A1 configuration, then every B configuration, then every
A2 configuration. With three or four workloads, matching controls were separated
by roughly 42–56 minutes even though each measured capture lasted one minute.
The retained interval rates also show within-capture change. This supports
testing shorter separation; it does not prove that time separation caused drift.

For the next distinct experiment, run A1, B and A2 consecutively for each control
configuration before advancing to the next configuration. Keep all cases, pinned
products, fixture inputs, build validation, runtime/JIT/GC settings, CPU affinity,
360-second elapsed workload warmup, 60-second measurement, prepared observer heap
and acceptance tolerances unchanged. Each phase remains a fresh process on one
ubuntu-latest VM. Record exact UTC capture start/end times and phase/product/case
identities in capture-order.json. Preserve raw histograms, maxima, runtime events,
binary/source archives and all control results. No post-hoc window selection.

Candidate-only cases still run after the controls. Their recorded thread-pool and
DNS continuation transitions are a separate unresolved question; changing control
order does not waive startup, correctness, changed-path or stability requirements.
The reorder alone is not a performance PASS or authorization for another run.
