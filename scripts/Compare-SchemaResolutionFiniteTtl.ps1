[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BeforeDll,
    [Parameter(Mandatory)][string]$AfterDll,
    [string]$Filter = '*SchemaResolutionFiniteTtl*',
    [long]$Affinity = 4,
    [switch]$Dry
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$before = (Resolve-Path -LiteralPath $BeforeDll).Path
$after = (Resolve-Path -LiteralPath $AfterDll).Path
$fixture = Join-Path $repo 'tools/Dekaf.Benchmarks/Benchmarks/Unit/SchemaResolutionFiniteTtlBenchmarks.cs'
$output = Join-Path $repo ('.artifacts/finite-ttl-' + [guid]::NewGuid().ToString('N'))
$harness = Join-Path $output 'harness'
New-Item -ItemType Directory -Path $harness -Force | Out-Null

# The nested solution bounds BDN project discovery. Preserve the friend assembly name.
$project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <BaselineDll Condition="'$(BaselineDll)' == ''">AFTER_DLL</BaselineDll>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="BenchmarkDotNet" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
    <Reference Include="Dekaf.SchemaRegistry"><HintPath>$(BaselineDll)</HintPath></Reference>
    <Reference Include="Dekaf"><HintPath>CORE_DLL</HintPath></Reference>
    <Compile Include="Program.cs" />
    <Compile Include="FIXTURE_PATH" />
  </ItemGroup>
</Project>
'@
$project.Replace('AFTER_DLL', [System.Security.SecurityElement]::Escape($after)).
    Replace('CORE_DLL', [System.Security.SecurityElement]::Escape((Join-Path (Split-Path $after -Parent) 'Dekaf.dll'))).
    Replace('FIXTURE_PATH', [System.Security.SecurityElement]::Escape($fixture)) |
    Set-Content (Join-Path $harness 'Dekaf.Benchmarks.csproj')

@'
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Perfolizer.Mathematics.OutlierDetection;

var dry = args.Contains("--dry");
var arguments = args.Where(value => value != "--dry").ToArray();
var before = Environment.GetEnvironmentVariable("DEKAF_TTL_BEFORE_DLL")!;
var after = Environment.GetEnvironmentVariable("DEKAF_TTL_AFTER_DLL")!;
var affinity = long.Parse(Environment.GetEnvironmentVariable("DEKAF_TTL_AFFINITY")!);
var common = (dry ? Job.Dry : Job.Default.WithWarmupCount(8).WithIterationCount(20))
    .WithEnvironmentVariable("DOTNET_TieredCompilation", "0")
    .WithAffinity(new IntPtr(affinity))
    .WithOutlierMode(OutlierMode.DontRemove);
var config = DefaultConfig.Instance
    .AddJob(common.WithId("A-Before").WithArguments([new MsBuildArgument($"/p:BaselineDll=\"{before}\"")]).AsBaseline())
    .AddJob(common.WithId("B-After").WithArguments([new MsBuildArgument($"/p:BaselineDll=\"{after}\"")]))
    .AddJob(common.WithId("C-BeforeControl").WithArguments([new MsBuildArgument($"/p:BaselineDll=\"{before}\"")]));
var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(arguments, config).ToArray();
return summaries.Length == 0 || summaries.Any(summary => summary.HasCriticalValidationErrors ||
    summary.Reports.Any(report => !report.Success)) ? 1 : 0;
'@ | Set-Content (Join-Path $harness 'Program.cs')

[ordered]@{
    Before = $before
    BeforeSha256 = (Get-FileHash -LiteralPath $before).Hash
    After = $after
    AfterSha256 = (Get-FileHash -LiteralPath $after).Hash
    FixtureSha256 = (Get-FileHash -LiteralPath $fixture).Hash
    Filter = $Filter
    Affinity = $Affinity
    TieredCompilation = 0
    Dry = [bool]$Dry
} | ConvertTo-Json | Set-Content (Join-Path $output 'inputs.json')

$priorBefore = $env:DEKAF_TTL_BEFORE_DLL
$priorAfter = $env:DEKAF_TTL_AFTER_DLL
$priorAffinity = $env:DEKAF_TTL_AFFINITY
Push-Location $harness
try {
    $env:DEKAF_TTL_BEFORE_DLL = $before
    $env:DEKAF_TTL_AFTER_DLL = $after
    $env:DEKAF_TTL_AFFINITY = $Affinity.ToString()
    dotnet new sln --name Comparison --format sln *> (Join-Path $output 'setup.log')
    if ($LASTEXITCODE -ne 0) { throw 'Solution creation failed.' }
    dotnet sln Comparison.sln add Dekaf.Benchmarks.csproj *>> (Join-Path $output 'setup.log')
    if ($LASTEXITCODE -ne 0) { throw 'Adding the comparison project failed.' }
    dotnet build Dekaf.Benchmarks.csproj -c Release *> (Join-Path $output 'build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Comparison build failed.' }
    $runArguments = @('--filter', $Filter, '--artifacts', '../results', '--exporters', 'fulljson', '--keepFiles')
    if ($Dry) { $runArguments += '--dry' } else { $runArguments += '--apples' }
    dotnet run -c Release --no-build --project Dekaf.Benchmarks.csproj -- @runArguments *> (Join-Path $output 'run.log')
    if ($LASTEXITCODE -ne 0) { throw 'Comparison failed; inspect run.log.' }
}
finally {
    Pop-Location
    $env:DEKAF_TTL_BEFORE_DLL = $priorBefore
    $env:DEKAF_TTL_AFTER_DLL = $priorAfter
    $env:DEKAF_TTL_AFFINITY = $priorAffinity
    Write-Output "Comparison artifacts: $output"
}
