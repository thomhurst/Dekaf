"""Select, validate and merge BenchmarkDotNet A1/B/A2 evidence for the automatic performance gate.

The gate compares the maintained fixtures in tools/Dekaf.Benchmarks between a PR's merge base
and its head, with each bounded A1/B/A2 batch on one hosted VM. Fixtures are selected from
the changed product paths; every
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
MAX_BATCHES = 4
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
    ('src/Dekaf.Outbox.EntityFrameworkCore/OutboxCommitObserver.cs', 'outbox-save',
     unit('OutboxSaveChangesBenchmarks'), None),
    ('src/Dekaf.Outbox.EntityFrameworkCore/OutboxNotificationOptionsExtensions.cs', 'outbox-save',
     unit('OutboxSaveChangesBenchmarks'), None),
    ('src/Dekaf/Protocol/Messages/ShareFetchResponse.cs', 'share-consumer',
     unit('ShareFetchResponseDecodingBenchmarks'), None),
    # KafkaConsumer owns both fetching and the offset-store API. The separate
    # partition dispatcher has its own implementation; unknown files retain the
    # directory-wide fallback below.
    ('src/Dekaf/Consumer/KafkaConsumer.cs', 'consumer', unit(
        'ConsumerHotPathBenchmarks', 'ConsumerBatchInterceptorBenchmarks', 'ConsumerFollowerOffsetRetryBenchmarks', 'FetchResponseParsingBenchmarks', 'FetchRequestBuildBenchmarks',
        'OffsetStoreBenchmarks', 'ConsumeResultOffsetStoreBenchmarks'), None),
    ('src/Dekaf/Consumer/IKafkaConsumer.cs', 'consumer', unit('ConsumerHotPathBenchmarks', 'ConsumerBatchInterceptorBenchmarks'), None),
    ('src/Dekaf/Consumer/IConsumerInterceptor.cs', 'consumer', unit('ConsumerHotPathBenchmarks', 'ConsumerBatchInterceptorBenchmarks'), None),
    ('src/Dekaf/Consumer/ConsumeBatch.cs', 'consumer', unit('ConsumerHotPathBenchmarks', 'ConsumerBatchInterceptorBenchmarks'), None),
    ('src/Dekaf/Consumer/PartitionedProcessing.cs', 'consumer', unit(
        'PartitionedDispatchBenchmarks', 'KeyOrderedDispatchBenchmarks', 'PartitionedOffsetTrackingBenchmarks'), None),
    ('src/Dekaf/Consumer/CompletedOffsetRanges.cs', 'consumer', unit('PartitionedOffsetTrackingBenchmarks'), None),
    # Exercise real RecordBatch read/write and lazy slab lifetimes. CRC implementation
    # and unrelated administrative protocol fixtures belong to the protocol fallback.
    ('src/Dekaf/Protocol/Records/RecordBatch.cs', 'protocol',
     unit('ConsumerHotPathBenchmarks', 'ParsedRecordSlabLifecycleBenchmarks')
     + ['*.Unit.ProtocolBenchmarks.*RecordBatch*'], None),
    ('src/Dekaf/Consumer/PartitionMessageKey.cs', 'consumer', unit(
        'BinaryKeyDispatchBenchmarks', 'DistinctBinaryKeyDispatchBenchmarks',
        'SharedSuffixBinaryKeyDispatchBenchmarks'), None),
    ('src/Dekaf/Consumer/KeyOrderedPartitionDispatcher.cs', 'consumer', unit(
        'KeyOrderedDispatchBenchmarks', 'BinaryKeyDispatchBenchmarks',
        'DistinctBinaryKeyDispatchBenchmarks', 'SharedSuffixBinaryKeyDispatchBenchmarks'), None),
    ('src/Dekaf/Producer/', 'producer', unit(
        'AccumulatorAppendBenchmarks', 'AccumulatorAdmissionAppendBenchmarks', 'ProducerFireHotPathBenchmarks',
        'PartitionerBenchmarks', 'InflightTrackingBenchmarks', 'BrokerUnackedByteBudgetBenchmarks',
        'ValueTaskSourcePoolBenchmarks', 'ProduceResponseParsingBenchmarks', 'PartitionQueueAccountingBenchmarks',
        'WaveCoalesceProbeBenchmarks'), None),
    ('src/Dekaf/Consumer/', 'consumer', unit(
        'ConsumerHotPathBenchmarks', 'ConsumerBatchInterceptorBenchmarks', 'ConsumerFollowerOffsetRetryBenchmarks', 'FetchResponseParsingBenchmarks', 'FetchRequestBuildBenchmarks',
        'OffsetStoreBenchmarks', 'ConsumeResultOffsetStoreBenchmarks',
        'PartitionedDispatchBenchmarks', 'KeyOrderedDispatchBenchmarks'), None),
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
        'DescribeTransactionsProtocolBenchmarks', 'OffsetTopicIdProtocolBenchmarks',
        'ConsumerGroupAssignmentBenchmarks'), None),
    ('src/Dekaf/Metadata/', 'metadata', unit(
        'ControllerMetadataRefreshBenchmarks', 'ConsumerFollowerOffsetRetryBenchmarks', 'TopicIdentityCheckBenchmarks', 'ProduceTopicCorrelationBenchmarks',
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
     'setup/cleanup and cannot meet the elapsed warmup floor. Add steady-state coverage in '
     'tools/Dekaf.Benchmarks before performance acceptance.'),
    ('src/Dekaf.Compression.', 'compression', [],
     'Compression fixtures use per-iteration setup/cleanup. Add steady-state coverage in '
     'tools/Dekaf.Benchmarks before performance acceptance.'),
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
     'Outbox lacks mapped steady-state coverage. Add fixtures in tools/Dekaf.Benchmarks and map them '
     'here before performance acceptance; loaded scenarios also need the existing stress project.'),
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


def select(changed_files, base=None, head=None):
    """Map changed product files to benchmark globs; order follows the first matching file."""
    areas, filters, not_applicable, considered, scopes = [], [], {}, [], []
    for path in changed_files:
        path = path.replace('\\', '/')
        if path in BUILD_INPUTS:
            match = (path, 'build-inputs', CORE, None)
        elif not path.startswith('src/') or not path.endswith(SOURCE_SUFFIXES):
            continue
        else:
            match = _area_for(path)
            if (base and head and path == 'src/Dekaf/Consumer/KafkaConsumer.cs'
                    and _pending_fetch_only(base, head, path)):
                match = (path, 'consumer', unit('ConsumerHotPathBenchmarks',
                                              'ParsedRecordSlabLifecycleBenchmarks'), None)
                scopes.append({'path': path, 'reason': 'Only PendingFetchData changed; consumer fetch '
                               'scheduling and offset-store implementations are byte-for-byte unchanged.'})
            elif (base and head and path == 'src/Dekaf/Protocol/Records/RecordBatch.cs'
                  and _record_count_bound_only(base, head, path)):
                match = (path, 'protocol', unit('ConsumerHotPathBenchmarks', 'ParsedRecordSlabLifecycleBenchmarks')
                         + ['*.Unit.ProtocolBenchmarks.Read*RecordBatch*'], None)
                scopes.append({'path': path, 'reason': 'Only the instance RecordCountUpperBound getter was added; '
                               'retain read/lifecycle coverage. Unchanged write methods cannot call the new getter.'})
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
        'scoped_files': scopes,
    }


def _pending_fetch_only(base, head, path):
    """Narrow only this known top-level type; unfamiliar formatting keeps the broad map."""
    # This is a conservative boundary check for the maintained file, not a C# parser.
    # Nested braces are indented. Every byte outside this class must remain unchanged.
    pattern = re.compile(r'^internal sealed class PendingFetchData : IDisposable\n\{.*?^\}\n', re.M | re.S)
    remaining = []
    for revision in (base, head):
        try:
            source = subprocess.check_output(['git', 'show', f'{revision}:{path}'],
                                             text=True, stderr=subprocess.DEVNULL)
        except subprocess.CalledProcessError:
            return False
        matches = list(pattern.finditer(source))
        if len(matches) != 1:
            return False
        match = matches[0]
        remaining.append(source[:match.start()] + source[match.end():])
    return remaining[0] == remaining[1]


def _record_count_bound_only(base, head, path):
    """Recognize only an added instance getter, with no field/layout or existing-code edits."""
    diff = subprocess.check_output([
        'git', 'diff', '--no-ext-diff', '--unified=0', f'{base}..{head}', '--', path,
    ], text=True)
    added = []
    for line in diff.splitlines():
        if line.startswith(('+++', '---')):
            continue
        if line.startswith('-'):
            return False
        if line.startswith('+') and line[1:].strip() and not line[1:].lstrip().startswith('//'):
            added.append(line[1:])
    return bool(re.fullmatch(
        r'    internal int RecordCountUpperBound\n    \{\n'
        r'(?:        \[MethodImpl\(MethodImplOptions\.AggressiveInlining\)\]\n)?'
        r'        get => [^\n]+(?:\n            [^\n]+)*;\n    \}', '\n'.join(added)))


def changed_files(base, head):
    output = subprocess.check_output(['git', 'diff', '--name-only', f'{base}..{head}'], text=True)
    return [path for path in output.splitlines() if path and not _friend_assembly_only(base, head, path)]


def _friend_assembly_only(base, head, path):
    """Ignore only standalone friend-assembly declarations, never build settings or references."""
    if not path.startswith('src/') or not path.endswith('.csproj'):
        return False
    diff = subprocess.check_output([
        'git', 'diff', '--no-ext-diff', '--unified=0', f'{base}..{head}', '--', path,
    ], text=True)
    changes = [line[1:].strip() for line in diff.splitlines()
               if line.startswith(('+', '-')) and not line.startswith(('+++', '---'))]
    return bool(changes) and all(re.fullmatch(
        r'<InternalsVisibleTo Include="Dekaf\.(?:Tests(?:\.[A-Za-z0-9]+)*|Benchmarks|Profiling)"\s*/>',
        line) for line in changes)


def count_listed_cases(listing):
    """Count listed methods; BDN --list flat does not expand parameter combinations."""
    return sum(1 for line in listing.splitlines() if line.strip().startswith('Dekaf.Benchmarks.'))


def count_execution_cases(log):
    """Read BDN's declared expanded total, including cases that later fail execution."""
    totals = re.findall(r'^// \*+ Found (\d+) benchmark\(s\) in total \*+\s*$', log, re.MULTILINE)
    if len(totals) != 1:
        raise ValueError('Expected exactly one BenchmarkDotNet expanded case total in the execution log')
    return int(totals[0])


