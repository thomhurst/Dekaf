# Pending-request pool cases

The standard `micro` suite covers #3142's pool-count correction and #3137's
Reservoir dependency change. It uses the shared out-of-process BenchmarkDotNet
project, MemoryDiagnoser, 50 workload warmups and 25 retained measurements.
The existing legacy pool runner is not used for this campaign.

`RentReturn` measures the ordinary warmed pool path and must allocate 0 B/op.
`CompleteResetAndRecover` completes and consumes a response, returns its request,
then completes another request to validate continued operation. Both successful
reservation disposal and an injected throwing disposal are measured. Exception
handling and replacement-request allocations in the failure case are cold-path
costs; report their absolute bytes and compare like-for-like against both controls.

The baseline's known count defect is explicitly asserted after every failed reset.
Only #3142's candidate asserts the repaired count. #3137 changes the dependency,
so both its products retain the baseline count behavior. Every phase performs
the same completion, cleanup and recovery work. The fixture does not rewrite
product code or exchange compiled assemblies.

Predeclared screen: 5% mean-time tolerance against each control and no allocation
growth. Control drift is diagnostic. These cases measure the changed data
structure directly; retained loaded producer evidence provides adjacent
throughput, per-message latency, CPU and stability coverage. Microbenchmark
iteration percentiles are not per-message latency.
