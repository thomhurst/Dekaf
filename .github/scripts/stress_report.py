"""Shared utilities for stress test result reporting."""

from collections import defaultdict
from datetime import datetime, timedelta
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

LATENCY_RATIO_THRESHOLD = 2.0
MAX_TIMELINE_ROWS = 100


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
    if result.get('isMessageBounded'):
        return None

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


def intra_run_throughput(result):
    """Return C#-computed intra-run metrics, or None for older result files."""
    if result.get('isMessageBounded'):
        return None

    numeric_fields = {
        'steadyStatePeakRatio': result.get('steadyStatePeakRatio'),
        'driftPercent': result.get('intraRunDriftPercent'),
        'slopePercentPerMinute': result.get('throughputSlopePercentPerMinute'),
        'steadyStatePeakRatioThreshold': result.get('steadyStatePeakRatioThreshold'),
        'slopePercentPerMinuteThreshold': result.get(
            'throughputSlopePercentPerMinuteThreshold'
        ),
    }
    if any(
        not isinstance(value, (int, float))
        or isinstance(value, bool)
        or not isfinite(value)
        for value in numeric_fields.values()
    ):
        return None

    boolean_fields = {
        'steadyStatePeakThresholdBreached': result.get(
            'steadyStatePeakThresholdBreached'
        ),
        'slopeThresholdBreached': result.get('throughputSlopeThresholdBreached'),
        'thresholdBreached': result.get('intraRunThroughputThresholdBreached'),
    }
    if any(not isinstance(value, bool) for value in boolean_fields.values()):
        return None

    return numeric_fields | boolean_fields


def paired_latency_thresholds(results):
    """Compare each Dekaf p50/p99 with its matching Confluent result."""
    baselines = {}
    for result in results:
        if str(result.get('client', '')).casefold() != 'confluent':
            continue
        latency = result.get('latency')
        if not isinstance(latency, dict):
            continue
        baselines[_latency_identity(result)] = latency

    evaluations = []
    for result in results:
        client = str(result.get('client', ''))
        if not client.casefold().startswith('dekaf'):
            continue
        latency = result.get('latency')
        if not isinstance(latency, dict):
            continue

        target_ms = result.get('deliveryLatencyTargetMs')
        p95_us = latency.get('p95Us')
        if _positive_finite_number(target_ms) and _positive_finite_number(p95_us):
            ratio = p95_us / (target_ms * 1_000)
            breached = ratio > 3.0
            evaluations.append({
                'scenario': _latency_scenario_label(result),
                'metric': 'latencyP95TargetRatio',
                'metricLabel': 'Delivery latency p95 / target',
                'current': ratio,
                'baselineCount': 0,
                'median': None,
                'mad': None,
                'lower': None,
                'upper': 3.0,
                'status': 'regression' if breached else 'stable',
                'repeatedRegression': False,
                'thresholdBreach': breached,
                'thresholdDirection': 'maximum',
                'latencyThreshold': True,
            })

        # Multi-connection Dekaf variants use their own throughput profile and have no
        # like-for-like Confluent pair, but the absolute configured target still applies.
        if client.casefold() != 'dekaf':
            continue

        baseline = baselines.get(_latency_identity(result))
        if baseline is None:
            continue

        for field, metric, label in (
            ('p50Us', 'latencyP50Ratio', 'Delivery latency p50 / Confluent'),
            ('p99Us', 'latencyP99Ratio', 'Delivery latency p99 / Confluent'),
        ):
            current = latency.get(field)
            reference = baseline.get(field)
            if (
                not _positive_finite_number(current)
                or not _positive_finite_number(reference)
            ):
                continue
            ratio = current / reference
            breached = ratio > LATENCY_RATIO_THRESHOLD
            evaluations.append({
                'scenario': _latency_scenario_label(result),
                'metric': metric,
                'metricLabel': label,
                'current': ratio,
                'baselineCount': 0,
                'median': None,
                'mad': None,
                'lower': None,
                'upper': LATENCY_RATIO_THRESHOLD,
                'status': 'regression' if breached else 'stable',
                'repeatedRegression': False,
                'thresholdBreach': breached,
                'thresholdDirection': 'maximum',
                'latencyThreshold': True,
            })
    return evaluations


