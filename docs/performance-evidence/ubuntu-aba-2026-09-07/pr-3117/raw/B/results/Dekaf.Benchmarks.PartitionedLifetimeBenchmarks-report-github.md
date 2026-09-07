```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Xeon 6973P-C 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

Job=B  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method          | Mode               | Mean     | Error   | StdDev  | Allocated |
|---------------- |------------------- |---------:|--------:|--------:|----------:|
| **ProcessLifetime** | **KeyRecordsDistinct** | **217.9 ns** | **5.08 ns** | **6.78 ns** |         **-** |
| **ProcessLifetime** | **KeyBatchesDistinct** | **212.7 ns** | **4.72 ns** | **6.31 ns** |         **-** |
| **ProcessLifetime** | **KeyRecordsPaired**   | **225.1 ns** | **0.39 ns** | **0.52 ns** |         **-** |
| **ProcessLifetime** | **KeyBatchesPaired**   | **222.4 ns** | **5.05 ns** | **6.74 ns** |         **-** |
