```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Xeon 6973P-C 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

Job=A2  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method          | Mode               | Mean       | Error    | StdDev   | Gen0   | Allocated |
|---------------- |------------------- |-----------:|---------:|---------:|-------:|----------:|
| **ProcessLifetime** | **KeyRecordsDistinct** |   **729.1 ns** |  **4.41 ns** |  **5.89 ns** | **0.0076** |     **928 B** |
| **ProcessLifetime** | **KeyBatchesDistinct** |   **827.9 ns** | **26.23 ns** | **35.02 ns** | **0.0267** |    **2248 B** |
| **ProcessLifetime** | **KeyRecordsPaired**   |   **965.7 ns** | **34.24 ns** | **45.71 ns** | **0.0114** |    **1164 B** |
| **ProcessLifetime** | **KeyBatchesPaired**   | **1,074.6 ns** | **23.68 ns** | **31.61 ns** | **0.0267** |    **2440 B** |