def _positive_finite_number(value):
    return (
        isinstance(value, (int, float))
        and not isinstance(value, bool)
        and isfinite(value)
        and value > 0
    )


def _latency_identity(result):
    return (
        str(result.get('scenario', 'unknown')).casefold(),
        result.get('brokerCount', 1),
        result.get('durationMinutes'),
        result.get('messageSizeBytes'),
        _latency_consumer_seed_batch_size(result),
        _latency_roundtrip_messages(result),
    )


def _latency_consumer_seed_batch_size(result):
    value = result.get('consumerSeedBatchSizeBytes')
    if value is not None:
        return value
    scenario = str(result.get('scenario', '')).casefold()
    return 16_384 if scenario.startswith('consumer') else None


def _latency_roundtrip_messages(result):
    validation = result.get('roundTripValidation')
    if isinstance(validation, dict):
        return validation.get('expectedMessages', result.get('roundTripMessages'))
    return result.get('roundTripMessages')


def _latency_scenario_label(result):
    brokers = result.get('brokerCount', 1)
    return (
        f"{result.get('scenario', 'unknown')} / {result.get('client', 'unknown')} / "
        f"{brokers} broker{'s' if brokers != 1 else ''} / "
        f"{result.get('messageSizeBytes', '?')}B / {result.get('durationMinutes', '?')}m"
    )


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
    seed_batch_size = results[0].get('consumerSeedBatchSizeBytes')

    workload = f"{duration} minutes, {message_size}B messages"
    if seed_batch_size is not None:
        workload += f", {seed_batch_size:,}B seed batches"
    lines.append(f"## {title} ({workload})")
    lines.append("")

    if include_ratio:
        lines.append("| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used | Comparison Ratio |")
        lines.append("|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|------------------|")
    else:
        lines.append("| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |")
        lines.append("|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|")

    baseline = find_confluent_baseline(results) if include_ratio else 0
    sorted_results = sorted(results, key=throughput_sort_key)

    for r in sorted_results:
        client = r.get('client', 'Unknown')
        throughput = r.get('throughput', {})
        msg_sec = effective_rate(r)
        mb_sec = effective_mb_rate(r)
        median_rate = median_interval_rate(r)
        median_msg_sec = f"{median_rate:,.0f}" if median_rate is not None else '-'
        intra_run = intra_run_throughput(r)
        drift = f"{intra_run['driftPercent']:+.1f}%" if intra_run is not None else '-'
        slope = f"{intra_run['slopePercentPerMinute']:+.2f}%" if intra_run is not None else '-'
        accepted_rate = accepted_messages_per_second(r)
        accepted = f"{accepted_rate:,.0f}" if accepted_rate is not None else '-'
        errors = throughput.get('totalErrors', 0)
        cpu_us_per_msg, cores_used = format_cpu_columns(r)

        if include_ratio:
            ratio = comparison_rate(r) / baseline if baseline > 0 else 1.0
            lines.append(f"| {client} | {cpu_us_per_msg} | {msg_sec:,.0f} | {median_msg_sec} | {drift} | {slope} | {mb_sec:.2f} | {accepted} | {errors} | {cores_used} | {ratio:.2f}x |")
        else:
            lines.append(f"| {client} | {cpu_us_per_msg} | {msg_sec:,.0f} | {median_msg_sec} | {drift} | {slope} | {mb_sec:.2f} | {accepted} | {errors} | {cores_used} |")

    lines.append("")

    if any(median_interval_rate(r) is not None for r in results):
        lines.append("*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*")
        lines.append("")
        lines.append("*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*")
        lines.append("")

    intra_run_metrics = None
    for result in results:
        intra_run_metrics = intra_run_throughput(result)
        if intra_run_metrics is not None:
            break
    if intra_run_metrics is not None:
        peak_threshold = intra_run_metrics['steadyStatePeakRatioThreshold']
        slope_threshold = intra_run_metrics['slopePercentPerMinuteThreshold']
        lines.append(
            "*Drift compares last-third with first-third average throughput. "
            "Slope is the normalized least-squares trend; steady-state below "
            f"{peak_threshold:.0%} of peak or slope below {slope_threshold:g}%/min "
            "fails the regression gate.*"
        )
        lines.append("")

    if any(r.get('deliveredMessages') is not None for r in results):
        lines.append("*Messages/sec counts broker-confirmed deliveries (end-offset delta). "
                     "Accepted msg/s is the client-side append rate — a large gap means messages "
                     "were buffered or dropped without ever reaching the broker.*")
        lines.append("")

    return lines


