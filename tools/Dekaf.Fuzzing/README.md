# Kafka protocol fuzzing

`Dekaf.Fuzzing` exposes two SharpFuzz 2.3.0 libFuzzer targets:

- The default `kafka-protocol` target covers `KafkaProtocolReader` primitives and every
  registered `IKafkaResponse` parser/version.
- The `record-batch` target covers RecordBatch decoding, CRC32C validation, record/header
  parsing, and every registered decompression codec.

Inputs above 1 MiB are ignored so hostile length prefixes cannot turn one iteration into
unbounded allocation. RecordBatch decompression is additionally capped at 4 MiB per iteration.

Primitive inputs retain the original format: byte 0 selects a read operation, and its high bit
selects a two-segment `ReadOnlySequence<byte>`. Original operations retain their modulo-22
selectors; bit `0x40` selects later operations without retargeting old corpus files.

Response inputs use this stable header before the response body:

| Offset | Size | Value |
|---|---:|---|
| 0 | 1 | `0xFF` response marker |
| 1 | 1 | Flags; bit 0 selects a two-segment input |
| 2 | 2 | Big-endian Kafka API key |
| 4 | 2 | Big-endian API version |

RecordBatch inputs use byte 0 as an operation selector and the remaining bytes as the payload:

| Selector | Operation |
|---:|---|
| 0 | Decode a RecordBatch and enumerate its records and headers |
| 1 | Validate a RecordBatch CRC32C over contiguous or segmented input |
| 2 | None decompression |
| 3 | gzip decompression |
| 4 | Snappy decompression |
| 5 | LZ4 decompression |
| 6 | Zstandard decompression |
| 7 | Brotli decompression |

Selector bit `0x80` makes the payload a two-segment `ReadOnlySequence<byte>`.

Expected protocol failures are limited to typed truncation, malformed-data, unsupported-format,
invalid-compressed-data, truncated-stream, and output-limit exceptions. Any other managed
exception, access violation, hang, or resource failure remains visible to libFuzzer as a crash.

## Build and replay corpora

```powershell
dotnet build tools/Dekaf.Fuzzing/Dekaf.Fuzzing.csproj --configuration Release --framework net10.0
dotnet build tests/Dekaf.Tests.Unit/Dekaf.Tests.Unit.csproj --configuration Release --framework net10.0
./tests/Dekaf.Tests.Unit/bin/Release/net10.0/Dekaf.Tests.Unit --treenode-filter "/*/*/(ProtocolReaderFuzzCorpusTests|ResponseParserFuzzCorpusTests|RecordBatchFuzzCorpusTests)/*"
```

Replay tests load every embedded file under `Corpus/ProtocolReader`, `Corpus/Responses`, and
`Corpus/RecordBatch`. They run contiguous and segmented variants, including valid and corrupt
seeds for every compression codec plus hostile RecordBatch lengths, counts, CRCs, and truncation.
Response corpus coverage is checked against the protocol registry so a new parser or supported
version cannot land without a seed.

Response body fixtures named `<ResponseType>.v<Version>.bin` can be wrapped in selector headers
and regenerated with:

```powershell
dotnet ./tools/Dekaf.Fuzzing/bin/Release/net10.0/Dekaf.Fuzzing.dll --build-response-corpus <fixture-directory> tools/Dekaf.Fuzzing/Corpus/Responses
```

## Run libFuzzer

Install the matching SharpFuzz instrumenter and download `libfuzzer-dotnet` for the current
platform from its official release page. Publish into the ignored `artifacts` directory so
instrumentation never modifies normal build output:

```powershell
dotnet tool install --global SharpFuzz.CommandLine --version 2.3.0
dotnet publish tools/Dekaf.Fuzzing/Dekaf.Fuzzing.csproj --configuration Release --framework net10.0 --output artifacts/fuzz/protocol
sharpfuzz artifacts/fuzz/protocol/Dekaf.dll
& /path/to/libfuzzer-dotnet --target_path=dotnet --target_arg=artifacts/fuzz/protocol/Dekaf.Fuzzing.dll -max_len=1048576 tools/Dekaf.Fuzzing/Corpus/ProtocolReader tools/Dekaf.Fuzzing/Corpus/Responses
```

Run the RecordBatch/codec target from a separate publish directory and instrument every assembly
whose parser or codec implementation contributes coverage:

```powershell
dotnet publish tools/Dekaf.Fuzzing/Dekaf.Fuzzing.csproj --configuration Release --framework net10.0 --output artifacts/fuzz/record-batch
@(
  "Dekaf.dll"
  "Dekaf.Compression.Brotli.dll"
  "Dekaf.Compression.Lz4.dll"
  "Dekaf.Compression.Snappy.dll"
  "Dekaf.Compression.Zstd.dll"
) | ForEach-Object { sharpfuzz "artifacts/fuzz/record-batch/$_" }
$env:DEKAF_FUZZ_TARGET = "record-batch"
& /path/to/libfuzzer-dotnet --target_path=dotnet --target_arg=artifacts/fuzz/record-batch/Dekaf.Fuzzing.dll -max_len=1048576 tools/Dekaf.Fuzzing/Corpus/RecordBatch
Remove-Item Env:\DEKAF_FUZZ_TARGET
```

Use `-max_total_time=<seconds>` for a bounded local run. Reproduce a crash without libFuzzer:

```powershell
dotnet ./tools/Dekaf.Fuzzing/bin/Release/net10.0/Dekaf.Fuzzing.dll /path/to/crash-input
```

Set `DEKAF_FUZZ_TARGET=record-batch` before replaying a RecordBatch/codec crash input.

## Nightly campaigns and regression promotion

The `Protocol Fuzzing` GitHub Actions workflow runs both targets every night. Each target has a
15-minute total-time bound, a 30-second per-input timeout, a 2 GiB RSS bound, a 1 MiB input limit,
and a fixed seed recorded in `metadata.json`. Manual runs can override the total-time and RSS
bounds. The workflow summary lists any crash input and its target/seed, while the matching
`protocol-fuzz-<target>-seed-<seed>-<run-id>` artifact retains the metadata, full log, evolved
corpus, and crash files for 90 days.

To reproduce an artifact locally, build the target and pass the retained crash file directly to
the harness:

```powershell
dotnet build tools/Dekaf.Fuzzing/Dekaf.Fuzzing.csproj --configuration Release --framework net10.0
$env:DEKAF_FUZZ_TARGET = "record-batch" # Omit for kafka-protocol.
dotnet ./tools/Dekaf.Fuzzing/bin/Release/net10.0/Dekaf.Fuzzing.dll ./path/to/retained-crash
Remove-Item Env:\DEKAF_FUZZ_TARGET
```

Confirm the failure, minimize the input with libFuzzer when useful, then give it a descriptive,
stable filename and copy it into the reviewed regression corpus:

- `record-batch` inputs go under `Corpus/RecordBatch`.
- `kafka-protocol` inputs beginning with the `0xFF` response marker go under `Corpus/Responses`;
  other inputs go under `Corpus/ProtocolReader`.

Run the replay tests shown in [Build and replay corpora](#build-and-replay-corpora). Commit the new
input together with the parser fix and a focused assertion when the generic replay invariant does
not fully describe the regression. Normal pull-request CI reruns all three corpus replay suites in
the explicit `Replay protocol fuzz regression corpora` step, so promoted inputs become reviewable
and permanently guarded.
