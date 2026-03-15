"""Shared utilities for stress test result reporting."""

from collections import defaultdict

SCENARIO_TITLES = {
    'producer': 'Producer (Fire-and-Forget)',
    'producer-idempotent': 'Producer (Fire-and-Forget, Idempotent)',
    'producer-async': 'Producer (Async)',
    'producer-async-idempotent': 'Producer (Async, Idempotent)',
    'consumer': 'Consumer',
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
    if num_bytes < 1024:
        return f"{num_bytes} B"
    elif num_bytes < 1024 * 1024:
        return f"{num_bytes / 1024:.2f} KB"
    elif num_bytes < 1024 * 1024 * 1024:
        return f"{num_bytes / (1024 * 1024):.2f} MB"
    else:
        return f"{num_bytes / (1024 * 1024 * 1024):.2f} GB"


def find_confluent_baseline(results):
    """Find Confluent throughput as a baseline for ratio calculations."""
    for r in results:
        if r.get('client', '').lower() == 'confluent':
            rate = r.get('throughput', {}).get('averageMessagesPerSecond', 0)
            if rate > 0:
                return rate
    return min((r.get('throughput', {}).get('averageMessagesPerSecond', 1) for r in results), default=1)


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
        lines.append("| Client | Messages/sec | MB/sec | Total Messages | Errors | Ratio |")
        lines.append("|--------|--------------|--------|----------------|--------|-------|")
    else:
        lines.append("| Client | Messages/sec | MB/sec | Total | Errors |")
        lines.append("|--------|--------------|--------|-------|--------|")

    baseline = find_confluent_baseline(results) if include_ratio else 0
    sorted_results = sorted(results, key=lambda r: r.get('throughput', {}).get('averageMessagesPerSecond', 0), reverse=True)

    for r in sorted_results:
        client = r.get('client', 'Unknown')
        throughput = r.get('throughput', {})
        msg_sec = throughput.get('averageMessagesPerSecond', 0)
        mb_sec = throughput.get('averageMegabytesPerSecond', 0)
        total = throughput.get('totalMessages', 0)
        errors = throughput.get('totalErrors', 0)

        if include_ratio:
            ratio = msg_sec / baseline if baseline > 0 else 1.0
            lines.append(f"| {client} | {msg_sec:,.0f} | {mb_sec:.2f} | {total:,} | {errors} | {ratio:.2f}x |")
        else:
            lines.append(f"| {client} | {msg_sec:,.0f} | {mb_sec:.2f} | {total:,} | {errors} |")

    lines.append("")
    return lines


def format_comparison_callout(results, title):
    """Generate Docusaurus admonition comparing Dekaf vs Confluent throughput."""
    dekaf = next((r for r in results if r.get('client', '').lower() == 'dekaf'), None)
    confluent = next((r for r in results if r.get('client', '').lower() == 'confluent'), None)

    if not (dekaf and confluent):
        return []

    dekaf_rate = dekaf.get('throughput', {}).get('averageMessagesPerSecond', 0)
    confluent_rate = confluent.get('throughput', {}).get('averageMessagesPerSecond', 0)
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
    lines.append("| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |")
    lines.append("|--------|----------|------|------|------|-----------------|")

    for r in sorted(results, key=lambda x: (x.get('client', ''), x.get('scenario', ''))):
        client = r.get('client', 'Unknown')
        scenario = r.get('scenario', 'Unknown')
        gc = r.get('gcStats', {})
        alloc_str = format_bytes(gc.get('allocatedBytes', 0))
        lines.append(f"| {client} | {scenario} | {gc.get('gen0Collections', 0)} | {gc.get('gen1Collections', 0)} | {gc.get('gen2Collections', 0)} | {alloc_str} |")

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
