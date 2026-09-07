```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Xeon 6973P-C 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

Job=DryA  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=1  LaunchCount=1  RunStrategy=ColdStart  
UnrollFactor=1  WarmupCount=1  

```
| Method          | Mode               | Mean     | Error | Gen0   | Allocated |
|---------------- |------------------- |---------:|------:|-------:|----------:|
| **ProcessLifetime** | **KeyRecordsDistinct** | **740.6 ns** |    **NA** | **0.0076** |     **928 B** |
| **ProcessLifetime** | **KeyBatchesDistinct** | **776.0 ns** |    **NA** | **0.0267** |    **2248 B** |
| **ProcessLifetime** | **KeyRecordsPaired**   | **929.2 ns** |    **NA** | **0.0114** |    **1164 B** |
| **ProcessLifetime** | **KeyBatchesPaired**   | **997.6 ns** |    **NA** | **0.0267** |    **2440 B** |
