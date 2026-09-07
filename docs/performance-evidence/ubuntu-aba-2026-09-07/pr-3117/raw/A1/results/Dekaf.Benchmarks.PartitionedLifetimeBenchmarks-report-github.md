```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Xeon 6973P-C 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

Job=A1  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method          | Mode               | Mean       | Error    | StdDev   | Gen0   | Allocated |
|---------------- |------------------- |-----------:|---------:|---------:|-------:|----------:|
| **ProcessLifetime** | **KeyRecordsDistinct** |   **735.7 ns** | **26.62 ns** | **35.53 ns** | **0.0076** |     **928 B** |
| **ProcessLifetime** | **KeyBatchesDistinct** |   **768.9 ns** |  **8.15 ns** | **10.87 ns** | **0.0267** |    **2248 B** |
| **ProcessLifetime** | **KeyRecordsPaired**   |   **933.7 ns** | **21.33 ns** | **28.48 ns** | **0.0114** |    **1164 B** |
| **ProcessLifetime** | **KeyBatchesPaired**   | **1,030.0 ns** | **15.28 ns** | **20.39 ns** | **0.0267** |    **2440 B** |
