"""Shared utilities for stress test result reporting."""

from collections import defaultdict

SCENARIO_TITLES = {
    'producer': 'Producer (Fire-and-Forget)',
    'producer-idempotent': 'Producer (Fire-and-Forget, Idempotent)',
    'producer-async': 'Producer (Async)',
    'producer-async-idempotent': 'Producer (Async, Idempotent)',
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


def format_cpu_columns(result):
    """Format the serialized CPU-per-message / cores-used values, or '-' when not recorded."""
    cpu_us_per_msg = result.get('cpuMicrosPerMessage')
    cores_used = result.get('averageCoresUsed')
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
            rate = effective_rate(r)
            if rate > 0:
                return rate
    return min((effective_rate(r) or 1 for r in results), default=1)


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
        lines.append("| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used | Ratio |")
        lines.append("|--------|--------------|--------|----------------|--------|------------|------------|-------|")
    else:
        lines.append("| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |")
        lines.append("|--------|--------------|--------|----------------|--------|------------|------------|")

    baseline = find_confluent_baseline(results) if include_ratio else 0
    sorted_results = sorted(results, key=effective_rate, reverse=True)

    for r in sorted_results:
        client = r.get('client', 'Unknown')
        throughput = r.get('throughput', {})
        msg_sec = effective_rate(r)
        mb_sec = effective_mb_rate(r)
        accepted_rate = r.get('acceptedMessagesPerSecond')
        accepted = f"{accepted_rate:,.0f}" if accepted_rate is not None else '-'
        errors = throughput.get('totalErrors', 0)
        cpu_us_per_msg, cores_used = format_cpu_columns(r)

        if include_ratio:
            ratio = msg_sec / baseline if baseline > 0 else 1.0
            lines.append(f"| {client} | {msg_sec:,.0f} | {mb_sec:.2f} | {accepted} | {errors} | {cpu_us_per_msg} | {cores_used} | {ratio:.2f}x |")
        else:
            lines.append(f"| {client} | {msg_sec:,.0f} | {mb_sec:.2f} | {accepted} | {errors} | {cpu_us_per_msg} | {cores_used} |")

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

    dekaf_rate = effective_rate(dekaf)
    confluent_rate = effective_rate(confluent)
    if confluent_rate <= 0:
        return []

    lines = []
    ratio = dekaf_rate / confluent_rate
    label = title.lower()

    if ratio > 1.05:
        lines.append(":::tip")
        lines.append(f"**Dekaf is {ratio:.2f}x faster** than Confluent.Kafka for {label} throughput!")
        lines.append(":::")
    elif ratio < 0.95:
        lines.append(":::note")
        lines.append(f"Confluent.Kafka is {1/ratio:.2f}x faster for {label} throughput.")
        lines.append(":::")
    else:
        lines.append(":::note")
        lines.append(f"Dekaf and Confluent.Kafka have similar {label} performance.")
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


def generate_scenario_tables(results, include_ratio=False, include_callout=False):
    """Generate per-scenario throughput tables for all results."""
    producer_scenarios, consumer_scenarios = group_by_scenario(results)
    output = []

    for scenarios, fallback_prefix in [(producer_scenarios, 'Producer '), (consumer_scenarios, 'Consumer ')]:
        for scenario_key in sorted(scenarios):
            title = scenario_title(scenario_key, fallback_prefix)
            scenario_results = scenarios[scenario_key]
            output.extend(format_throughput_table(scenario_results, f"{title} Throughput", include_ratio=include_ratio))
            if include_callout:
                output.extend(format_comparison_callout(scenario_results, title))

    return output
