# KafkaProtocolReader fuzzing

`Dekaf.Fuzzing` exposes a SharpFuzz 2.3.0 libFuzzer target for `KafkaProtocolReader` primitives. Byte 0 selects a read operation; its high bit selects a two-segment `ReadOnlySequence<byte>`. Remaining bytes are parser input. Inputs above 1 MiB are ignored so hostile length prefixes cannot turn one iteration into unbounded allocation.

Expected parse failures are limited to `InsufficientDataException` (truncated input) and `MalformedProtocolDataException` (invalid structure). Any other managed exception, access violation, hang, or resource failure remains visible to libFuzzer as a crash.

## Build and replay corpus

```powershell
dotnet build tools/Dekaf.Fuzzing/Dekaf.Fuzzing.csproj --configuration Release --framework net10.0
dotnet build tests/Dekaf.Tests.Unit/Dekaf.Tests.Unit.csproj --configuration Release --framework net10.0
./tests/Dekaf.Tests.Unit/bin/Release/net10.0/Dekaf.Tests.Unit --treenode-filter "/*/Dekaf.Tests.Unit.Protocol/ProtocolReaderFuzzCorpusTests/*"
```

The TUnit test loads every embedded file under `Corpus/ProtocolReader` and passes it through the same target used by libFuzzer.

## Run libFuzzer

Install the matching SharpFuzz instrumenter and download `libfuzzer-dotnet` for the current platform from its official release page. Publish into the ignored `artifacts` directory so instrumentation never modifies normal build output:

```powershell
dotnet tool install --global SharpFuzz.CommandLine --version 2.3.0
dotnet publish tools/Dekaf.Fuzzing/Dekaf.Fuzzing.csproj --configuration Release --framework net10.0 --output artifacts/fuzz/protocol-reader
sharpfuzz artifacts/fuzz/protocol-reader/Dekaf.dll
& /path/to/libfuzzer-dotnet --target_path=dotnet --target_arg=artifacts/fuzz/protocol-reader/Dekaf.Fuzzing.dll -max_len=1048576 tools/Dekaf.Fuzzing/Corpus/ProtocolReader
```

Use `-max_total_time=<seconds>` for a bounded local run. A crash file can be reproduced without libFuzzer:

```powershell
dotnet run --project tools/Dekaf.Fuzzing/Dekaf.Fuzzing.csproj --configuration Release --framework net10.0 -- /path/to/crash-input
```
