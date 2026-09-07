```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763 2.45GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=A1  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method                         | RecordCount | HeaderCount | Mean       | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|------------------------------- |------------ |------------ |-----------:|----------:|----------:|-------:|-------:|----------:|
| ParseSynchronousBatch          | 1024        | 0           | 183.075 μs | 1.2983 μs | 1.7332 μs | 5.0872 | 0.7267 |   90450 B |
| ParseWarmPreparedBatch         | 1024        | 0           | 161.304 μs | 0.4047 μs | 0.5403 μs | 5.3191 | 0.6649 |   90328 B |
| TraverseRetainedBatch          | 1024        | 0           |   3.263 μs | 0.0032 μs | 0.0042 μs |      - |      - |         - |
| ParseBorrowedSynchronousBatch  | 1024        | 0           | 182.991 μs | 1.4525 μs | 1.9390 μs | 5.0872 | 0.7267 |   90448 B |
| ParseBorrowedWarmPreparedBatch | 1024        | 0           | 164.207 μs | 0.1954 μs | 0.2608 μs | 5.3191 | 0.6649 |   90328 B |
| ParseBorrowedColdPreparedBatch | 1024        | 0           | 193.392 μs | 1.4112 μs | 1.8839 μs | 4.6875 | 0.7813 |   90792 B |
| ParseColdPreparedBatch         | 1024        | 0           | 193.450 μs | 0.9911 μs | 1.3231 μs | 5.3354 | 0.7622 |   90792 B |
