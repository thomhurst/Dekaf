```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700K 3.60GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Job-XZUHJM : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

OutlierMode=DontRemove  Affinity=00000000000000000100  IterationCount=15  
IterationTime=1s  WarmupCount=30  

```
| Method           | Pattern      | BatchSize | Mean     | Error    | StdDev   | Allocated |
|----------------- |------------- |---------- |---------:|---------:|---------:|----------:|
| **DispatchLifetime** | **Repeated**     | **1**         | **35.95 ms** | **1.218 ms** | **1.140 ms** |  **45.66 KB** |
| **DispatchLifetime** | **Repeated**     | **16**        | **34.06 ms** | **2.272 ms** | **2.126 ms** |     **47 KB** |
| **DispatchLifetime** | **Distinct**     | **1**         | **33.65 ms** | **1.407 ms** | **1.316 ms** |  **45.66 KB** |
| **DispatchLifetime** | **Distinct**     | **16**        | **34.95 ms** | **1.195 ms** | **1.118 ms** |     **47 KB** |
| **DispatchLifetime** | **PendingPairs** | **1**         | **35.35 ms** | **2.241 ms** | **2.096 ms** |  **46.02 KB** |
| **DispatchLifetime** | **PendingPairs** | **16**        | **31.78 ms** | **3.691 ms** | **3.453 ms** |   **48.7 KB** |
