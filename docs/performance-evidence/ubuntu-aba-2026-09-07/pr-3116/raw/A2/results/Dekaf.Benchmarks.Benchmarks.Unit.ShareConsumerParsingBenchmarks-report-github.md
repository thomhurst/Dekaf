```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763 2.45GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=A2  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method                         | RecordCount | HeaderCount | Mean       | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|------------------------------- |------------ |------------ |-----------:|----------:|----------:|-------:|-------:|----------:|
| ParseSynchronousBatch          | 1024        | 0           | 194.392 μs | 1.6514 μs | 2.2046 μs | 5.4012 | 0.7716 |   90448 B |
| ParseWarmPreparedBatch         | 1024        | 0           | 169.778 μs | 0.5824 μs | 0.7775 μs | 4.7554 | 0.6793 |   90328 B |
| TraverseRetainedBatch          | 1024        | 0           |   3.314 μs | 0.0322 μs | 0.0429 μs |      - |      - |         - |
| ParseBorrowedSynchronousBatch  | 1024        | 0           | 183.193 μs | 2.2392 μs | 2.9892 μs | 5.0872 | 0.7267 |   90448 B |
| ParseBorrowedWarmPreparedBatch | 1024        | 0           | 170.030 μs | 1.4030 μs | 1.8729 μs | 4.7554 | 0.6793 |   90328 B |
| ParseBorrowedColdPreparedBatch | 1024        | 0           | 200.816 μs | 1.2091 μs | 1.6141 μs | 4.8077 | 0.8013 |   90792 B |
| ParseColdPreparedBatch         | 1024        | 0           | 201.310 μs | 1.0731 μs | 1.4326 μs | 4.9342 | 0.8224 |   90795 B |
