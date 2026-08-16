---
sidebar_position: 5
---

# Public API Compatibility

Every shipping project under `src/` uses
`Microsoft.CodeAnalysis.PublicApiAnalyzers`. Its `PublicAPI.Shipped*.txt` and
`PublicAPI.Unshipped*.txt` files make additions, removals, nullable annotations,
generic constraints, parameter names, and other signature changes visible in
code review. The core `Dekaf` package has separate `net10.0` and
`netstandard2.0` files because those assets intentionally expose a few
TFM-specific collection and ref-struct shapes. Other packages share one API
baseline across `net8.0` and `net10.0`.

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
