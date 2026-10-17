# Build image for the Anthropic OSS Scanner (https://github.com/anthropics/oss-scanner).
# The scanner builds this file with the repository as the build context, then scans the
# finished image with no network access. Everything the build, the unit tests and the
# fuzz targets need is fetched here.
FROM ubuntu:26.04

ENV DEBIAN_FRONTEND=noninteractive \
    DOTNET_ROOT=/usr/share/dotnet \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    PATH=/usr/share/dotnet:/root/.dotnet/tools:$PATH

RUN apt-get update && apt-get install -y --no-install-recommends \
      ca-certificates curl git jq libicu74 python3 \
    && rm -rf /var/lib/apt/lists/*

# Scanner contract: the checkout lives inside the image, at /src.
COPY . /src
WORKDIR /src

# global.json pins an SDK feature band that the stock dotnet/sdk images do not ship,
# so install exactly what global.json selects.
RUN curl --fail --location --silent --show-error https://dot.net/v1/dotnet-install.sh \
      --output /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --jsonfile /src/global.json --install-dir "$DOTNET_ROOT" \
    && rm /tmp/dotnet-install.sh \
    && dotnet --info

# The unit test project references every product package plus tools/Dekaf.Fuzzing, so
# building it restores and compiles all of src/. net10.0 matches PR CI.
RUN dotnet build tests/Dekaf.Tests.Unit/Dekaf.Tests.Unit.csproj \
      --configuration Release --framework net10.0

# Fuzzing harness, laid out like .github/workflows/protocol-fuzzing.yml (which owns the
# pinned tool versions). Each target is published and instrumented under
# /opt/dekaf-fuzz/<target>; see .oss-scanner/threat_model.md for how to run them.
RUN set -eu \
    && workflow=.github/workflows/protocol-fuzzing.yml \
    && libfuzzer_version="$(grep -m1 '^  LIBFUZZER_DOTNET_VERSION:' "$workflow" | cut -d"'" -f2)" \
    && libfuzzer_sha256="$(grep -m1 '^  LIBFUZZER_DOTNET_SHA256:' "$workflow" | cut -d"'" -f2)" \
    && sharpfuzz_version="$(jq -er '.tools["sharpfuzz.commandline"].version' .config/fuzzing/dotnet-tools.json)" \
    && mkdir -p /opt/dekaf-fuzz/tools \
    && dotnet tool install --tool-path /opt/dekaf-fuzz/tools SharpFuzz.CommandLine --version "$sharpfuzz_version" \
    && curl --fail --location --silent --show-error \
         "https://github.com/Metalnem/libfuzzer-dotnet/releases/download/${libfuzzer_version}/libfuzzer-dotnet-ubuntu" \
         --output /opt/dekaf-fuzz/tools/libfuzzer-dotnet \
    && echo "${libfuzzer_sha256}  /opt/dekaf-fuzz/tools/libfuzzer-dotnet" | sha256sum --check \
    && chmod +x /opt/dekaf-fuzz/tools/libfuzzer-dotnet \
    && for target in kafka-protocol record-batch; do \
         dotnet publish tools/Dekaf.Fuzzing/Dekaf.Fuzzing.csproj --configuration Release --framework net10.0 \
           --output "/opt/dekaf-fuzz/$target" ; \
       done \
    && /opt/dekaf-fuzz/tools/sharpfuzz /opt/dekaf-fuzz/kafka-protocol/Dekaf.dll \
    && for assembly in Dekaf.dll Dekaf.Compression.Brotli.dll Dekaf.Compression.Lz4.dll \
         Dekaf.Compression.Snappy.dll Dekaf.Compression.Zstd.dll; do \
         /opt/dekaf-fuzz/tools/sharpfuzz "/opt/dekaf-fuzz/record-batch/$assembly" ; \
       done

# Smoke-test the image without failing the build: the protocol and compression unit tests
# cover the parsers the scanner cares most about.
RUN dotnet test --project tests/Dekaf.Tests.Unit/Dekaf.Tests.Unit.csproj \
      --configuration Release --framework net10.0 --no-build \
      --treenode-filter "/*/(Dekaf.Tests.Unit.Protocol|Dekaf.Tests.Unit.Compression)/*/*" \
    || echo "WARNING: unit test smoke run reported failures"
