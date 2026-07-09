"""Shared utilities for stress test result reporting."""

from collections import defaultdict
from math import isfinite
from statistics import median

SCENARIO_TITLES = {
    'producer': 'Producer (Fire-and-Forget)',
    'producer-idempotent': 'Producer (Fire-and-Forget, Idempotent)',
    'producer-acks-all': 'Producer (Acks All)',
    'producer-async': 'Producer (Async)',
    'producer-async-idempotent': 'Producer (Async, Idempotent)',
    'producer-transactional': 'Producer (Transactional EOS)',
    'producer-roundtrip': 'Producer → Consumer Round-Trip',
    'consumer': 'Consumer',
    'consumer-batch': 'Consumer (Batch)',
    'consumer-raw': 'Consumer (Raw Bytes)',
    'consumer-raw-batch': 'Consumer (Raw Batch)',
}


def group_by_scenario(results):
    """Group results by (scenario, broker_count) tuple, split into producer and consumer."""
    scenario_groups = defaultdict(list)
    for r in results:
        broker_count = r.get('brokerCount', 1)
        key = (r.get('scenario', 'unknown'), broker_count)
        scenario_groups[key].append(r)

    producer = {k: v for k, v in scenario_groups.items() if 'producer' in k[0]}
    consumer = {k: v for k, v in scenario_groups.items() if 'consumer' in k[0]}
    return producer, consumer


def scenario_title(scenario_key, fallback_prefix=''):
    """Get the display title for a (scenario_name, broker_count) tuple key."""
    scenario, broker_count = scenario_key
    base_title = SCENARIO_TITLES.get(scenario, f"{fallback_prefix}({scenario})" if fallback_prefix else scenario)
    if broker_count > 1:
        return f"{base_title}, {broker_count} Brokers"
    return base_title


def format_bytes(num_bytes):
    """Format a byte count as a human-readable string."""
    if num_bytes is None:
        return 'N/A'
    if num_bytes < 1024:
        return f"{num_bytes} B"
    elif num_bytes < 1024 * 1024:
        return f"{num_bytes / 1024:.2f} KB"
    elif num_bytes < 1024 * 1024 * 1024:
        return f"{num_bytes / (1024 * 1024):.2f} MB"
    else:
        return f"{num_bytes / (1024 * 1024 * 1024):.2f} GB"


def cpu_micros_per_message(result):
    """CPU microseconds per message, derived when older result files omit it."""
    cpu_us_per_msg = result.get('cpuMicrosPerMessage')
    if cpu_us_per_msg is not None:
        return cpu_us_per_msg

    cpu_time_seconds = result.get('cpuTimeSeconds')
    total_messages = result.get('throughput', {}).get('totalMessages', 0)
    if cpu_time_seconds is None or not total_messages:
        return None
    return cpu_time_seconds * 1_000_000 / total_messages


def average_cores_used(result):
    """Average cores used, derived when older result files omit it."""
    cores_used = result.get('averageCoresUsed')
    if cores_used is not None:
        return cores_used

    cpu_time_seconds = result.get('cpuTimeSeconds')
    elapsed_seconds = result.get('throughput', {}).get('elapsedSeconds', 0)
    if cpu_time_seconds is None or not elapsed_seconds:
        return None
    return cpu_time_seconds / elapsed_seconds


def format_cpu_columns(result):
    """Format CPU-per-message / cores-used values, or '-' when not recorded."""
    cpu_us_per_msg = cpu_micros_per_message(result)
    cores_used = average_cores_used(result)
    return (
        f"{cpu_us_per_msg:.2f}" if cpu_us_per_msg is not None else '-',
        f"{cores_used:.2f}" if cores_used is not None else '-',
    )


def effective_rate(result):
    """Headline throughput. The selection policy (broker-confirmed delivered rate when
    measured, else the client-side tracker average) lives in StressTestResult and is
    serialized as effectiveMessagesPerSecond; the throughput fallback here only covers
    result files written before that field existed."""
    rate = result.get('effectiveMessagesPerSecond')
    if rate is not None:
        return rate
    return result.get('throughput', {}).get('averageMessagesPerSecond', 0)


