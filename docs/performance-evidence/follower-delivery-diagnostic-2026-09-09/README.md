# Follower recovery delivery diagnostic

**Local correctness evidence only; no performance acceptance.** With 200 real
Kafka records and one synthetic follower `OffsetOutOfRange` response, main
consumes offsets 0–13 and resumes at 200. The candidate resumes at 14 and consumes
all 200 records in order. The empty microbenchmark cannot expose this skipped
backlog because its requested offset and latest offset are both 42.

| Observation | Main | Candidate |
|---|---:|---:|
| Persisted records before consumption | 200 | 200 |
| Injected follower errors | 1 | 1 |
| Offset requested when error is injected | 14 | 14 |
| Next leader fetch offset | 200 | 14 |
| Latest-offset lookups after error | 1 | 0 |
| Records delivered to public consume loop | 14 | 200 |
| Complete ordered delivery | No | Yes |

The source validates payload against persisted offset, the full delivered
sequence, one injected error and the expected baseline/candidate outcome. The
independent [validation](validation.json) also checks the exact leader retry
offsets, lookup counts and copied product/dependency hashes. Full raw results
remain in [A-run.log](A-run.log) and [B-run.log](B-run.log).

## Identity and limits

- A: `a12b5980bb9ca9aa31755f3784c5255d0288be71`, using the saved product binary
  from [hosted artifact 10108596847](https://github.com/thomhurst/Dekaf/actions/runs/34358968465/artifacts/10108596847).
- B: locally built at `70add9d4ac21d3c130b152b7baa36aa43ed75006`, retained in its
  assembly informational version. Product source, SDK and dependency inputs are
  unchanged through PR #3086 head `70ceab239c280e6d228c7de11d400c4fb7737b48`.
  This is not an exact-current-head hosted performance comparison.
- SDK 10.0.401, runtime 10.0.12, Windows, Release; Dockerized Kafka 4.3.1.
  The broker image content ID, container creation time and ports are retained.
  The owned diagnostic broker was stopped and removed after recording results.
- The same [fixture source](Program.cs.txt) compiles against both libraries.
  One real broker supplies every successful record. A connection wrapper adds
  logical broker 2 to metadata and advertises it as the preferred read replica.
  On its first fetch above offset zero, the wrapper returns a synthetic empty
  error response before sending that request. Subsequent successful fetches use
  the real broker and protocol parser. This exercises public `ConsumeOneAsync`
  recovery; it does not reproduce a real follower's replication or failure.
- The log is a fixed 200-record backlog, not a concurrent advancing producer.
  A two-second empty poll ends the read loop. Baseline's recorded leader request
  at offset 200 and latest-offset lookup identify its reset, rather than inferring
  skipped data solely from a timeout. No application seek repairs that reset.
- Wrapper scheduling, synthetic error timing, setup allocations and local
  runtime conditions make these runs unsuitable for performance claims.
  No CPU, throughput, latency, allocation or stability gate passes from them.

## Replay and retained files

[manifest.json](manifest.json) maps original local file names to retained names
and SHA256 hashes. Raw source, both compiler logs/projects, reference manifests,
broker log and both results are byte-preserved with local `.gitattributes` rules.
The compiled local DLLs remain on the evidence volume; this report does not claim
that their bytes are archived in Git. Input references are distinguished from
DLLs copied into the output: framework assemblies resolve through
`Microsoft.AspNetCore.App`, and the manifest does not falsely claim every input
reference DLL was loaded.

To reproduce, build the pinned product revisions, place each product and its
dependencies in a separate `lib` directory, and use the retained source and
corresponding project (adjusting only absolute `HintPath` locations). Start a
single Kafka 4.3.1 broker advertising `localhost:19094`, with internal topics
configured for one replica. Use distinct empty topic names for each invocation:

```text
dotnet Dekaf.Benchmarks.dll UNIQUE_BASELINE_TOPIC false
dotnet Dekaf.Benchmarks.dll UNIQUE_CANDIDATE_TOPIC true
```

Each invocation creates its topic, produces 200 acknowledged records and checks
recovery. This diagnostic supplies correctness evidence missing from cached
empty responses. Representative loaded performance, advancing-log behavior,
actual delivery latency, client CPU and stability remain separate work.
