---
sidebar_position: 4
---

# Compatibility

Dekaf currently targets `net10.0`.

The project is open to broader target-framework support when it does not regress the `net10.0` performance path. `netstandard2.0` support is tracked by #1224 and split into staged child issues so compatibility work can land without weakening the current package.

## Current Support

| Area | Status |
| --- | --- |
| Core package (`Dekaf`) | `net10.0` |
| Compression packages | `net10.0` |
| Serialization packages | `net10.0` |
| Schema Registry packages | `net10.0` |
| Dependency Injection, Hosting, Health Checks | `net10.0` |
| Testing package | `net10.0` |
| Tools, benchmarks, stress tests | `net10.0` |

The `net10.0` target stays the primary optimization target. Protocol serialization, production, and consumption hot paths should continue using modern BCL APIs where they are needed for low allocation and throughput.

## netstandard2.0 Goal

The compatibility goal is to let older applications consume Dekaf packages without requiring a second Kafka client package, while preserving the existing `net10.0` behavior and performance.

The intended path is staged:

1. Define the compatibility plan and blocker categories (#1298).
2. Establish a core `netstandard2.0` restore/build baseline (#1299).
3. Replace, guard, or polyfill core `net10.0` API blockers (#1300).
4. Add package and smoke validation for the final supported package set (#1301).

## Build Probe

The first compatibility probe forced the core package to compile as `netstandard2.0` without editing project files:

```powershell
dotnet build src/Dekaf/Dekaf.csproj --configuration Release `
  -p:TargetFrameworks=netstandard2.0 `
  -p:TargetFramework=netstandard2.0
```

That probe confirmed the work is cross-cutting and should not be shipped as one large PR.

## Blocker Categories

### Missing Package References

Several APIs are inbox for `net10.0` but require package references or replacement when targeting `netstandard2.0`:

- `System.IO.Pipelines`
- `System.Threading.Channels`
- `System.Text.Json`
- `System.Runtime.Intrinsics`
- hashing and runtime support packages used by core protocol paths

The first build child should add conditional package references only for targets that need them, leaving `net10.0` package closure unchanged where possible.

### Compiler Support Shims

The codebase uses modern C# features such as `init` and `required`. A `netstandard2.0` target needs compatibility definitions for compiler support types such as:

- `System.Runtime.CompilerServices.IsExternalInit`
- `System.Runtime.CompilerServices.RequiredMemberAttribute`
- `System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute`
- `System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute`

These shims should be internal, conditional, and included only for older target frameworks.

### net10-only API Usage

Some source paths use APIs that do not exist on `netstandard2.0`:

- span-based stream overrides such as `Stream.Write(ReadOnlySpan<byte>)`
- `System.Threading.Lock`
- `Task.WaitAsync`
- modern throw helpers
- selected runtime intrinsics and vectorized helpers

Each replacement needs performance review. The `net10.0` hot path should keep modern APIs when conditional compilation can isolate the compatibility path.

### Package Matrix

Not every package has to multi-target at the same time. The likely sequence is:

1. `Dekaf`
2. serialization and compression packages that can compile without framework-specific hosting dependencies
3. Schema Registry packages
4. extensions packages where their `Microsoft.Extensions.*` dependencies support the chosen older target
5. `Dekaf.Testing`

Tools, benchmarks, stress tests, and CI utilities should remain `net10.0`.

## Validation Requirements

Compatibility support is not complete until these checks exist:

- `dotnet build` for `net10.0` remains green.
- Packable libraries build for every declared target framework.
- A sample or smoke test references the `netstandard2.0` asset from a supported runtime.
- Unit tests continue running against `net10.0`.
- Any compatibility helper has focused tests or compile canaries.

Integration tests should keep using the existing runtime target unless a specific compatibility runtime issue requires a separate run.

## Non-goals

- Do not lower the runtime target for tools, benchmarks, or stress tests.
- Do not replace high-performance `net10.0` code with slower shared code when conditional compilation can keep the fast path.
- Do not claim `netstandard2.0` support until package restore, build, packaging, and smoke validation are all in place.
