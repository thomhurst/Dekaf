[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$LegacyDekafVersion = '1.12.0'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$fixtureProject = Join-Path $repoRoot 'tests/Dekaf.Tests.Compatibility.LegacyConsumer/Dekaf.Tests.Compatibility.LegacyConsumer.csproj'
$runDirectory = Join-Path $repoRoot 'artifacts/abstractions-binary-compatibility'
$candidateDekaf = Join-Path $repoRoot "src/Dekaf/bin/$Configuration/net10.0/Dekaf.dll"
$candidateAbstractions = Join-Path $repoRoot "src/Dekaf.Abstractions/bin/$Configuration/net10.0/Dekaf.Abstractions.dll"
$candidateAbstractionsDeps = Join-Path $repoRoot "src/Dekaf.Abstractions/bin/$Configuration/net10.0/Dekaf.Abstractions.deps.json"

foreach ($candidate in @($candidateDekaf, $candidateAbstractions, $candidateAbstractionsDeps)) {
    if (-not (Test-Path -LiteralPath $candidate)) {
        throw "Candidate assembly not found: $candidate"
    }
}

dotnet build $fixtureProject `
    --configuration $Configuration `
    --output $runDirectory `
    -p:LegacyDekafVersion=$LegacyDekafVersion `
    -p:TreatWarningsAsErrors=true
if ($LASTEXITCODE -ne 0) {
    throw "Legacy compatibility fixture build failed with exit code $LASTEXITCODE"
}

Copy-Item -LiteralPath $candidateDekaf -Destination $runDirectory -Force
Copy-Item -LiteralPath $candidateAbstractions -Destination $runDirectory -Force

dotnet exec `
    --additional-deps $candidateAbstractionsDeps `
    (Join-Path $runDirectory 'Dekaf.Tests.Compatibility.LegacyConsumer.dll')
if ($LASTEXITCODE -ne 0) {
    throw "Legacy compatibility fixture failed with exit code $LASTEXITCODE"
}
