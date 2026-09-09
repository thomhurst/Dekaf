# Member-removal GC warmup experiment

PR 3129 uses 360 seconds of continuous configured workload warmup, followed by
60 seconds of measurement in every fresh A1/B/A2 process. This extends the prior
180-second experiment after run 34282420353 observed Gen2/ArrayPool-trimming JIT
approximately 214 seconds into the workload. Retain every sample and runtime
transition; 360 seconds is not itself proof of steady state.

First run the legacy one-member pilot against fresh main. If valid, expand the
existing four control cases and fifteen candidate-only cases with identical
settings. The full matrix has 27 measured process invocations and needs about
192 minutes for priming, warmup and measurement, plus builds and validation.
Only this full administrative job receives a 240-minute timeout. Other suites,
scheduled stress coverage and paid-lane limits remain unchanged.

Use the same runner, exact pins, fixtures, preallocated value-type histograms,
runtime/JIT settings, continuous call loop and deferred reporting in all phases.
Archive raw maxima, CPU/allocation counters, completed counts, JIT events,
per-second histograms, heap/RSS and GC trends. Keep the existing 3% throughput
and CPU limits, 5% latency limits and allocation/control rules. A confirmed
regression fails; drift, remaining startup transitions or insufficient evidence
remain INCONCLUSIVE. No automatic gate update follows successful execution.
