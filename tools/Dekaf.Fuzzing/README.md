# Kafka protocol fuzzing

`Dekaf.Fuzzing` exposes one SharpFuzz 2.3.0 libFuzzer entry point covering both
`KafkaProtocolReader` primitives and every registered `IKafkaResponse` parser/version.
Inputs above 1 MiB are ignored so hostile length prefixes cannot turn one iteration into
unbounded allocation.

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

Expected parse failures are limited to `InsufficientDataException` (truncated input) and
`MalformedProtocolDataException` (invalid structure). Any other managed exception, access
violation, hang, or resource failure remains visible to libFuzzer as a crash.

## Build and replay corpora

```powershell
dotnet build tools/Dekaf.Fuzzing/Dekaf.Fuzzing.csproj --configuration Release --framework net10.0
dotnet build tests/Dekaf.Tests.Unit/Dekaf.Tests.Unit.csproj --configuration Release --framework net10.0
./tests/Dekaf.Tests.Unit/bin/Release/net10.0/Dekaf.Tests.Unit --treenode-filter "/*/*/(ProtocolReaderFuzzCorpusTests|ResponseParserFuzzCorpusTests)/*"
```

Replay tests load every embedded file under `Corpus/ProtocolReader` and `Corpus/Responses`, run
contiguous and segmented variants, and mutate response truncation, lengths, varints, and nested
payload bytes. Response corpus coverage is checked against the protocol registry so a new parser
or supported version cannot land without a seed.

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

Use `-max_total_time=<seconds>` for a bounded local run. Reproduce a crash without libFuzzer:

```powershell
dotnet ./tools/Dekaf.Fuzzing/bin/Release/net10.0/Dekaf.Fuzzing.dll /path/to/crash-input
```
