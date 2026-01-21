using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Dekaf.Benchmarks;

var config = DefaultConfig.Instance
    .WithOptions(ConfigOptions.DisableOptimizationsValidator);

// Check for specific benchmark filter from command line
if (args.Length > 0 && args[0] == "--filter")
{
    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
        .Run(args, config);
}
else
{
    // Run all benchmarks
    BenchmarkRunner.Run(typeof(Program).Assembly, config);
}