def check_listing(listing, filters, role):
    """Validate every requested glob, not just the union (BDN silently ignores empty globs)."""
    names = [line.strip() for line in listing.splitlines()
             if line.strip().startswith('Dekaf.Benchmarks.')]
    missing = []
    for pattern in filters:
        # BDN GlobFilter recognizes only * and ?, not fnmatch's [character classes].
        regex = re.compile('^' + re.escape(pattern).replace(r'\*', '.*').replace(r'\?', '.') + '$', re.I)
        if not any(regex.fullmatch(name) for name in names):
            missing.append(pattern)
    if missing:
        raise ValueError(f'{role}: filters match no benchmark methods: {", ".join(missing)}. '
                         'Land compatible fixtures on main before comparing, or correct the path map.')


def plan_batches(baseline, candidate, max_cases=MAX_CASES):
    """Partition original BDN full names; each batch will run its own same-VM A1/B/A2."""
    if not 1 <= max_cases <= MAX_CASES:
        raise ValueError(f'Batch size must be between 1 and {MAX_CASES}')
    if not baseline or not candidate:
        raise ValueError('Both revisions must have validated benchmark cases')
    cases = []
    for role, benchmarks in (('A', baseline), ('B', candidate)):
        keys = [_case_key(item) for item in benchmarks]
        if len(set(keys)) != len(keys):
            raise ValueError(f'{role}: duplicate benchmark identities')
        full_names = [item.get('FullName') for item in benchmarks]
        if any(not isinstance(name, str) or not name.startswith('Dekaf.Benchmarks.')
               or any(character in name for character in '*?\r\n') for name in full_names):
            raise ValueError(f'{role}: missing or non-literal BenchmarkDotNet FullName; cannot safely partition filters')
        if len({name.casefold() for name in full_names}) != len(full_names):
            raise ValueError(f'{role}: ambiguous BenchmarkDotNet FullName filters')
        cases.append(dict(zip(keys, full_names)))
    if cases[0] != cases[1]:
        baseline_only = sorted(set(cases[0]) - set(cases[1]))
        candidate_only = sorted(set(cases[1]) - set(cases[0]))
        renamed_filters = [f'{key}: A={cases[0][key]!r}, B={cases[1][key]!r}'
                           for key in sorted(cases[0].keys() & cases[1].keys())
                           if cases[0][key] != cases[1][key]]
        raise ValueError('A/B benchmark cases differ. '
                         f'Baseline only: {baseline_only}; candidate only: {candidate_only}. '
                         f'FullName differences: {renamed_filters}. '
                         'Land identical fixture coverage before comparison.')
    if len(baseline) > max_cases * MAX_BATCHES:
        raise ValueError(f'{len(baseline)} cases exceed {MAX_BATCHES} batches of {max_cases}; '
                         'select affected workloads with narrower filters before timing')
    ordered = sorted(cases[0])
    batches = []
    for start in range(0, len(ordered), max_cases):
        keys = ordered[start:start + max_cases]
        batches.append({'id': len(batches) + 1, 'cases': keys,
                        'filters': [cases[0][key] for key in keys]})
    return {'case_count': len(ordered), 'batches': batches}


