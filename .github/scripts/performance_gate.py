"""Select, validate and merge BenchmarkDotNet A1/B/A2 evidence for the automatic performance gate.

The gate compares the maintained fixtures in tools/Dekaf.Benchmarks between a PR's merge base
and its head on one hosted VM. Fixtures are selected from the changed product paths; every
selected class uses BenchmarkDotNet's throughput strategy with [MemoryDiagnoser], so warmup
elapsed time, sample counts and allocations are BenchmarkDotNet's own measurements.
"""
import argparse
import json
import math
import re
import subprocess
import sys
from pathlib import Path


DEFAULT_ITERATIONS = 25
DEFAULT_MIN_WARMUP_SECONDS = 20
MAX_CASES = 48
SOURCE_SUFFIXES = ('.cs', '.csproj', '.props', '.targets', '.proto')
BUILD_INPUTS = ('Directory.Packages.props', 'Directory.Build.props', 'Directory.Build.targets', 'global.json')


def unit(*classes):
    """BenchmarkDotNet globs for whole unit benchmark classes (dot-delimited, exact class names)."""
    return [f'*.Unit.{name}.*' for name in classes]


CORE = unit('AccumulatorAppendBenchmarks', 'ConsumerHotPathBenchmarks',
            'FetchResponseParsingBenchmarks', 'ProduceResponseParsingBenchmarks')
TRACE = unit('TraceContextInjectionBenchmarks', 'TraceContextExtractionBenchmarks',
             'ProducerActivityDisabledBenchmarks', 'ConsumerActivityHotPathBenchmarks')

