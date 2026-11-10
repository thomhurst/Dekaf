---
sidebar_position: 5
description: "Package targets, tested runtimes, Native AOT coverage, and how Dekaf validates its public API surface."
---

# API and Runtime Compatibility

See the [Kafka feature support matrix](kafka-feature-support.md) for KIP scope,
broker capabilities, runtime limits, and validation evidence.

## Package assets and tested runtimes

The `Dekaf` and `Dekaf.Abstractions` packages ship `net11.0`, `net10.0`, and
`netstandard2.0` assets. .NET 11 and .NET 10 applications select their own
optimized asset; older runtimes, including .NET 8, resolve `netstandard2.0`.
The `net11.0` assets are compiled with Runtime Async (`runtime-async=on`), a
preview feature of .NET 11. Compatibility
with .NET Standard alone is not a promise of testing on every implementing
runtime, identical API shapes or equivalent performance on all runtimes.

The other shipping packages target `net10.0` and `net11.0`: compression,
serialization, Schema Registry and its extensions (including KMS), dependency
injection, hosting, health checks, OpenTelemetry, outbox/EF Core and testing.
Those package requirements apply even when the core package could run on an
older runtime. The Avro POCO source generator targets `netstandard2.0` as a
compiler analyzer bundled in `Dekaf.SchemaRegistry.Avro`; it is not a separate
runtime package and does not lower the Avro package's runtime requirements.

Build the repository with the .NET 11 SDK; install the .NET 10 runtime (or SDK)
to execute the `net10.0` test binaries and tools. The configured CI/release coverage is:

| Validation | Runtime / target | Broker versions |
| --- | --- | --- |
| Unit tests for code changes | .NET 10 and .NET 11 | No broker required |
| PR integration tests | .NET 10 and .NET 11 | Kafka 4.4.0 |
| GSSAPI NuGet consumer round trips (Linux) | .NET 10 and .NET 11 | Kafka 4.4.0 |
| NuGet release integration gate | .NET 10 and .NET 11 | Kafka 4.0.2, 4.1.2, 4.2.1, 4.3.1, 4.4.0 |
| Native AOT core and DI smoke apps | .NET 10 (DI also .NET 11), `linux-x64` | No broker required |
| PR Native AOT integration smoke subset | .NET 10, `linux-x64` | Kafka 4.4.0 |
| NuGet release Native AOT integration sweep | .NET 10, `linux-x64` | Kafka 4.0.2 and 4.4.0 |

Native AOT coverage validates the scenarios exercised by
the smoke apps and integration suites; it does not establish support for
every package API, third-party serializer or target platform. Modern .NET
library assets enable AOT/trim analyzers. The `netstandard2.0` assets, which
retain reflection-based protocol metadata, do not,
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
`netstandard2.0` files; the `net11.0` assets share the `net10.0` declarations. These
assets intentionally expose a few
TFM-specific collection and ref-struct shapes. Other runtime packages share one
API baseline across `net10.0` and `net11.0`.

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
