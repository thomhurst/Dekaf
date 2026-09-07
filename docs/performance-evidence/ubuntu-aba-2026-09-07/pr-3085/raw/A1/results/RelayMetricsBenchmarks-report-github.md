```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=A1  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method           | Enabled | Mean       | Error     | StdDev    | Median     | Max        | Gen0   | Allocated |
|----------------- |-------- |-----------:|----------:|----------:|-----------:|-----------:|-------:|----------:|
| **SynchronousBatch** | **False**   | **519.069 ns** | **1.1840 ns** | **1.5806 ns** | **518.553 ns** | **524.728 ns** | **0.0083** |     **144 B** |
| PendingBatch     | False   | 974.766 ns | 2.6185 ns | 3.4956 ns | 974.371 ns | 987.779 ns | 0.0272 |     480 B |
| PublisherControl | False   |   5.575 ns | 0.0738 ns | 0.0986 ns |   5.551 ns |   6.039 ns |      - |         - |
| **SynchronousBatch** | **True**    | **513.593 ns** | **1.1217 ns** | **1.4974 ns** | **513.292 ns** | **519.260 ns** | **0.0083** |     **144 B** |
| PendingBatch     | True    | 955.575 ns | 4.4890 ns | 5.9927 ns | 954.527 ns | 977.073 ns | 0.0267 |     480 B |
| PublisherControl | True    |   5.558 ns | 0.0066 ns | 0.0088 ns |   5.556 ns |   5.585 ns |      - |         - |