def effective_mb_rate(result):
    rate = result.get('effectiveMegabytesPerSecond')
    if rate is not None:
        return rate
    return result.get('throughput', {}).get('averageMegabytesPerSecond', 0)


def accepted_messages_per_second(result):
    """Client-side mean rate for producer results with delivered throughput."""
    accepted = result.get('acceptedMessagesPerSecond')
    if accepted is not None:
        return accepted
    if result.get('deliveredMessages') is not None:
        return result.get('throughput', {}).get('averageMessagesPerSecond')
    return None


def median_interval_rate(result):
    """Median sampled client-side interval throughput, or None for old result files."""
    rate = result.get('medianIntervalMessagesPerSecond')
    if rate is not None:
        return rate

    samples = result.get('throughput', {}).get('messagesPerSecondSamples') or []
    finite_samples = [
        sample for sample in samples
        if isinstance(sample, (int, float)) and isfinite(sample)
    ]
    if not finite_samples:
        return None
    return median(finite_samples)


def comparison_rate(result):
    """Rate used for ranking and ratios: median interval rate when present, else headline rate."""
    rate = median_interval_rate(result)
    return rate if rate is not None else effective_rate(result)


def allocated_per_message(result):
    """Heap allocation per application message, or None when unavailable."""
    serialized = result.get('allocatedBytesPerMessage')
    if serialized is not None:
        return serialized

    gc = result.get('gcStats', {})
    allocated = gc.get('allocatedBytes')
    total_messages = result.get('throughput', {}).get('totalMessages', 0)
    if allocated is None or not total_messages:
        return None
    return allocated / total_messages


def find_confluent_baseline(results):
    """Find Confluent throughput as a baseline for ratio calculations."""
    for r in results:
        if r.get('client', '').lower() == 'confluent':
            rate = comparison_rate(r)
            if rate > 0:
                return rate
    return min((comparison_rate(r) or 1 for r in results), default=1)


def throughput_sort_key(result):
    """Median sampled throughput leads current reports; old results keep headline order."""
    cpu_us_per_msg = cpu_micros_per_message(result)
    return -comparison_rate(result), cpu_us_per_msg if cpu_us_per_msg is not None else float('inf')


def format_throughput_table(results, title, include_ratio=False):
    """Generate a markdown throughput table for a single scenario group."""
    if not results:
        return []

    lines = []
    duration = results[0].get('durationMinutes', 'N/A')
    message_size = results[0].get('messageSizeBytes', 'N/A')

    lines.append(f"## {title} ({duration} minutes, {message_size}B messages)")
    lines.append("")

    if include_ratio:
        lines.append("| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used | Comparison Ratio |")
        lines.append("|--------|------------|--------------|--------------|--------|----------------|--------|------------|------------------|")
    else:
        lines.append("| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |")
        lines.append("|--------|------------|--------------|--------------|--------|----------------|--------|------------|")

    baseline = find_confluent_baseline(results) if include_ratio else 0
    sorted_results = sorted(results, key=throughput_sort_key)

    for r in sorted_results:
        client = r.get('client', 'Unknown')
        throughput = r.get('throughput', {})
        msg_sec = effective_rate(r)
        mb_sec = effective_mb_rate(r)
        median_rate = median_interval_rate(r)
        median_msg_sec = f"{median_rate:,.0f}" if median_rate is not None else '-'
        accepted_rate = accepted_messages_per_second(r)
        accepted = f"{accepted_rate:,.0f}" if accepted_rate is not None else '-'
        errors = throughput.get('totalErrors', 0)
        cpu_us_per_msg, cores_used = format_cpu_columns(r)

        if include_ratio:
            ratio = comparison_rate(r) / baseline if baseline > 0 else 1.0
            lines.append(f"| {client} | {cpu_us_per_msg} | {msg_sec:,.0f} | {median_msg_sec} | {mb_sec:.2f} | {accepted} | {errors} | {cores_used} | {ratio:.2f}x |")
        else:
            lines.append(f"| {client} | {cpu_us_per_msg} | {msg_sec:,.0f} | {median_msg_sec} | {mb_sec:.2f} | {accepted} | {errors} | {cores_used} |")

    lines.append("")

    if any(median_interval_rate(r) is not None for r in results):
        lines.append("*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*")
        lines.append("")
        lines.append("*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*")
        lines.append("")

    if any(r.get('deliveredMessages') is not None for r in results):
        lines.append("*Messages/sec counts broker-confirmed deliveries (end-offset delta). "
                     "Accepted msg/s is the client-side append rate — a large gap means messages "
                     "were buffered or dropped without ever reaching the broker.*")
        lines.append("")

    return lines


