# Dekaf threat model

## What this project does and where untrusted input enters

Dekaf is a pure C# Apache Kafka client library (producer, consumer, share consumer, admin
client) plus optional Schema Registry, compression, serialization, outbox and hosting
integrations. It runs inside the application that references it. It is not a server.

Treat these as untrusted:

- **Kafka broker responses.** A malicious, compromised or impersonated broker (for example
  over plaintext, or after DNS or metadata redirection) controls every byte the client
  reads. The response frame reader and every response parser in `src/Dekaf/Protocol/`
  must reject malformed data without crashes, memory corruption, unbounded allocation or
  hangs.
- **Record data in fetch responses.** Record batches, record keys, values and headers, and
  compressed payloads (gzip, Snappy, LZ4, Zstandard, Brotli) come from whoever produced to
  the topic. The consumer decodes them (`src/Dekaf/Protocol/Records/`,
  `src/Dekaf/Compression/`, `src/Dekaf.Compression.*/`).
- **Deserializer input.** JSON, Avro, Protobuf and JSON Schema deserializers
  (`src/Dekaf.Serialization.*`, `src/Dekaf.SchemaRegistry.*`) decode record payloads
  written by other producers.
- **Schema Registry and KMS responses.** Schemas, schema references, rule sets (CEL,
  JSONata, field-level encryption) and key material returned over HTTP. Field-level
  encryption (CSFLE) must not leak plaintext or keys, and a malicious registry must not
  get code execution or unbounded resource use through schemas or rules.
- **Authentication exchanges.** SASL PLAIN, SCRAM-SHA-256/512, GSSAPI, OAUTHBEARER and AWS
  MSK IAM server messages (`src/Dekaf/Security/Sasl/`), and TLS server certificates.
  Look for credential disclosure, downgrade, and authentication bypass.

Treat these as trusted:

- Client configuration, builder options, credentials and certificates that the
  application supplies.
- The application's own records on the produce path. Producer bugs that corrupt or
  misroute data on the wire are still in scope as integrity issues.
- Databases behind the outbox stores (`src/Dekaf.Outbox.*`), apart from data that a
  broker or another producer controls.

## Components that matter most / least

Most important:

- `src/Dekaf/Protocol/` (`KafkaProtocolReader`, `ResponseFrameReader` in
  `src/Dekaf/Networking/`, message parsers in `Messages/`, `Records/`). This code uses
  `unsafe`, `Unsafe.*`, `MemoryMarshal` and pooled buffers for performance, so
  out-of-bounds reads or writes, use-after-return of pooled memory, double return to
  `ArrayPool`, and integer overflow in length or offset arithmetic are the top concern.
- Decompression wrappers in `src/Dekaf.Compression.*/`.
- SASL authenticators and TLS setup in `src/Dekaf/Security/`.
- Schema Registry client, rule handlers, CSFLE and KMS integrations.

Less important but in scope: admin client, outbox, hosting, health checks, OpenTelemetry,
dependency injection extensions.

Out of scope:

- `tests/`, `tools/` (benchmarks, stress tests, profiling, trace analyzer, CI pipeline),
  `samples/`, `docs/`. `tools/Dekaf.Fuzzing/` is in scope only as a way to exercise the
  library.
- `src/Dekaf.Testing/` (in-memory test doubles) and
  `src/Dekaf.SchemaRegistry.Avro.Poco.Generator/` (a build-time source generator that
  reads developer-supplied schemas).
- Bugs inside third-party packages (compression codecs, Apache.Avro, Google.Protobuf,
  cloud SDKs). Report them only when Dekaf calls them in an unsafe way or fails to bound
  their input or output.

## How to exercise it

The image builds everything under `/src` (Release, net10.0). No Kafka broker is available
during the scan, and integration tests need Docker, so use the unit tests and fuzz
targets instead.

- Unit tests: `dotnet test --project tests/Dekaf.Tests.Unit --configuration Release
  --framework net10.0 --no-build --treenode-filter "/*/Dekaf.Tests.Unit.Protocol/*/*"`.
  Filter segments are `/<Assembly>/<Namespace>/<Class>/<Test>`. Binary response fixtures
  live in `tests/Dekaf.Tests.Unit/Protocol/ResponseFixtures/`. Build protocol inputs with
  the library's own writers or the fixtures rather than hand-written bytes.
- Fuzz targets (`tools/Dekaf.Fuzzing/README.md` documents the input formats):

  ```sh
  DEKAF_FUZZ_TARGET=kafka-protocol /opt/dekaf-fuzz/tools/libfuzzer-dotnet \
    --target_path=dotnet --target_arg=/opt/dekaf-fuzz/kafka-protocol/Dekaf.Fuzzing.dll \
    -max_len=1048576 -timeout=30 -rss_limit_mb=2048 \
    tools/Dekaf.Fuzzing/Corpus/ProtocolReader tools/Dekaf.Fuzzing/Corpus/Responses

  DEKAF_FUZZ_TARGET=record-batch /opt/dekaf-fuzz/tools/libfuzzer-dotnet \
    --target_path=dotnet --target_arg=/opt/dekaf-fuzz/record-batch/Dekaf.Fuzzing.dll \
    -max_len=1048576 -timeout=30 -rss_limit_mb=2048 \
    tools/Dekaf.Fuzzing/Corpus/RecordBatch
  ```

  Copy a corpus directory before fuzzing so the checkout stays clean.

## How you rate severity

Use the levels in `SECURITY.md`:

- **Critical:** memory corruption (out-of-bounds write, use-after-free of pooled memory)
  reachable from a broker response, fetched record or registry response; arbitrary code
  execution; disclosure of SASL, OAuth, TLS or KMS credentials, or CSFLE plaintext or keys,
  to a party that should not see them.
- **High:** out-of-bounds read that discloses process memory to a remote party;
  authentication bypass or TLS validation bypass; a single malformed response or record
  that crashes the process (for example an uncatchable exception or stack overflow);
  silent data corruption or loss on the produce or consume path.
- **Moderate:** denial of service from one malicious broker or record that needs memory or
  CPU far out of proportion to the input (unbounded allocation from a length prefix,
  decompression bombs, infinite loops or hangs); issues that
  need uncommon configuration.
- **Low:** defense-in-depth gaps with no demonstrated impact; resource use that stays
  proportional to input size.

A malformed input that throws a normal, catchable exception (`MalformedProtocolDataException`,
`InsufficientDataException` or another `KafkaException`) and leaves the client usable is
expected behavior, not a vulnerability. Without a working reproducer, cap memory safety
findings at High.

## Anything to leave alone

- The client supports Kafka 4.0 and later only. Do not report missing support for older
  brokers or the classic consumer group protocol.
- Plaintext connections and SASL PLAIN without TLS are documented as insecure options that
  the application chooses. Do not report them unless the client downgrades from a TLS or
  stronger configuration on its own.
- Performance, allocation and style findings without a security impact.
- Reports should include a reproducer (a unit test or fuzz input) and a minimal patch that
  keeps the hot path allocation-free; see `AGENTS.md` for the performance rules.