def check_case_set(benchmarks, expected):
    actual = [_case_key(item) for item in benchmarks]
    if len(actual) != len(set(actual)) or sorted(actual) != sorted(expected):
        return [f'case set differs from plan; missing: {sorted(set(expected) - set(actual))}; '
                f'unexpected: {sorted(set(actual) - set(expected))}']
    return []


def summarize_batches(plan, directory):
    """Check complete coverage and combine verdicts only; never compare metrics across VMs."""
    screens = []
    for batch in plan['batches']:
        path = Path(directory) / f'batch-{batch["id"]}' / 'comparison.json'
        report = json.loads(path.read_text(encoding='utf-8'))
        actual = [item['case'] for item in report['cases']]
        if sorted(actual) != sorted(batch['cases']) or report['baseline_only'] or report['candidate_only']:
            raise ValueError(f'Batch {batch["id"]}: comparison does not cover exactly the planned cases')
        screen = report['screen']
        if screen not in ('PASS', 'REGRESSION', 'INCONCLUSIVE'):
            raise ValueError(f'Batch {batch["id"]}: unknown screen {screen}')
        print(f'Batch {batch["id"]}: {len(actual)} cases, {screen}')
        screens.append(screen)
    if not screens:
        raise ValueError('No measured batches')
    if 'REGRESSION' in screens:
        return 'REGRESSION'
    if 'INCONCLUSIVE' in screens:
        return 'INCONCLUSIVE'
    return 'PASS'


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
    count_budget = counting.add_mutually_exclusive_group()
    count_budget.add_argument('--max-cases', type=int, default=MAX_CASES)
    count_budget.add_argument('--discovery', action='store_true',
                              help='Use the combined batch budget for Dry discovery, not measurement')
    counting.add_argument('--execution-log', type=Path, help='Use the BDN declared case total instead of stdin method names')
    listing = commands.add_parser('check-list', help='Require each selected glob on one revision')
    listing.add_argument('--listing', type=Path, required=True)
    listing.add_argument('--selection', type=Path, required=True)
    listing.add_argument('--role', required=True)
    planning = commands.add_parser('plan', help='Partition identical Dry-validated cases into bounded A1/B/A2 jobs')
    planning.add_argument('--baseline', type=Path, required=True)
    planning.add_argument('--candidate', type=Path, required=True)
    planning.add_argument('--output', type=Path, required=True)
    planning.add_argument('--max-cases', type=int, default=MAX_CASES)
    summarizing = commands.add_parser('summarize', help='Require every planned batch and report the combined screen')
    summarizing.add_argument('--plan', type=Path, required=True)
    summarizing.add_argument('--results', type=Path, required=True)
    validating = commands.add_parser('validate', help='Validate one phase and write its merged case array')
    validating.add_argument('--phase', type=Path, required=True)
    validating.add_argument('--expected', type=int)
    validating.add_argument('--iterations', type=int, default=DEFAULT_ITERATIONS)
    validating.add_argument('--dry', action='store_true', help='Dry validation: cases and allocation metric only')
    validating.add_argument('--min-warmup', type=float, default=DEFAULT_MIN_WARMUP_SECONDS)
    validating.add_argument('--merged', type=Path)
    validating.add_argument('--cases', type=Path)
    validating.add_argument('--expected-cases', type=Path, help='Require these exact case identities, not just a count')
    args = parser.parse_args(argv)

    if args.command in ('check-list', 'plan', 'summarize'):
        try:
            if args.command == 'check-list':
                selection = json.loads(args.selection.read_text(encoding='utf-8'))
                check_listing(args.listing.read_text(encoding='utf-8-sig'), selection['filters'], args.role)
            elif args.command == 'plan':
                plan = plan_batches(load_reports(args.baseline), load_reports(args.candidate), args.max_cases)
                args.output.write_text(json.dumps(plan, indent=2) + '\n', encoding='utf-8')
                print(f'{plan["case_count"]} cases in {len(plan["batches"])} same-VM A1/B/A2 batch(es)')
            else:
                plan = json.loads(args.plan.read_text(encoding='utf-8'))
                screen = summarize_batches(plan, args.results)
                print(f'Micro screen: {screen}. Loaded pipeline evidence remains a separate requirement.')
                if screen == 'REGRESSION':
                    return 1
                if screen == 'INCONCLUSIVE':
                    print('::warning::Micro screen INCONCLUSIVE; review the named cases in the batch job summaries.')
        except (ValueError, OSError, KeyError) as error:
            print(f'::error::{error}', file=sys.stderr)
            return 1
        return 0

    if args.command == 'select':
        if args.filters and args.filters.strip():
            filters = args.filters.split()
            selection = {'applicable': True, 'areas': ['explicit'], 'filters': filters, 'not_applicable': [],
                         'considered_files': []}
        else:
            selection = select(changed_files(args.base, args.head), args.base, args.head)
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
        max_cases = MAX_CASES * MAX_BATCHES if args.discovery else args.max_cases
        if count > max_cases:
            print(f'{count} cases exceed the {max_cases}-case budget '
                  f'(about {estimate_minutes(count)} minutes); dispatch with narrower filters', file=sys.stderr)
            return 1
        print(count)
        return 0
    benchmarks = load_reports(args.phase)
    fatal, warnings = validate_phase(
        benchmarks, args.expected, None if args.dry else args.iterations, 0 if args.dry else args.min_warmup)
    if args.expected_cases:
        fatal.extend(check_case_set(benchmarks, json.loads(args.expected_cases.read_text(encoding='utf-8'))))
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
