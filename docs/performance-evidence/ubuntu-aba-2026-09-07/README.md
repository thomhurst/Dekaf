# Fresh-main Ubuntu A–B–A results

All seven branches were rebased without conflicts onto main `a48fafe4121350da7ad83fcdd238f0f8039d6d59` before measurement.

Each run used one ubuntu-latest VM, with exact prebuilt baseline binaries reused for A1 and A2. Builds and Dry validation preceded all timed phases. Tiered compilation was disabled, each host was pinned to CPU 0, all outliers were retained, and each phase started a fresh .NET process. Runtime, image, CPU, timestamps and hashes are in the raw provenance.

| PR | Measured head | Run | Finding | Acceptance |
|---|---|---|---|---|
| [#3082](pr-3082/README.md) | `5492f3069` | [Actions](https://github.com/thomhurst/Dekaf/actions/runs/34145853504) | Repeated binary keys improve 77â€“80%, but 64 KiB distinct keys remain about 3.25â€“3.35 times slower than main. | REGRESSION (large distinct keys) |
| [#3083](pr-3083/README.md) | `0e327370e` | [Actions](https://github.com/thomhurst/Dekaf/actions/runs/34145860025) | Typed traversal is near the later main control; several unchanged controls drift about 9â€“13%. | INCONCLUSIVE |
| [#3085](pr-3085/README.md) | `3feaeaa1f` | [Actions](https://github.com/thomhurst/Dekaf/actions/runs/34146134261) | Disabled pending relay improves 10â€“13% at 0 B/batch, but disabled synchronous relay is 3â€“4% slower. | REGRESSION (disabled synchronous relay) |
| [#3086](pr-3086/README.md) | `3d63058d6` | [Actions](https://github.com/thomhurst/Dekaf/actions/runs/34145873466) | Follower-error handling improves 23â€“27% and saves 64 B/fetch; prefetch-success timing remains uncertain. | INCONCLUSIVE |
| [#3109](pr-3109/README.md) | `5446a4f35` | [Actions](https://github.com/thomhurst/Dekaf/actions/runs/34145879464) | Complete-queue shutdown timing lies between main controls; baseline allocation also varies. | INCONCLUSIVE |
| [#3116](pr-3116/README.md) | `677b93bc1` | [Actions](https://github.com/thomhurst/Dekaf/actions/runs/34145885653) | Borrowed parsing improves roughly 5â€“11% and removes most batch allocation; retained traversal is 6â€“8% slower. | REGRESSION (retained traversal) |
| [#3117](pr-3117/README.md) | `e9e6e3c15` | [Actions](https://github.com/thomhurst/Dekaf/actions/runs/34145891257) | All four sustained dispatch cases improve 70â€“79% versus both main controls, with 0 B/message. | INCONCLUSIVE (full acceptance) |

138 measured cases completed across seven successful A–B–A runs. The first #3085 attempt stopped before timing because the declared case count omitted two relay publisher-control cases. The corrected workflow required all ten cases; product SHAs and fixture method bodies were unchanged. The failed attempt is preserved under pr-3085/initial-dry-failure.

Current-main comparisons expose different behavior where the feature fixes correctness or introduces an API. In particular, #3082 main groups binary keys by reference; #3086 main incorrectly resets follower offsets; #3116 baseline uses the existing legacy parser for the new borrowed API rows. Those differences are explicit in the fixture and cannot be treated as identical feature contracts.

Hosted runners reduced local workload interference but did not eliminate control movement. For example, #3083 controls move about 9–13% on some operations, and #3109 baseline allocation differs across unchanged controls. Results are kept separate from the Windows experiments. No averages hide conflicting controls, no performance thresholds were changed, and no full Pareto acceptance or merge was performed.
