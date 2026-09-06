---
sidebar_position: 5
description: "Package targets, tested runtimes, Native AOT coverage, and how Dekaf validates its public API surface."
---

# API and Runtime Compatibility

## Package assets and tested runtimes

The `Dekaf` and `Dekaf.Abstractions` packages ship `net10.0` and
`netstandard2.0` assets. .NET 10 applications select the optimized `net10.0`
asset; .NET 8 applications use the core's `netstandard2.0` asset. Compatibility
with .NET Standard alone is not a promise of testing on every implementing
runtime, identical API shapes or equivalent performance on all runtimes.

The other shipping packages target `net8.0` and `net10.0`: compression,
serialization, Schema Registry and its extensions (including KMS), dependency
injection, hosting, health checks, OpenTelemetry, outbox/EF Core and testing.
Those package requirements apply even when the core package could run on an
older runtime. The Avro POCO source generator targets `netstandard2.0` as a
compiler analyzer bundled in `Dekaf.SchemaRegistry.Avro`; it is not a separate
runtime package and does not lower the Avro package's runtime requirements.

Build the repository with the .NET 10 SDK; install the .NET 8 runtime (or SDK)
to execute the `net8.0` test binaries. The configured CI/release coverage is:

| Validation | Runtime / target | Broker versions |
| --- | --- | --- |
| Unit tests for code changes | .NET 8 and .NET 10 | No broker required |
| PR integration tests | .NET 10 | Kafka 4.3.1 |
| NuGet release integration gate | .NET 8 and .NET 10 | Kafka 4.0.2, 4.1.2, 4.2.1, 4.3.1 |
| Native AOT core and DI smoke apps | .NET 10, `linux-x64` | No broker required |
| PR Native AOT integration smoke subset | .NET 10, `linux-x64` | Kafka 4.3.1 |
| NuGet release Native AOT integration sweep | .NET 10, `linux-x64` | Kafka 4.0.2 and 4.3.1 |

Manual non-publishing CI runs also execute ordinary integration tests on both
.NET 8 and .NET 10. Native AOT coverage validates the scenarios exercised by
the smoke apps and integration suites; it does not establish support for
every package API, third-party serializer or target platform. Modern .NET
library assets enable AOT/trim analyzers. The `netstandard2.0` assets do not,
and third-party dependency warnings still need attention in an application's
own publish output.

The source of truth is the package project files, `src/Directory.Build.props`
and `.github/workflows/ci.yml`.

## Public API baselines

Every shipping project under `src/` uses
`Microsoft.CodeAnalysis.PublicApiAnalyzers`. Its `PublicAPI.Shipped*.txt` and
`PublicAPI.Unshipped*.txt` files make additions, removals, nullable annotations,
generic constraints, parameter names, and other signature changes visible in
code review. The `Dekaf` and `Dekaf.Abstractions` packages have separate `net10.0` and
`netstandard2.0` files because those assets intentionally expose a few
TFM-specific collection and ref-struct shapes. Other runtime packages share one
API baseline across `net8.0` and `net10.0`.

## Validate locally

Run the normal Release build to check source declarations:

```powershell
dotnet build --configuration Release
```

Run the gate self-test to verify every shipping project has baseline files and
that additions/removals fail with `RS0016`/`RS0017`:

```powershell
pwsh scripts/Test-PublicApiGate.ps1
```

Run package validation against the latest released package baseline:

```powershell
dotnet pack src/Dekaf/Dekaf.csproj --configuration Release
```

Package validation is inherited by every project under `src/`. It compares all
TFM assets with the released version in `DekafPackageVersion` and checks binary
breaks, parameter names, attributes, and compatible-framework surface drift.
Compatible additions are reviewed through the source declaration files rather
than treated as breaks against the previous release.

## Update a baseline

For an intentional additive API change, apply the `RS0016` code fix or add its
exact diagnostic signature to the matching `PublicAPI.Unshipped*.txt` file. For
an intentional removal, keep the old shipped entry and add the same entry to
the unshipped file with the `*REMOVED*` prefix. The PR must explain versioning
impact; breaking changes require explicit maintainer approval.

When preparing a release, promote accepted entries and retire removal markers:

```powershell
pwsh scripts/Promote-PublicApi.ps1
```

After release, update `DekafPackageVersion` to the exact newly published stable
version so future package validation compares against the immediate release.
Compatibility suppressions must name the affected package/member and document
maintainer approval plus the removal condition.