def format_connection_scale_timeline(results, title):
    """Correlate producer connection changes with the nearest throughput interval."""
    rows = []
    for result in results:
        diagnostics = result.get('producerDeliveryDiagnostics') or {}
        for event in diagnostics.get('connectionScaleEvents') or []:
            rows.append((result, event))
    if not rows:
        return []

    rows.sort(key=lambda row: row[1].get('occurredAtUtc', ''))
    lines = [
        f"## Connection Scale Timeline - {title}",
        "",
        "| Client | Event UTC | Broker | Connections | Buffer | Pressure (buffer/send) | Nearest throughput sample |",
        "|--------|-----------|-------:|-------------|-------:|------------------------|---------------------------|",
    ]
    for result, event in rows:
        event_time = _parse_timestamp(event.get('occurredAtUtc'))
        samples = result.get('throughput', {}).get('intervalSamples') or []
        timestamped_samples = [
            (sample, _parse_timestamp(sample.get('capturedAtUtc')))
            for sample in samples
        ]
        timestamped_samples = [
            (sample, captured_at)
            for sample, captured_at in timestamped_samples
            if captured_at is not None
        ]
        nearest = min(
            timestamped_samples,
            key=lambda item: abs((item[1] - event_time).total_seconds()),
            default=(None, None),
        )[0] if event_time is not None else None
        sample_text = '-'
        if nearest is not None:
            sample_text = (
                f"{nearest.get('elapsedSeconds', 0):.1f}s / "
                f"{nearest.get('messagesPerSecond', 0):,.0f} msg/s"
            )
        lines.append(
            f"| {result.get('client', 'Unknown')} | "
            f"{event.get('occurredAtUtc', '-')} | {event.get('brokerId', '-')} | "
            f"{event.get('oldConnectionCount', '-')}→{event.get('newConnectionCount', '-')} | "
            f"{event.get('bufferUtilization', 0):.0%} | "
            f"{event.get('bufferPressureDelta', 0):,}/{event.get('sendLoopPressureDelta', 0):,} | "
            f"{sample_text} |"
        )
    lines.append("")
    return lines


def format_producer_request_diagnostics(results, title):
    """Show per-broker ProduceRequest rate and average size so fragmentation is visible."""
    rows = [
        (result, diagnostic)
        for result in results
        for diagnostic in (
            (result.get('producerDeliveryDiagnostics') or {}).get('brokerProduceRequests') or []
        )
    ]
    if not rows:
        return []

    rows.sort(key=lambda row: (row[0].get('client', ''), row[1].get('brokerId', 0)))
    lines = [
        f"## Producer Request Diagnostics - {title}",
        "",
        "| Client | Broker | Requests | Requests/s | Avg bytes/request |",
        "|--------|-------:|---------:|-----------:|------------------:|",
    ]
    for result, diagnostic in rows:
        lines.append(
            f"| {result.get('client', 'Unknown')} | {diagnostic.get('brokerId', '-')} | "
            f"{diagnostic.get('requestCount', 0):,} | "
            f"{diagnostic.get('requestsPerSecond', 0):.2f} | "
            f"{format_bytes(diagnostic.get('averageRequestBytes', 0))} |"
        )

    lines.extend([
        "",
        "*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*",
        "",
    ])
    return lines


def _sample_evenly(rows, maximum_rows):
    if len(rows) <= maximum_rows:
        return rows

    return [
        rows[round(index * (len(rows) - 1) / (maximum_rows - 1))]
        for index in range(maximum_rows)
    ]


