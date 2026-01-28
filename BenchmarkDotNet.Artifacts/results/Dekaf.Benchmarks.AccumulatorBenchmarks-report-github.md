```

BenchmarkDotNet v0.14.0, Ubuntu 25.10 (Questing Quokka)
12th Gen Intel Core i7-12700K, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2
  Job-PZCEMQ : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2

InvocationCount=1  IterationCount=10  UnrollFactor=1  
WarmupCount=3  

```
| Method                                          | MessageSize | BatchSize | Mean         | Error       | StdDev     | Median       | Allocated |
|------------------------------------------------ |------------ |---------- |-------------:|------------:|-----------:|-------------:|----------:|
| **&#39;Serialize + Accumulate (Fire-and-Forget Path)&#39;** | **100**         | **100**       |    **38.573 μs** |   **0.9910 μs** |  **0.5897 μs** |    **38.459 μs** |         **-** |
| &#39;Serialize Only (Key + Value)&#39;                  | 100         | 100       |    18.851 μs |   0.2459 μs |  0.1463 μs |    18.837 μs |         - |
| &#39;String Interpolation Only&#39;                     | 100         | 100       |     5.537 μs |   0.4794 μs |  0.2507 μs |     5.636 μs |         - |
| &#39;DateTimeOffset.UtcNow Only&#39;                    | 100         | 100       |     3.420 μs |   1.0854 μs |  0.7179 μs |     3.203 μs |         - |
| &#39;Accumulate Same Partition (Tests Cache)&#39;       | 100         | 100       |    34.945 μs |   0.9758 μs |  0.5807 μs |    34.880 μs |         - |
| &#39;Accumulate Rotating Partitions (No Cache)&#39;     | 100         | 100       |    39.324 μs |   0.8164 μs |  0.4858 μs |    39.261 μs |         - |
| &#39;Batch Append Same Partition (Single Lock)&#39;     | 100         | 100       |    68.362 μs |   1.8587 μs |  0.9721 μs |    68.390 μs |   13120 B |
| **&#39;Serialize + Accumulate (Fire-and-Forget Path)&#39;** | **100**         | **1000**      |   **694.237 μs** |  **26.6934 μs** | **15.8848 μs** |   **691.485 μs** |  **130616 B** |
| &#39;Serialize Only (Key + Value)&#39;                  | 100         | 1000      |   176.699 μs |  15.6270 μs |  8.1732 μs |   177.911 μs |   40656 B |
| &#39;String Interpolation Only&#39;                     | 100         | 1000      |    50.202 μs |   7.2541 μs |  3.7940 μs |    50.791 μs |   40656 B |
| &#39;DateTimeOffset.UtcNow Only&#39;                    | 100         | 1000      |    28.854 μs |   5.7171 μs |  3.4022 μs |    26.798 μs |         - |
| &#39;Accumulate Same Partition (Tests Cache)&#39;       | 100         | 1000      |   665.087 μs |  42.7133 μs | 25.4180 μs |   655.138 μs |  132008 B |
| &#39;Accumulate Rotating Partitions (No Cache)&#39;     | 100         | 1000      |   710.115 μs |  46.6427 μs | 27.7563 μs |   699.489 μs |  130616 B |
| &#39;Batch Append Same Partition (Single Lock)&#39;     | 100         | 1000      |   983.656 μs |  16.2130 μs |  8.4797 μs |   984.087 μs |  188032 B |
| **&#39;Serialize + Accumulate (Fire-and-Forget Path)&#39;** | **1000**        | **100**       |    **53.522 μs** |   **5.1121 μs** |  **3.3813 μs** |    **51.938 μs** |    **8744 B** |
| &#39;Serialize Only (Key + Value)&#39;                  | 1000        | 100       |    27.451 μs |   9.1311 μs |  4.7757 μs |    25.675 μs |         - |
| &#39;String Interpolation Only&#39;                     | 1000        | 100       |     5.885 μs |   0.4649 μs |  0.2432 μs |     5.917 μs |         - |
| &#39;DateTimeOffset.UtcNow Only&#39;                    | 1000        | 100       |     2.698 μs |   0.0553 μs |  0.0329 μs |     2.688 μs |         - |
| &#39;Accumulate Same Partition (Tests Cache)&#39;       | 1000        | 100       |    72.581 μs |  34.7723 μs | 22.9997 μs |    87.272 μs |    9512 B |
| &#39;Accumulate Rotating Partitions (No Cache)&#39;     | 1000        | 100       |    52.594 μs |   1.4382 μs |  0.8558 μs |    52.492 μs |    9800 B |
| &#39;Batch Append Same Partition (Single Lock)&#39;     | 1000        | 100       |    83.681 μs |   6.1545 μs |  3.2189 μs |    83.454 μs |   15136 B |
| **&#39;Serialize + Accumulate (Fire-and-Forget Path)&#39;** | **1000**        | **1000**      |   **901.616 μs** | **109.9474 μs** | **57.5046 μs** |   **914.708 μs** |  **475032 B** |
| &#39;Serialize Only (Key + Value)&#39;                  | 1000        | 1000      |   246.679 μs |   9.3102 μs |  5.5404 μs |   245.921 μs |   40368 B |
| &#39;String Interpolation Only&#39;                     | 1000        | 1000      |    52.626 μs |  12.8297 μs |  7.6348 μs |    52.760 μs |   40656 B |
| &#39;DateTimeOffset.UtcNow Only&#39;                    | 1000        | 1000      |    45.043 μs |   6.6773 μs |  3.9736 μs |    43.248 μs |         - |
| &#39;Accumulate Same Partition (Tests Cache)&#39;       | 1000        | 1000      |   866.971 μs | 150.9624 μs | 89.8353 μs |   902.933 μs |  474456 B |
| &#39;Accumulate Rotating Partitions (No Cache)&#39;     | 1000        | 1000      |   924.372 μs | 114.6617 μs | 68.2334 μs |   930.734 μs |  475032 B |
| &#39;Batch Append Same Partition (Single Lock)&#39;     | 1000        | 1000      | 1,225.249 μs |  75.1741 μs | 44.7349 μs | 1,230.417 μs |  530480 B |
