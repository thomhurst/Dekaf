```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Xeon 6973P-C 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

Job=DryB  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=1  LaunchCount=1  RunStrategy=ColdStart  
UnrollFactor=1  WarmupCount=1  

```
| Method          | Mode               | Mean     | Error | Allocated |
|---------------- |------------------- |---------:|------:|----------:|
| **ProcessLifetime** | **KeyRecordsDistinct** | **218.5 ns** |    **NA** |         **-** |
| **ProcessLifetime** | **KeyBatchesDistinct** | **211.7 ns** |    **NA** |         **-** |
| **ProcessLifetime** | **KeyRecordsPaired**   | **225.7 ns** |    **NA** |         **-** |
| **ProcessLifetime** | **KeyBatchesPaired**   | **221.6 ns** |    **NA** |         **-** |
