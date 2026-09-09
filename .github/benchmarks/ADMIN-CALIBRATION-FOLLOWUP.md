# Administrative recorder calibration after #3159

Pin A and B to fresh main `551d4d0825dca64b7143d6f18fc2b13baaf1fe0a`. Use `suite=admin-calibration`, `pr=3138`, selecting the existing two baseline control cases: `legacy-create:16` and `legacy-delete:16`. All A1/B/A2 processes use one compiled baseline fixture, without candidate-only APIs. Run once; no automatic repeat is authorized by this plan.

This measures repeatability after the observer storage/scan and infrastructure-affinity corrections in #3159. It is not product acceptance for #3138 or another PR. The earlier full #3138 run [34310252228](https://github.com/thomhurst/Dekaf/actions/runs/34310252228) used baseline `551d4d0825dca64b7143d6f18fc2b13baaf1fe0a`, B `546a7df7d5e5a2abe38f73795ceee5040be3fc4d` and harness `838f031b70fbcc7bea3c3c994ea2b7d2a527ea4b`. Its create p99 was 1072/1151/1122 ns (+7.37%/+2.58%), with other control/runtime uncertainty. Older admin controls also retained repeated Gen2 activity and maxima uncertainty. No admin candidate is accepted from those observations.

Use the unchanged 480-second continuous workload warmup and 180-second measurement for each fresh process on one GitHub-hosted ubuntu-latest VM. Six processes cost about 66 minutes plus build/smoke time. Preserve all tick/count samples, maxima, correctness accounting, runtime/GC/heap data and new host CPU/steal/pressure observations. Exhausted recording storage or missing host telemetry fails the capture.

The initial prose incorrectly counted three controls. The selected driver has always used the two named cases; this correction does not alter the running command, binaries, matrix, warmup, measurement or limits.

Keep 3% throughput/CPU, 5% p50/p99/max and 1 B/call allocation screens. Compare B with each A and retain A1/A2 drift; do not average or remove maxima. Failed screens establish insufficient repeatability for the declared configuration. Passing screens characterize only this calibration, not general maximum precision or long-run behavior. Review before spending on another administrative acceptance campaign with this recorder. No historical result is relabeled or pooled with the changed harness.