def format_comparison_callout(results, title):
    """Generate Docusaurus admonition comparing Dekaf vs Confluent throughput."""
    dekaf = next((r for r in results if r.get('client', '').lower() == 'dekaf'), None)
    confluent = next((r for r in results if r.get('client', '').lower() == 'confluent'), None)

    if not (dekaf and confluent):
        return []

    dekaf_rate = comparison_rate(dekaf)
    confluent_rate = comparison_rate(confluent)
    if confluent_rate <= 0:
        return []

    lines = []
    throughput_ratio = dekaf_rate / confluent_rate
    dekaf_cpu = cpu_micros_per_message(dekaf)
    confluent_cpu = cpu_micros_per_message(confluent)
    label = title.lower()

    if dekaf_cpu is not None and confluent_cpu is not None and dekaf_cpu > 0 and confluent_cpu > 0:
        cpu_ratio = confluent_cpu / dekaf_cpu
        if cpu_ratio > 1.05:
            lines.append(":::tip")
            lines.append(f"**Dekaf uses {cpu_ratio:.2f}x less CPU per message** than Confluent.Kafka for {label}; comparison throughput is {throughput_ratio:.2f}x.")
            lines.append(":::")
        elif cpu_ratio < 0.95:
            lines.append(":::note")
            lines.append(f"Confluent.Kafka uses {1/cpu_ratio:.2f}x less CPU per message for {label}; comparison throughput is {throughput_ratio:.2f}x.")
            lines.append(":::")
        else:
            lines.append(":::note")
            lines.append(f"Dekaf and Confluent.Kafka have similar CPU efficiency for {label}; comparison throughput is {throughput_ratio:.2f}x.")
            lines.append(":::")
    elif throughput_ratio > 1.05:
        lines.append(":::tip")
        lines.append(f"**Dekaf is {throughput_ratio:.2f}x faster** than Confluent.Kafka for {label} comparison throughput!")
        lines.append(":::")
    elif throughput_ratio < 0.95:
        lines.append(":::note")
        lines.append(f"Confluent.Kafka is {1/throughput_ratio:.2f}x faster for {label} comparison throughput.")
        lines.append(":::")
    else:
        lines.append(":::note")
        lines.append(f"Dekaf and Confluent.Kafka have similar {label} comparison throughput.")
        lines.append(":::")

    lines.append("")
    return lines


def format_gc_table(results, title="GC Statistics"):
    """Generate a markdown GC statistics table."""
    if not results:
        return []

    lines = []
    lines.append(f"## {title}")
    lines.append("")
    lines.append("| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |")
    lines.append("|--------|----------|------|------|------|-----------------|-----------|")

    for r in sorted(results, key=lambda x: (x.get('client', ''), x.get('scenario', ''), x.get('brokerCount', 1))):
        client = r.get('client', 'Unknown')
        scenario_key = (r.get('scenario', 'Unknown'), r.get('brokerCount', 1))
        label = scenario_title(scenario_key)
        gc = r.get('gcStats', {})
        allocated = gc.get('allocatedBytes')
        alloc_str = format_bytes(allocated)
        alloc_msg = allocated_per_message(r)
        alloc_msg_str = format_bytes(round(alloc_msg)) if alloc_msg is not None else 'N/A'
        lines.append(f"| {client} | {label} | {gc.get('gen0Collections', 0)} | {gc.get('gen1Collections', 0)} | {gc.get('gen2Collections', 0)} | {alloc_str} | {alloc_msg_str} |")

    lines.append("")
    if any(r.get('client', '').lower() == 'confluent' for r in results):
        lines.append("*Confluent.Kafka uses native librdkafka; .NET GC allocation counters exclude unmanaged allocations.*")
        lines.append("")
    return lines