# Longest matching prefix wins per changed file. Only steady-state classes belong here: no
# [IterationSetup]/[IterationCleanup], fixed InvocationCount or ColdStart fixtures, because
# those cannot reach the elapsed warmup floor with the shared BDN settings.
AREAS = (
    ('src/Dekaf/Producer/', 'producer', unit(
        'AccumulatorAppendBenchmarks', 'AccumulatorAdmissionAppendBenchmarks', 'ProducerFireHotPathBenchmarks',
        'PartitionerBenchmarks', 'InflightTrackingBenchmarks', 'BrokerUnackedByteBudgetBenchmarks',
        'ValueTaskSourcePoolBenchmarks', 'ProduceResponseParsingBenchmarks', 'PartitionQueueAccountingBenchmarks',
        'WaveCoalesceProbeBenchmarks'), None),
    ('src/Dekaf/Consumer/', 'consumer', unit(
        'ConsumerHotPathBenchmarks', 'FetchResponseParsingBenchmarks', 'FetchRequestBuildBenchmarks',
        'OffsetStoreBenchmarks', 'PartitionedDispatchBenchmarks', 'KeyOrderedDispatchBenchmarks'), None),
    ('src/Dekaf/ShareConsumer/', 'share-consumer', unit(
        'ShareConsumerParsingBenchmarks', 'ShareAcknowledgementTrackingBenchmarks',
        'ShareConsumerPreparationReplayBenchmarks', 'ShareConsumerRenewalBenchmarks'), None),
    ('src/Dekaf/Networking/', 'networking', unit(
        'ReceiveLoopCopyBenchmark', 'ResponseFrameReaderBenchmarks', 'PendingRequestTableBenchmarks',
        'PipelinedResponseAllocationBenchmarks', 'KafkaConnectionCapabilitiesBenchmarks',
        'PooledHeaderReturnBenchmarks'), None),
    ('src/Dekaf/Protocol/', 'protocol', unit(
        'FetchResponseParsingBenchmarks', 'ProduceResponseParsingBenchmarks', 'ResponseFrameReaderBenchmarks',
        'Crc32CBenchmarks', 'ControlPlaneProtocolBenchmarks', 'RecordHeaderMaterializationBenchmarks'), None),
    ('src/Dekaf/Serialization/', 'serialization', unit(
        'CachingStringDeserializerBenchmarks', 'ConsumerHotPathBenchmarks'), None),
    ('src/Dekaf/Admin/', 'admin', unit(
        'ControlPlaneProtocolBenchmarks', 'AdminMultiGroupOffsetQueryBenchmarks', 'ListOffsetsProtocolBenchmarks',
        'DescribeTransactionsProtocolBenchmarks', 'OffsetTopicIdProtocolBenchmarks'), None),
    ('src/Dekaf/Metadata/', 'metadata', unit(
        'ControllerMetadataRefreshBenchmarks', 'TopicIdentityCheckBenchmarks', 'ProduceTopicCorrelationBenchmarks',
        'DnsPreferenceCacheBenchmarks'), None),
    ('src/Dekaf/Retry/', 'retry', unit(
        'ExponentialBackoffRetryPolicyBenchmarks', 'ProducerRetryWaitBenchmarks', 'TransactionRetryClockBenchmarks'), None),
    ('src/Dekaf/Security/', 'security', unit('OAuthBearerRefreshBenchmarks'), None),
    ('src/Dekaf/Telemetry/', 'telemetry', TRACE, None),
    ('src/Dekaf/Diagnostics/', 'telemetry', TRACE, None),
    ('src/Dekaf.OpenTelemetry/', 'telemetry', TRACE, None),
    ('src/Dekaf/Streams/', 'streams', unit('StreamsGroupHeartbeatProtocolBenchmarks'), None),
    ('src/Dekaf/Compression/', 'compression', [],
     'Compression fixtures (CompressionBenchmarks, CompressionCodecComparisonBenchmarks) use per-iteration '
     'setup/cleanup and cannot meet the elapsed warmup floor; measure them with performance-comparison.yml.'),
    ('src/Dekaf.Compression.', 'compression', [],
     'Compression fixtures use per-iteration setup/cleanup; measure them with performance-comparison.yml.'),
    ('src/Dekaf.Serialization.Routing/', 'routing', unit(
        'RoutingSerdeBenchmarks', 'HeaderRoutingLookupBenchmarks', 'HeaderRoutingParseBenchmarks'), None),
    ('src/Dekaf.Serialization.Json/', 'json-serialization', [],
     'SerializerBenchmarks uses [IterationSetup]; no steady-state JSON serializer fixture exists yet.'),
    ('src/Dekaf.SchemaRegistry.Avro', 'schema-registry-avro', unit('AvroSchemaRegistryMigrationBenchmarks'), None),
    ('src/Dekaf.SchemaRegistry.Protobuf/', 'schema-registry-protobuf', unit('ProtobufSchemaRegistrySerializerBenchmarks'), None),
    ('src/Dekaf.SchemaRegistry.Jsonata/', 'schema-registry-jsonata', unit('SchemaRegistryJsonataRuleBenchmarks'), None),
    ('src/Dekaf.SchemaRegistry.Json/', 'schema-registry-json', unit('SchemaRegistryPreparationBenchmarks'), None),
    ('src/Dekaf.SchemaRegistry.Kms.Aws/', 'kms-aws', unit('AwsKmsProviderBenchmarks'), None),
    ('src/Dekaf.SchemaRegistry.Kms.Azure/', 'kms-azure', unit('AzureKeyVaultKmsProviderBenchmarks'), None),
    ('src/Dekaf.SchemaRegistry.Kms.Vault/', 'kms-vault', unit('VaultKmsProviderBenchmarks'), None),
    ('src/Dekaf.SchemaRegistry.Kms.', 'kms', [], 'No benchmark fixture covers this KMS provider.'),
    ('src/Dekaf.SchemaRegistry/', 'schema-registry', unit(
        'SchemaRegistryPreparationBenchmarks', 'SchemaRegistryIdentityCacheBenchmarks',
        'SchemaRegistrySubjectCacheBenchmarks'), None),
    ('src/Dekaf.Testing/', 'testing', unit(
        'InMemoryPendingOffsetQueryBenchmarks', 'InMemoryDeliveryCallbackBenchmarks',
        'InMemoryAdminTimeoutBenchmarks', 'InMemoryConsumerGroupOffsetQueryBenchmarks'), None),
    ('src/Dekaf.Outbox', 'outbox', [],
     'Outbox has no unit fixture; use the outbox suites in performance-comparison.yml.'),
    ('src/Dekaf.Extensions.', 'extensions', [], 'Hosting/DI extensions are not on a measured hot path.'),
    ('src/Dekaf.Abstractions/', 'core', CORE, None),
    ('src/Dekaf/', 'core', CORE, None),
)


