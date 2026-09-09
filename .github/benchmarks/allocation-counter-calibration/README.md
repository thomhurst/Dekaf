# Allocation-counter calibration

This diagnostic tests an alternative to a decreasing process allocation counter.
It does not run Dekaf workloads or produce a performance acceptance verdict.

Build with the repository SDK and install the repository-pinned dotnet-trace.
Run:

~~~text
python .github/benchmarks/allocation-counter-calibration/run.py --output calibration-results --trace /path/to/dotnet-trace
~~~

The workload records exact per-thread allocation deltas around known byte-array
allocation loops. An external EventPipe collector retains allocation ticks and
phase markers. The inspector sums AllocationAmount64 within those markers,
rejects lost events or missing boundaries, and checks a predeclared 1% agreement
tolerance. It preserves the process counter, known thread totals, raw trace,
heap-count history, source, actual binaries, tool/runtime identities and hashes.
The default four cases exercise small/large objects with server GC and DATAS
disabled/enabled. A fifth case follows four allocating workers with a slower
serial tail to exercise heap shrink. A calibration pass is not a general error
bound, a throughput/latency claim or permission to replace a missing gate metric.

The initial Windows .NET 10.0.12 diagnostic observed 10,289,187,416 known allocated
bytes during a grow/shrink case. The process counter reported 6,847,925,448;
allocation events reported 10,295,144,504 (+0.0579%), with zero lost events.
Heap count grew from 1 to 7 and then fell through 5, 3 and 2 to 1. Four simpler
calibrations were within 0.091% with zero lost events. This reproduces a process
counter accounting loss while showing a promising separate observation method.
It does not establish an error bound for long-running Linux producer workloads.

The portable runner then completed all five declared cases using the archived
source and binaries. The [summary](https://github.com/thomhurst/Dekaf/blob/9ea11d3a0f4ca623a274f8810f3eb16039714c21/.github/benchmarks/allocation-counter-calibration/observations/windows-net10.0.12-summary.json)
records zero lost events and absolute event-sum error below 0.097% in every case.
Its [heap-shrink result](https://github.com/thomhurst/Dekaf/blob/9ea11d3a0f4ca623a274f8810f3eb16039714c21/.github/benchmarks/allocation-counter-calibration/observations/heap-shrink-result.json) records
10,289,245,648 known bytes, 10,296,113,848 event bytes (+0.0668%), and only
6,248,153,360 bytes from the process counter. Heap count grows to 8 and then
falls to 1. This second calibration is retained alongside the initial result;
the difference in process-counter losses is not averaged away.

The [inventory](https://github.com/thomhurst/Dekaf/blob/9ea11d3a0f4ca623a274f8810f3eb16039714c21/.github/benchmarks/allocation-counter-calibration/observations/windows-net10.0.12-inventory.json) binds the complete
local capture, including raw traces and binaries, retained at
C:/git/Dekaf-perf-improve-20260908/allocation-counter-calibration-verified-v3.
Runnable source is maintained here; historical JSON observations and the manifest remain at the linked pinned revision. The raw
traces and binaries are not a hosted Actions artifact or embedded in this tree.

Before using event sums for acceptance, measure recorder overhead identically
in A1/B/A2, bound phase-edge allocation-context and tick remainders, validate
loss detection under the actual event rate, and assess heap growth/shrink and
native versus managed allocation scope. Retain the original precise counters
even when invalid; never clamp them or rewrite old results.

[Runtime event documentation](https://learn.microsoft.com/en-us/dotnet/fundamentals/diagnostics/runtime-garbage-collection-events#gcallocationtick_v3-event)
describes the approximate 100 KB tick and AllocationAmount64.
[The .NET 10.0.0 source](https://github.com/dotnet/runtime/blob/v10.0.0/src/coreclr/gc/gc.cpp)
accumulates allocation-context amounts per heap/object-heap kind and resets the
tick remainder after emitting an event. This is useful explanatory evidence,
not proof of the exact .NET 10.0.12 implementation: the installed runtime reports
commit 95017c711e6afc1085133d440e42b4bd78155701, whose public source URL returned 404.