def format_transaction_verification(results):
    """Generate committed/aborted read_committed verification rows when present."""
    transactional = [result for result in results if result.get('transactionVerification') is not None]
    if not transactional:
        return []

    lines = [
        "### Transaction Verification",
        "",
        "| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |",
        "|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|",
    ]
    failure_samples = []

    for result in sorted(transactional, key=lambda item: item.get('client', '')):
        verification = result['transactionVerification']
        successful = verification.get('isSuccessful')
        if successful is None:
            successful = all(verification.get(field, 0) == 0 for field in (
                'duplicateMessages',
                'shortfallMessages',
                'leakedAbortedMessages',
                'unexpectedMessages',
                'missingSentinelPartitions',
            ))

        values = [
            result.get('client', 'Unknown'),
            f"{verification.get('acceptedMessages', 0):,}",
            f"{verification.get('committedMessages', 0):,}",
            f"{verification.get('abortedMessages', 0):,}",
            f"{verification.get('deliveredMessages', 0):,}",
            f"{verification.get('duplicateMessages', 0):,}",
            f"{verification.get('shortfallMessages', 0):,}",
            f"{verification.get('leakedAbortedMessages', 0):,}",
            f"{verification.get('unexpectedMessages', 0):,}",
            f"{verification.get('missingSentinelPartitions', 0):,}",
            'PASS' if successful else 'FAIL',
        ]
        lines.append(f"| {' | '.join(values)} |")
        failure_samples.extend(
            (result.get('client', 'Unknown'), sample)
            for sample in verification.get('failureSamples', [])
        )

    lines.append("")
    if failure_samples:
        lines.append("**Transaction verification failure samples:**")
        lines.extend(f"- {client}: {sample}" for client, sample in failure_samples)
        lines.append("")
    return lines


def format_roundtrip_validation_table(results):
    """Generate strict content/order validation results when present."""
    validation_results = [r for r in results if r.get('roundTripValidation') is not None]
    if not validation_results:
        return []

    lines = [
        "### Round-Trip Validation",
        "",
        "| Client | Expected | Consumed | Missing | Duplicates | Corrupt | Out of Order | Wrong Partition | Unexpected | Timed Out | Result |",
        "|--------|----------|----------|---------|------------|---------|--------------|-----------------|------------|-----------|--------|",
    ]

    for result in sorted(validation_results, key=lambda item: item.get('client', '')):
        validation = result['roundTripValidation']
        success = validation.get('isSuccess', False)
        lines.append(
            f"| {result.get('client', 'Unknown')} | "
            f"{validation.get('expectedMessages', 0):,} | "
            f"{validation.get('consumedMessages', 0):,} | "
            f"{validation.get('missingMessages', 0):,} | "
            f"{validation.get('duplicateMessages', 0):,} | "
            f"{validation.get('corruptMessages', 0):,} | "
            f"{validation.get('outOfOrderMessages', 0):,} | "
            f"{validation.get('mispartitionedMessages', 0):,} | "
            f"{validation.get('unexpectedMessages', 0):,} | "
            f"{'yes' if validation.get('timedOut', False) else 'no'} | "
            f"{'PASS' if success else 'FAIL'} |"
        )

    lines.append("")
    return lines


def generate_scenario_tables(results, include_ratio=False, include_callout=False):
    """Generate per-scenario throughput tables for all results."""
    producer_scenarios, consumer_scenarios = group_by_scenario(results)
    output = []

    for scenarios, fallback_prefix in [(producer_scenarios, 'Producer '), (consumer_scenarios, 'Consumer ')]:
        for scenario_key in sorted(scenarios):
            title = scenario_title(scenario_key, fallback_prefix)
            scenario_results = scenarios[scenario_key]
            output.extend(format_throughput_table(scenario_results, f"{title} Throughput", include_ratio=include_ratio))
            output.extend(format_transaction_verification(scenario_results))
            output.extend(format_roundtrip_validation_table(scenario_results))
            if include_callout:
                output.extend(format_comparison_callout(scenario_results, title))

    return output
