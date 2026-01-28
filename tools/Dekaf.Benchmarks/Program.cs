using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

var config = DefaultConfig.Instance
    .WithOptions(ConfigOptions.DisableOptimizationsValidator);

// Pass all arguments to BenchmarkSwitcher for flexible filtering
// Examples:
//   dotnet run -c Release -- --filter "*Unit*"     (run unit benchmarks)
//   dotnet run -c Release -- --filter "*Client*"   (run client benchmarks)
//   dotnet run -c Release -- --filter "*Producer*" (run producer benchmarks)
//   dotnet run -c Release                          (run all benchmarks)

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
    .Run(args, config);