def _area_for(path):
    match = None
    for prefix, area, filters, reason in AREAS:
        if path.startswith(prefix) and (match is None or len(prefix) > len(match[0])):
            match = (prefix, area, filters, reason)
    return match


def select(changed_files):
    """Map changed product files to benchmark globs; order follows the first matching file."""
    areas, filters, not_applicable, considered = [], [], {}, []
    for path in changed_files:
        path = path.replace('\\', '/')
        if path in BUILD_INPUTS:
            match = (path, 'build-inputs', CORE, None)
        elif not path.startswith('src/') or not path.endswith(SOURCE_SUFFIXES):
            continue
        else:
            match = _area_for(path)
        if match is None:
            continue
        considered.append(path)
        _, area, area_filters, reason = match
        if reason:
            not_applicable.setdefault(area, reason)
            continue
        if area not in areas:
            areas.append(area)
        for item in area_filters:
            if item not in filters:
                filters.append(item)
    return {
        'applicable': bool(filters),
        'areas': areas,
        'filters': filters,
        'not_applicable': [{'area': area, 'reason': reason} for area, reason in not_applicable.items()],
        'considered_files': considered,
    }


def changed_files(base, head):
    output = subprocess.check_output(['git', 'diff', '--name-only', f'{base}..{head}'], text=True)
    return [line.strip() for line in output.splitlines() if line.strip()]


def count_listed_cases(listing):
    """Count listed methods; BDN --list flat does not expand parameter combinations."""
    return sum(1 for line in listing.splitlines() if line.strip().startswith('Dekaf.Benchmarks.'))


def count_execution_cases(log):
    """Read BDN's declared expanded total, including cases that later fail execution."""
    totals = re.findall(r'^// \*+ Found (\d+) benchmark\(s\) in total \*+\s*$', log, re.MULTILINE)
    if len(totals) != 1:
        raise ValueError('Expected exactly one BenchmarkDotNet expanded case total in the execution log')
    return int(totals[0])


def _case_key(benchmark):
    parts = [benchmark.get(field) for field in ('Namespace', 'Type', 'Method', 'Parameters')]
    return ' '.join(str(part) for part in parts if part not in (None, ''))


def _allocated(benchmark):
    for metric in benchmark.get('Metrics') or []:
        if (metric.get('Descriptor') or {}).get('Id') == 'Allocated Memory':
            return metric.get('Value')
    return None


def _warmup_seconds(benchmark):
    total = 0.0
    for measurement in benchmark.get('Measurements') or []:
        if measurement.get('IterationMode') == 'Workload' and measurement.get('IterationStage') == 'Warmup':
            total += float(measurement.get('Nanoseconds') or 0)
    return total / 1e9


def load_reports(directory):
    benchmarks = []
    for path in sorted(Path(directory).rglob('*-report-full.json')):
        report = json.loads(path.read_text(encoding='utf-8-sig'))
        benchmarks.extend(report.get('Benchmarks') or [])
    return sorted(benchmarks, key=_case_key)


def validate_phase(benchmarks, expected_count, iterations=None, min_warmup_seconds=0):
    """Return (fatal, warnings). Fatal issues mean the phase is not a complete measurement."""
    fatal, warnings = [], []
    if expected_count is not None and len(benchmarks) != expected_count:
        fatal.append(f'expected {expected_count} cases, found {len(benchmarks)}')
    keys = [_case_key(item) for item in benchmarks]
    if len(set(keys)) != len(keys):
        fatal.append('duplicate case identities in the report')
    for key, benchmark in zip(keys, benchmarks):
        statistics = benchmark.get('Statistics') or {}
        samples = statistics.get('N')
        mean = statistics.get('Mean')
        if not isinstance(mean, (int, float)) or isinstance(mean, bool) or not math.isfinite(mean) or mean <= 0:
            fatal.append(f'{key}: no finite positive mean')
        if iterations is not None and samples != iterations:
            fatal.append(f'{key}: {samples} measured iterations, expected {iterations}')
        allocated = _allocated(benchmark)
        if not isinstance(allocated, (int, float)) or isinstance(allocated, bool) or allocated < 0:
            fatal.append(f'{key}: no Allocated Memory metric (MemoryDiagnoser missing or failed)')
        warmup = _warmup_seconds(benchmark)
        if min_warmup_seconds and warmup < min_warmup_seconds:
            warnings.append(f'{key}: workload warmup {warmup:.1f} s below {min_warmup_seconds} s '
                            '(tiered code sped up after the pilot); the screen marks this case INCONCLUSIVE')
    return fatal, warnings


