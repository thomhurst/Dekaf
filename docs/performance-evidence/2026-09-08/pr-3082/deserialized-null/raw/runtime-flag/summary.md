# Deserialized-null incremental diagnosis

Product acceptance: **INCONCLUSIVE**. No protected-metric tradeoff is approved.

| Key operation | A1 ns | B ns | A2 ns | B/A1 | B/A2 | Control drift | B/op A1/B/A2 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| ByteArray | 53.552598 | 45.070575 | 19.325329 | -15.839% | +133.220% | -63.913% | 0/0/0 |
| ReadOnlyMemory | 74.206145 | 16.415819 | 67.774273 | -77.878% | -75.779% | -8.668% | 0/0/0 |
| Memory | 90.832808 | 18.024778 | 24.610105 | -80.156% | -26.759% | -72.906% | 0/0/0 |
| ArraySegment | 2.503130 | 2.537150 | 3.476665 | +1.359% | -27.023% | +38.893% | 0/0/0 |
| String | 53.186621 | 30.454218 | 18.961992 | -42.741% | +60.607% | -64.348% | 0/0/0 |
| Int32 | 16.864419 | 0.205352 | 0.209513 | -98.782% | -1.986% | -98.758% | 0/0/0 |
| NullableInt32 | 18.125778 | 0.598060 | 0.460200 | -96.701% | +29.957% | -97.461% | 0/0/0 |
| CustomString | 7.672163 | 7.993278 | 7.570997 | +4.185% | +5.578% | -1.319% | 0/0/0 |
| NullKinds | 0.632578 | 0.999771 | 0.677949 | +58.047% | +47.470% | +7.173% | 0/0/0 |

NullKinds intentionally changes equality from equal in A to distinct in B. It is not equivalent completed dispatch work. Other cases use identical runtime false wire flags and identical normal hash/equality outputs.

Each phase retains 15 corrected BDN iteration samples and all actual measurements, including maxima. These are iteration statistics, not per-message latency percentiles. CPU boundary deltas include BDN/logging/bookkeeping between the last warmup and last actual boundary; they are diagnostic process activity, not isolated client CPU per completed message.

| Key operation | Direct warmup seconds A1/B/A2 | Direct warmup ops A1/B/A2 | BDN warmup seconds A1/B/A2 | Measured JIT counts A1/B/A2 | Diagnostic screen |
| --- | --- | --- | --- | --- | --- |
| ByteArray | 20.010908/20.012333/20.000003 | 397947607/403495347/397888083 | 10.535/10.352/10.679 | 15/262/82 | INCONCLUSIVE |
| ReadOnlyMemory | 20.000003/20.010014/20.012477 | 351685337/395373946/357141692 | 11.564/13.231/11.908 | 185/143/194 | INCONCLUSIVE |
| Memory | 20.000003/20.010576/20.000009 | 356288003/391474779/371417158 | 14.478/15.665/15.330 | 300/126/104 | INCONCLUSIVE |
| ArraySegment | 20.000004/20.010045/20.000005 | 257232112/184547586/252165878 | 0.627/0.766/0.673 | 4/4/4 | INCONCLUSIVE |
| String | 20.009983/20.000005/20.000010 | 384757101/361710678/373428276 | 7.320/10.427/9.333 | 181/132/120 | INCONCLUSIVE |
| Int32 | 20.010177/20.009753/20.012261 | 421945381/425561018/422099700 | 15.361/11.443/14.067 | 6/4/4 | INCONCLUSIVE |
| NullableInt32 | 20.000003/20.012369/20.010330 | 426356356/401113219/422422430 | 20.710/16.733/14.712 | 210/4/4 | INCONCLUSIVE |
| CustomString | 20.010779/20.011707/20.012476 | 347837000/342210945/300201513 | 1.530/1.492/1.507 | 5/55/4 | INCONCLUSIVE |
| NullKinds | 20.010011/20.000003/20.010969 | 378593077/326469263/375280642 | 1.255/1.487/1.294 | 4/4/4 | CHANGED-SEMANTICS |

Full confidence intervals, individual samples, runtime intervals, layout checks, memory scopes and loaded assembly bindings are retained in summary.json and the phase artifacts. Overlapping intervals do not prove equivalence.
