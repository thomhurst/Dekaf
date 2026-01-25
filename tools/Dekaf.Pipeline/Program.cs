using Dekaf.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.Extensions;
using ModularPipelines.Options;

var builder = Pipeline.CreateBuilder();

builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);
builder.Services.Configure<NuGetOptions>(builder.Configuration.GetSection("NuGet"));
builder.Services.AddModulesFromAssembly(typeof(Program).Assembly);
builder.Options.ExecutionMode = ExecutionMode.WaitForAllModules;

// Check for --categories argument
var categoriesArg = args.SkipWhile(a => a != "--categories").Skip(1).TakeWhile(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
if (categoriesArg.Length > 0)
{
    builder.RunCategories(categoriesArg);
}

await builder.Build().RunAsync();