def estimate_minutes(cases, phases=3, seconds_per_case=90, fixed_minutes=25):
    return math.ceil(cases * phases * seconds_per_case / 60 + fixed_minutes)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    selecting = commands.add_parser('select', help='Select fixtures from changed files between two revisions')
    selecting.add_argument('--base', required=True)
    selecting.add_argument('--head', required=True)
    selecting.add_argument('--filters', help='Explicit space-separated BDN globs that replace path selection')
    selecting.add_argument('--output', type=Path, required=True)
    counting = commands.add_parser('count', help='Count listed methods, or expanded cases from an execution log')
    counting.add_argument('--max-cases', type=int, default=MAX_CASES)
    counting.add_argument('--execution-log', type=Path, help='Use the BDN declared case total instead of stdin method names')
    validating = commands.add_parser('validate', help='Validate one phase and write its merged case array')
    validating.add_argument('--phase', type=Path, required=True)
    validating.add_argument('--expected', type=int)
    validating.add_argument('--iterations', type=int, default=DEFAULT_ITERATIONS)
    validating.add_argument('--dry', action='store_true', help='Dry validation: cases and allocation metric only')
    validating.add_argument('--min-warmup', type=float, default=DEFAULT_MIN_WARMUP_SECONDS)
    validating.add_argument('--merged', type=Path)
    validating.add_argument('--cases', type=Path)
    args = parser.parse_args(argv)

    if args.command == 'select':
        if args.filters and args.filters.strip():
            filters = args.filters.split()
            selection = {'applicable': True, 'areas': ['explicit'], 'filters': filters, 'not_applicable': [],
                         'considered_files': []}
        else:
            selection = select(changed_files(args.base, args.head))
        selection.update(base=args.base, head=args.head)
        args.output.write_text(json.dumps(selection, indent=2) + '\n', encoding='utf-8')
        print(json.dumps(selection, indent=2))
        return 0
    if args.command == 'count':
        try:
            count = (count_execution_cases(args.execution_log.read_text(encoding='utf-8-sig'))
                     if args.execution_log else count_listed_cases(sys.stdin.read()))
        except ValueError as error:
            print(str(error), file=sys.stderr)
            return 1
        if count == 0:
            print('The selected filters match no benchmark cases', file=sys.stderr)
            return 1
        if count > args.max_cases:
            print(f'{count} cases exceed the {args.max_cases}-case budget '
                  f'(about {estimate_minutes(count)} minutes); dispatch with narrower filters', file=sys.stderr)
            return 1
        print(count)
        return 0
    benchmarks = load_reports(args.phase)
    fatal, warnings = validate_phase(
        benchmarks, args.expected, None if args.dry else args.iterations, 0 if args.dry else args.min_warmup)
    for warning in warnings:
        print(f'::warning::{args.phase.name}: {warning}')
    for issue in fatal:
        print(f'::error::{args.phase.name}: {issue}')
    if args.merged:
        args.merged.write_text(json.dumps(benchmarks) + '\n', encoding='utf-8')
    if args.cases:
        args.cases.write_text(json.dumps([_case_key(item) for item in benchmarks], indent=2) + '\n', encoding='utf-8')
    print(f'{args.phase.name}: {len(benchmarks)} cases, {len(fatal)} fatal issue(s), {len(warnings)} warning(s)')
    return 1 if fatal else 0


if __name__ == '__main__':
    raise SystemExit(main())
