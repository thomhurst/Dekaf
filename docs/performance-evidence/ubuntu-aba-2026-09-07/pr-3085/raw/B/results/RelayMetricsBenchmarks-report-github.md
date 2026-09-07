```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=B  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method           | Enabled | Mean         | Error     | StdDev    | Median       | Max          | Gen0   | Allocated |
|----------------- |-------- |-------------:|----------:|----------:|-------------:|-------------:|-------:|----------:|
| **SynchronousBatch** | **False**   |   **538.134 ns** | **1.8900 ns** | **2.5231 ns** |   **538.922 ns** |   **541.337 ns** |      **-** |         **-** |
| PendingBatch     | False   |   850.881 ns | 1.0594 ns | 1.4142 ns |   850.735 ns |   856.411 ns |      - |         - |
| PublisherControl | False   |     5.582 ns | 0.0621 ns | 0.0829 ns |     5.559 ns |     5.963 ns |      - |         - |
| **SynchronousBatch** | **True**    |   **747.671 ns** | **1.2182 ns** | **1.6262 ns** |   **747.231 ns** |   **754.139 ns** |      **-** |         **-** |
| PendingBatch     | True    | 1,398.296 ns | 3.0786 ns | 4.1099 ns | 1,397.083 ns | 1,413.394 ns | 0.0056 |     176 B |
| PublisherControl | True    |     5.536 ns | 0.0144 ns | 0.0192 ns |     5.529 ns |     5.599 ns |      - |         - |