def format_consumer_fetch_timeline(results, title):
    """Correlate consumer fetch diagnostics with throughput and connection reaps."""
    rows = [
        (result, sample)
        for result in results
        for sample in (result.get('consumerFetchDiagnostics') or {}).get('samples') or []
    ]
    if not rows:
        return []

    rows.sort(key=lambda row: row[1].get('capturedAtUtc', ''))
    displayed_rows = _sample_evenly(rows, MAX_TIMELINE_ROWS)
    omitted_rows = len(rows) - len(displayed_rows)
    lines = [
        f"## Consumer Fetch Timeline - {title}",
        "",
        "| Client | Interval end UTC | Fetch req/s | Bytes/fetch | Avg fetch RTT | Queues (pending / buffer / total) | Prefetched | Connection reaps | Nearest throughput |",
        "|--------|------------------|------------:|------------:|--------------:|----------------------------------:|-----------:|-----------------:|-------------------:|",
    ]

    for result, sample in displayed_rows:
        diagnostics = result.get('consumerFetchDiagnostics') or {}
        captured_at = _parse_timestamp(sample.get('capturedAtUtc'))
        timestamped_throughput = [
            (throughput, timestamp)
            for throughput in (result.get('throughput') or {}).get('intervalSamples') or []
            if (timestamp := _parse_timestamp(throughput.get('capturedAtUtc'))) is not None
        ]
        nearest = min(
            timestamped_throughput,
            key=lambda item: abs((item[1] - captured_at).total_seconds()),
            default=(None, None),
        )[0] if captured_at is not None else None
        throughput_text = '-'
        if nearest is not None:
            throughput_text = (
                f"{nearest.get('elapsedSeconds', 0):.0f}s / "
                f"{nearest.get('messagesPerSecond', 0):,.0f} msg/s"
            )

        interval_start = (
            captured_at - timedelta(seconds=sample.get('intervalSeconds', 0))
            if captured_at is not None else None
        )
        reaps = sum(
            1
            for reap in diagnostics.get('connectionReapEvents') or []
            if interval_start is not None
            and (occurred_at := _parse_timestamp(reap.get('occurredAtUtc'))) is not None
            and interval_start < occurred_at <= captured_at
        )
        reap_text = '-' if reaps == 0 else f"{reaps} reap{'s' if reaps != 1 else ''}"
        interval_end_text = (
            captured_at.strftime('%H:%M:%S')
            if captured_at is not None else sample.get('capturedAtUtc', '-')
        )

        lines.append(
            f"| {result.get('client', 'Unknown')} | {interval_end_text} | "
            f"{sample.get('fetchRequestsPerSecond', 0):.2f} | {format_bytes(sample.get('bytesPerFetch'))} | "
            f"{sample.get('averageFetchRttMs', 0):.2f}ms | "
            f"{sample.get('pendingFetchDepth', 0)} / {sample.get('prefetchBufferDepth', 0)} / "
            f"{sample.get('prefetchDepth', 0)} | {format_bytes(sample.get('prefetchedBytes'))} | "
            f"{reap_text} | {throughput_text} |"
        )

    if omitted_rows > 0:
        lines.append(
            f"*{omitted_rows:,} fetch sample(s) omitted; rows sampled across the full timeline.*"
        )

    lines.extend([
        "",
        "*Bytes/fetch is consumed message payload bytes divided by completed fetch requests for the interval; queue depths are point-in-time samples.*",
        "",
    ])
    return lines


