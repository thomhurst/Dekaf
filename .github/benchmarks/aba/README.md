# Shared BenchmarkDotNet fixtures

Dispatch `performance-comparison.yml` with `suite=micro` and exact harness,
baseline and candidate SHAs; see [dispatch instructions](../README.md) and
[standard tooling/settings](../STANDARD-TOOLS.md). The workflow copies this
normal project into each product checkout. BenchmarkDotNet handles builds,
process isolation, timing, allocation measurement and exports. The copied
directory includes `Directory.Build.props`/`.targets` stop-files so the fixture
host does not import the product checkout's repository build settings.
The copied `Performance.sln` bounds BenchmarkDotNet's project discovery to this
directory, avoiding other `Dekaf.Benchmarks.csproj` files in the product checkout.

Fixture differences required for a fresh-main comparison:

- #3082: repeated and distinct binary keys plus string control; main's reference equality has a known correctness difference for separately fetched equal keys. Existing dispatch allocations remain measured.
- #3083: typed/raw traversal and completion-bound construction/parsing, with equal batch shapes.
- #3085: synchronous/pending relay cycles and actual publisher/direct-publisher controls, with listeners disabled/enabled. Main has no new Outbox instruments; the enabled baseline means an enabled listener with no corresponding instruments. Delegate binding adapts Task/ValueTask return types once during setup.
- #3086: successful, follower-error, leader-error and response-pool paths for foreground/prefetch. Main's follower-error offset reset is explicitly asserted as its known incorrect behavior; the candidate must retain the position. The two-response retry case is excluded because main cannot perform the same correct retry. Empty-fetch allocation and this semantic difference prevent interpreting faster incorrect behavior as acceptance.
- #3116: 1,024 records, no headers; synchronous/warm/cold parsing and retained traversal. Main has no borrowed API, so the Borrowed-named rows use its equivalent legacy parser; candidate rows use borrowed parsing. Both compute the same record/header checksum. Legacy rows remain as common controls. This is an API implementation comparison, not an assertion that main has the new API.
- #3117: sustained distinct and paired record/batch dispatch; identical lifecycle and zero-allocation probes, with main's existing allocation reported as measured.

The workflow screens every paired case against the declared tolerance (see [standard tooling](../STANDARD-TOOLS.md)); `REGRESSION` fails the job and `INCONCLUSIVE` permits one exact repeat. That screen is the acceptance result for changes within the micro scope. Whole-PR acceptance also requires correctness and any loaded evidence the change scope requires. Raw artifacts are retained for 90 days.
