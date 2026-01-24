```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
12th Gen Intel Core i7-12700K, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2
  Job-ETBZBA : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2
  ShortRun   : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2

InvocationCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                                         | Job        | IterationCount | LaunchCount | Mean      | Error       | StdDev     | Median    | Allocated |
|----------------------------------------------- |----------- |--------------- |------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Protocol Write: 1000 Int32s&#39;                  | Job-ETBZBA | 10             | Default     | 17.839 μs |   6.6308 μs |  3.9459 μs | 17.241 μs |         - |
| &#39;Protocol Write: 100 Strings (100 chars each)&#39; | Job-ETBZBA | 10             | Default     | 10.468 μs |   5.2698 μs |  3.4857 μs |  9.980 μs |         - |
| &#39;Protocol Write: 100 CompactStrings&#39;           | Job-ETBZBA | 10             | Default     |  7.283 μs |   0.9499 μs |  0.4968 μs |  7.211 μs |         - |
| &#39;Protocol Read: 1000 Int32s&#39;                   | Job-ETBZBA | 10             | Default     | 84.195 μs |  32.2209 μs | 19.1741 μs | 78.461 μs |         - |
| &#39;RecordBatch: Write 10 Records&#39;                | Job-ETBZBA | 10             | Default     | 13.968 μs |   1.0886 μs |  0.6478 μs | 13.915 μs |         - |
| &#39;RecordBatch: Read 10 Records&#39;                 | Job-ETBZBA | 10             | Default     |  4.622 μs |   3.3501 μs |  1.7522 μs |  4.102 μs |         - |
| &#39;VarInt: Write 1000 values&#39;                    | Job-ETBZBA | 10             | Default     | 20.587 μs |   7.3457 μs |  4.3713 μs | 20.928 μs |         - |
| &#39;VarInt: Read 1000 values&#39;                     | Job-ETBZBA | 10             | Default     | 49.744 μs |  36.3099 μs | 21.6074 μs | 63.583 μs |         - |
| &#39;Protocol Write: 1000 Int32s&#39;                  | ShortRun   | 3              | 1           | 14.205 μs |   3.9835 μs |  0.2184 μs | 14.272 μs |         - |
| &#39;Protocol Write: 100 Strings (100 chars each)&#39; | ShortRun   | 3              | 1           |  7.969 μs |   4.9457 μs |  0.2711 μs |  7.826 μs |         - |
| &#39;Protocol Write: 100 CompactStrings&#39;           | ShortRun   | 3              | 1           |  7.489 μs |   7.3333 μs |  0.4020 μs |  7.266 μs |         - |
| &#39;Protocol Read: 1000 Int32s&#39;                   | ShortRun   | 3              | 1           | 60.132 μs |  97.4100 μs |  5.3394 μs | 57.770 μs |         - |
| &#39;RecordBatch: Write 10 Records&#39;                | ShortRun   | 3              | 1           | 15.540 μs |  16.7290 μs |  0.9170 μs | 15.895 μs |         - |
| &#39;RecordBatch: Read 10 Records&#39;                 | ShortRun   | 3              | 1           |  8.332 μs | 119.2696 μs |  6.5376 μs |  5.074 μs |         - |
| &#39;VarInt: Write 1000 values&#39;                    | ShortRun   | 3              | 1           | 19.986 μs | 103.9743 μs |  5.6992 μs | 16.945 μs |         - |
| &#39;VarInt: Read 1000 values&#39;                     | ShortRun   | 3              | 1           | 65.636 μs | 116.4767 μs |  6.3845 μs | 62.215 μs |         - |