def format_latency_outlier_timeline(results, title):
    """Correlate sampled delivery stalls with scaling, throughput, and GC."""
    rows = [
        (result, sample)
        for result in results
        for sample in (result.get('latency') or {}).get('outlierSamples') or []
    ]
    if not rows:
        return []

    rows.sort(key=lambda row: row[1].get('startedAtUtc', ''))
    lines = [
        f"## Delivery Latency Outliers - {title}",
        "",
        "| Client | Message | Started UTC | Latency | Correlated owner | Scale events in stall | Throughput interval | GC interval delta |",
        "|--------|--------:|-------------|--------:|------------------|-----------------------|---------------------|-------------------|",
    ]
    for result, outlier in rows:
        started_at = _parse_timestamp(outlier.get('startedAtUtc'))
        completed_at = _parse_timestamp(outlier.get('completedAtUtc'))
        scale_events = []
        for event in (result.get('producerDeliveryDiagnostics') or {}).get('connectionScaleEvents') or []:
            occurred_at = _parse_timestamp(event.get('occurredAtUtc'))
            if started_at is not None and completed_at is not None and occurred_at is not None:
                if started_at <= occurred_at <= completed_at:
                    scale_events.append(event)

        timestamped_samples = sorted(
            (
                (sample, captured_at)
                for sample in (result.get('throughput') or {}).get('intervalSamples') or []
                if (captured_at := _parse_timestamp(sample.get('capturedAtUtc'))) is not None
            ),
            key=lambda item: item[1],
        )
        completion_sample = None
        if completed_at is not None:
            completion_sample = next(
                (item for item in timestamped_samples if item[1] >= completed_at),
                min(
                    timestamped_samples,
                    key=lambda item: abs((item[1] - completed_at).total_seconds()),
                    default=None,
                ),
            )
        baseline_sample = None
        if started_at is not None:
            baseline_sample = next(
                (item for item in reversed(timestamped_samples) if item[1] <= started_at),
                None,
            )

        completion = completion_sample[0] if completion_sample else None
        baseline = baseline_sample[0] if baseline_sample else {}
        gen2_delta = max(0, (completion or {}).get('gen2Collections', 0) - baseline.get('gen2Collections', 0))
        pause_delta_ms = max(0, (completion or {}).get('gcPauseDurationMs', 0) - baseline.get('gcPauseDurationMs', 0))
        client_rate = result.get('medianIntervalMessagesPerSecond')
        if not isinstance(client_rate, (int, float)):
            client_rate = (result.get('throughput') or {}).get('averageMessagesPerSecond', 0)

        if scale_events:
            owner = 'connection transition'
        elif gen2_delta > 0 or pause_delta_ms >= 10:
            owner = 'GC pause'
        elif completion is not None and completion.get('messagesPerSecond', 0) < client_rate * 0.5:
            owner = 'throughput collapse'
        else:
            owner = 'broker/backlog (no scale or GC event)'

        scale_summary = '-'
        if scale_events:
            scale_summary = ', '.join(
                f"{event.get('brokerId', '-')}:{event.get('oldConnectionCount', '-')}→{event.get('newConnectionCount', '-')}"
                for event in scale_events
            )
        throughput_summary = '-'
        gc_summary = '-'
        if completion is not None:
            throughput_summary = (
                f"{completion.get('elapsedSeconds', 0):.1f}s / "
                f"{completion.get('messagesPerSecond', 0):,.0f} msg/s"
            )
            gc_summary = f"Gen2 +{gen2_delta:,} / pause +{pause_delta_ms:.1f}ms"
        latency_us = outlier.get('latencyUs', 0)
        latency_text = f"{latency_us / 1_000_000:.1f}s" if latency_us >= 1_000_000 else f"{latency_us / 1000:.1f}ms"
        message_index = outlier.get('messageIndex')
        message_text = '-' if message_index is None else f"{message_index:,}"
        lines.append(
            f"| {result.get('client', 'Unknown')} | {message_text} | "
            f"{outlier.get('startedAtUtc', '-')} | {latency_text} | {owner} | "
            f"{scale_summary} | {throughput_summary} | {gc_summary} |"
        )

    lines.append("")
    dropped = sum((result.get('latency') or {}).get('droppedOutlierSamples', 0) for result in results)
    if dropped > 0:
        lines.append(f"*{dropped:,} additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*")
        lines.append("")
    return lines


def _parse_timestamp(value):
    if not isinstance(value, str):
        return None
    try:
        return datetime.fromisoformat(value.replace('Z', '+00:00'))
    except ValueError:
        return None


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
            output.extend(format_connection_scale_timeline(scenario_results, title))
            output.extend(format_producer_request_diagnostics(scenario_results, title))
            output.extend(format_consumer_fetch_timeline(scenario_results, title))
            output.extend(format_latency_outlier_timeline(scenario_results, title))
            output.extend(format_transaction_verification(scenario_results))
            output.extend(format_roundtrip_validation_table(scenario_results))
            if include_callout:
                output.extend(format_comparison_callout(scenario_results, title))

    return output
