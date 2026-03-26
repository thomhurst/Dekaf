using TUnit.Core;

[assembly: Timeout(600_000)] // 10 minutes — generous for CI thread pool starvation (4 cores, 16 parallel tests)
