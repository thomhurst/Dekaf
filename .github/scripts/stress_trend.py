"""Noise-aware regression detection for weekly stress-test results."""

import argparse
import json
from math import isfinite
from pathlib import Path
from statistics import median

from stress_report import (
    PAIRED_ORDER_LABELS,
    comparison_rate,
    cpu_micros_per_message,
    effective_rate,
    geometric_mean,
    intra_run_throughput,
    median_interval_rate,
    paired_order_identity,
    paired_latency_thresholds,
)


HISTORY_VERSION = 1
HISTORY_LIMIT = 10
MIN_BASELINE_RUNS = 3
MAD_MULTIPLIER = 2.0
RELATIVE_NOISE_FLOOR = 0.01
# A candidate whose delivered/median drain ratio falls more than this far below
# the same-run control's is treated as a delivery backlog, not a shared stall.
DRAIN_RATIO_DIVERGENCE = 0.85

_IDENTITY_FIELDS = (
    "scenario",
    "client",
    "brokerCount",
    "durationMinutes",
    "messageSizeBytes",
    "consumerSeedBatchSizeBytes",
    "consumerConnectionsPerBroker",
    "roundTripMessages",
    "roundTripSteadySeconds",
    "pairedClientOrder",
    "pairedSampleCount",
)

_METRICS = {
    "messagesPerSecond": {
        "label": "Messages/sec",
        # Trend on the same rate the docs tables rank on: the median sampled interval
        # throughput when the run recorded interval samples, else the whole-run mean.
        # A short shared stall depresses the mean of an otherwise steady run and
        # produced a false repeated-regression fail (issue #2467).
        "extract": comparison_rate,
        "lower_is_regression": True,
    },
    "cpuMicrosPerMessage": {
        "label": "CPU μs/msg",
        "extract": cpu_micros_per_message,
        "lower_is_regression": False,
    },
}


def _finite_number(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and isfinite(value)


def _history_metric_value(observation, metric):
    """Baseline value recorded by a stored history observation.

    Throughput prefers the additively stored median interval rate so trailing bands
    track the trended rate; entries written before that field existed fall back to
    the whole-run mean they recorded.
    """
    if metric == "messagesPerSecond":
        median_rate = median_interval_rate(observation)
        if median_rate is not None:
            return median_rate
    return observation.get(metric)


def _delivered_rate_if_backlogged(result, control):
    """Whole-run delivered mean when the candidate's flush drain lags its control.

    comparison_rate alone would hide delivery-backlog regressions: producer
    interval samples stop at the configured duration while
    EffectiveMessagesPerSecond keeps counting broker-confirmed deliveries
    through the flush drain, so a candidate that appends fast but delivers
    slowly shows a stable median with a collapsed delivered mean. A shared
    environment stall depresses the delivered/median ratio for BOTH clients
    proportionally (the #2467 false fail showed ~74% on each side), but a
    backlog depresses only the candidate's. When the candidate's ratio falls
    more than DRAIN_RATIO_DIVERGENCE below the same-run control's, trend the
    delivered mean instead so the backlog stays visible to the gate. Without a
    control (or without medians on either side) the median stands, matching
    the fail-open transition the issue chose.

    Only producer results with broker-confirmed deliveries qualify: consumer
    lanes have no deliveredMessages, so their effective rate is just the
    client-side whole-run average and a mean/median divergence there is a
    boundary-stall artifact, not a flush backlog.
    """
    if result.get("deliveredMessages") is None:
        return None
    median_rate = median_interval_rate(result)
    if median_rate is None or median_rate <= 0 or control is None:
        return None
    control_median = median_interval_rate(control)
    control_mean = effective_rate(control)
    if (
        control_median is None
        or control_median <= 0
        or not _finite_number(control_mean)
        or control_mean <= 0
    ):
        return None
    delivered = effective_rate(result)
    if not _finite_number(delivered) or delivered <= 0:
        return None

    candidate_drain = delivered / median_rate
    control_drain = control_mean / control_median
    if candidate_drain < control_drain * DRAIN_RATIO_DIVERGENCE:
        return delivered
    return None


def _roundtrip_messages(result):
    if result.get("roundTripSteadySeconds") is not None:
        return None
    validation = result.get("roundTripValidation")
    if isinstance(validation, dict):
        return validation.get("expectedMessages", result.get("roundTripMessages"))
    return result.get("roundTripMessages")


def _consumer_seed_batch_size(result):
    value = result.get("consumerSeedBatchSizeBytes")
    if value is not None:
        return value
    scenario = str(result.get("scenario", "")).casefold()
    return 16_384 if scenario.startswith("consumer") else None


def _consumer_connections_per_broker(result):
    value = result.get("consumerConnectionsPerBroker")
    if value is not None:
        return value

    scenario = str(result.get("scenario", "")).casefold()
    client = str(result.get("client", "")).casefold()
    if not scenario.startswith("consumer"):
        return None

    # Results written before the connection count became part of the performance
    # identity used the old two-connection Dekaf preset or Confluent's one connection.
    return 1 if client.startswith("confluent") else 2


# WARNING: Every component below — most notably the client label — is a history
# series key. Renaming a client label, or adding a new identity dimension (as the
# order-balanced pairedClientOrder did in #2055), creates a fresh series with zero
# history: the lane silently reports "Collecting baseline 0/3" and loses its
# trailing regression band until MIN_BASELINE_RUNS clean runs re-accumulate
# (issue #2468). If a label or identity dimension must change, either fold the new
# form back onto the old series (see _order_balanced_aggregates) or migrate
# .github/stress-history.json in the same PR.
def _order_family_key(result):
    """Series key ignoring client order: one family per (scenario, client, brokers, shape)."""
    return (
        str(result.get("scenario", "unknown")).casefold(),
        str(result.get("client", "unknown")).casefold(),
        result.get("brokerCount", 1),
        result.get("durationMinutes"),
        result.get("messageSizeBytes"),
        _consumer_seed_batch_size(result),
        _consumer_connections_per_broker(result),
        _roundtrip_messages(result),
        result.get("roundTripSteadySeconds"),
    )


def _identity(result):
    return _order_family_key(result) + (paired_order_identity(result),)


def _is_confluent(result):
    return str(result.get("client", "")).casefold().startswith("confluent")


def _pair_identity(result):
    """Match a Dekaf scenario to its same-run Confluent control.

    Client and consumer connection count are intentionally excluded: each client uses its
    own connection preset, while broker count and workload shape define the paired run.
    """
    return (
        str(result.get("scenario", "unknown")).casefold(),
        result.get("brokerCount", 1),
        result.get("durationMinutes"),
        result.get("messageSizeBytes"),
        _consumer_seed_batch_size(result),
        _roundtrip_messages(result),
        result.get("roundTripSteadySeconds"),
        paired_order_identity(result),
    )


# Synthetic order-balanced aggregates carry their trend metrics precomputed under
# this key because the _METRICS extractors read raw result fields that the
# synthetic rows do not have.
_AGGREGATE_METRICS_FIELD = "orderBalancedAggregateMetrics"


def _identity_record(result):
    """Identity fields normalized exactly as history observations store them."""
    record = {field: result.get(field) for field in _IDENTITY_FIELDS}
    record["brokerCount"] = result.get("brokerCount", 1)
    record["consumerSeedBatchSizeBytes"] = _consumer_seed_batch_size(result)
    record["consumerConnectionsPerBroker"] = _consumer_connections_per_broker(result)
    record["roundTripMessages"] = _roundtrip_messages(result)
    return record


def _metric_value(result, metric, definition):
    """Trend value for a result: precomputed for synthetic aggregates, extracted otherwise."""
    aggregate_metrics = result.get(_AGGREGATE_METRICS_FIELD)
    if aggregate_metrics is not None:
        return aggregate_metrics.get(metric)
    return definition["extract"](result)


def _complete_order_pair(samples):
    """The samples if they hold exactly one result per client order, else None.

    Duplicate samples for one order (e.g. merged result files) would weight
    that order more heavily in the geometric mean, so only an exactly balanced
    pair qualifies for aggregation.
    """
    order_counts = {}
    for sample in samples:
        order = str(sample.get("pairedClientOrder", "")).casefold()
        order_counts[order] = order_counts.get(order, 0) + 1
    if set(order_counts) != set(PAIRED_ORDER_LABELS):
        return None
    if any(count != 1 for count in order_counts.values()):
        return None
    return samples


def _order_balanced_aggregates(current_results):
    """Collapse each order-balanced sample pair into one synthetic trend result.

    Order-balanced acceptance (#2055) runs the flagship pairings once per client
    order, which puts pairedClientOrder into every sample's history identity and
    resets those lanes' trailing bands to zero (issue #2468). The trend series is
    therefore the geometric mean of the ordered pair, mirroring the aggregation the
    docs "Order-Balanced Aggregate" table introduced but applied to the trend
    metrics (deliberately effective_rate / CPU per message rather than the docs'
    comparison_rate — metric selection is owned by #2467). The synthetic row keeps
    a None order component so it attaches to the pre-rename plain Dekaf/Confluent
    history series and inherits the existing bands. Ordered samples become
    non-trended diagnostics for the aggregated metrics; unpaired and single-sample
    results (e.g. the "Dekaf (3conn)" control) are untouched.

    Returns (aggregates, aggregated_metrics) where aggregated_metrics maps each
    family's _order_family_key to the set of metric names its aggregate trends —
    a metric that failed to aggregate (missing/zero sample value) keeps trending
    on the per-order series instead of silently losing coverage.
    """
    groups = {}
    for result in current_results:
        if paired_order_identity(result) is not None:
            groups.setdefault(_order_family_key(result), []).append(result)

    aggregates = []
    aggregated_metrics = {}
    for key, samples in groups.items():
        if _complete_order_pair(samples) is None:
            # Anything other than exactly one sample per order — a partial pair,
            # a duplicated/merged result, or an unknown label — has no
            # well-defined balanced aggregate (the geomean would weight one
            # order more heavily), so the ordered samples keep trending on
            # their own series rather than silently losing regression coverage.
            continue

        metrics = {
            metric: geometric_mean(
                [definition["extract"](sample) for sample in samples]
            )
            for metric, definition in _METRICS.items()
        }
        trended = frozenset(
            metric for metric, value in metrics.items() if value is not None
        )
        if not trended:
            continue

        aggregates.append({
            **_identity_record(samples[0]),
            "pairedClientOrder": None,
            "pairedSampleCount": len(samples),
            _AGGREGATE_METRICS_FIELD: metrics,
        })
        aggregated_metrics[key] = trended

    return aggregates, aggregated_metrics


def _matching_observations(runs, result):
    key = _identity(result)
    is_aggregate = _AGGREGATE_METRICS_FIELD in result
    family_key = _order_family_key(result) if is_aggregate else None
    matches = []
    for run in runs:
        observation = next(
            (item for item in run.get("results", []) if _identity(item) == key),
            None,
        )
        if observation is None and is_aggregate:
            observation = _reconstructed_aggregate_observation(run, family_key)
        if observation is not None:
            matches.append(observation)
    return matches


def _reconstructed_aggregate_observation(run, family_key):
    """Geomean observation rebuilt from a historical run's ordered pair.

    Runs recorded between the #2055 order rename and the synthetic aggregate
    series carry the lane's data only as per-order observations. Rebuilding
    their geometric mean keeps the aggregate's baseline continuous across that
    window instead of skipping straight back to the last pre-rename entry.
    The synthetic observation deliberately carries no trend fields: it extends
    the baseline, but a stale pre-rename regression verdict must not pair with
    a fresh aggregate one across intervening ordered runs to fake a
    consecutive (repeatedRegression) failure. Metrics whose per-order series
    flagged a regression in that run are omitted so they cannot widen the
    clean baseline the trend filter otherwise excludes them from.
    """
    samples = [
        item
        for item in run.get("results", [])
        if paired_order_identity(item) is not None
        and _order_family_key(item) == family_key
    ]
    if not samples or _complete_order_pair(samples) is None:
        return None

    observation = {}
    for metric in _METRICS:
        if any(sample.get(f"{metric}Trend") == "regression" for sample in samples):
            continue
        value = geometric_mean([sample.get(metric) for sample in samples])
        if value is not None:
            observation[metric] = value
    if not observation:
        return None

    if any(sample.get("environmentShiftSuspected", False) for sample in samples):
        observation["environmentShiftSuspected"] = True
    return observation


def _matching_control_ratios(runs, result, metric):
    pair_key = _pair_identity(result)
    candidate_key = _identity(result)
    is_aggregate = _AGGREGATE_METRICS_FIELD in result
    family_key = _order_family_key(result) if is_aggregate else None
    trend_field = f"{metric}ControlRatioTrend"
    ratios = []

    for run in runs:
        if run.get("environmentShiftSuspected", False):
            continue
        matching_results = [
            item
            for item in run.get("results", [])
            if _pair_identity(item) == pair_key
            and not item.get("environmentShiftSuspected", False)
        ]
        candidate = next(
            (item for item in matching_results if _identity(item) == candidate_key),
            None,
        )
        control = next((item for item in matching_results if _is_confluent(item)), None)
        if candidate is None:
            if is_aggregate:
                ratio = _reconstructed_control_ratio(run, family_key, pair_key, metric)
                if ratio is not None:
                    ratios.append(ratio)
            continue

        candidate_value = _history_metric_value(candidate, metric)
        if (
            not _finite_number(candidate_value)
            or candidate.get(trend_field) == "regression"
        ):
            continue

        control_value = (
            _history_metric_value(control, metric) if control is not None else None
        )
        if not _finite_number(control_value) or control_value == 0:
            # Interim ordered runs carry the Confluent control only as an
            # ordered pair, which never matches an order-less pair key. Rebuild
            # its geomean so order-less candidates (e.g. the "Dekaf (3conn)"
            # control) keep a continuous ratio baseline across the rename
            # window too.
            control_value = _reconstructed_ordered_control_value(run, pair_key, metric)
        if control_value is None:
            continue

        ratios.append(candidate_value / control_value)

    return ratios[-HISTORY_LIMIT:]


def _reconstructed_control_ratio(run, family_key, pair_key, metric):
    """Candidate/control ratio rebuilt from a historical run's ordered pairs.

    Mirrors _reconstructed_aggregate_observation for the ratio series: interim
    ordered runs record the pairing only under per-order pair identities, which
    the synthetic aggregate's order-less pair key never matches. Rebuilding the
    ordered geomean ratio keeps the ratio baseline continuous across the rename
    window, so an aggregate regression is corroborated against the immediately
    preceding comparable ratios instead of only the stale pre-rename level.
    Runs whose per-order ratio series flagged a regression are skipped,
    matching the trend filter applied to directly recorded ratios.
    """
    candidate_samples = [
        item
        for item in run.get("results", [])
        if paired_order_identity(item) is not None
        and not item.get("environmentShiftSuspected", False)
        and _order_family_key(item) == family_key
    ]
    if _complete_order_pair(candidate_samples) is None:
        return None
    if any(
        sample.get(f"{metric}ControlRatioTrend") == "regression"
        for sample in candidate_samples
    ):
        return None

    candidate_value = geometric_mean(
        [sample.get(metric) for sample in candidate_samples]
    )
    control_value = _reconstructed_ordered_control_value(run, pair_key, metric)
    if candidate_value is None or control_value is None:
        return None
    return candidate_value / control_value


def _reconstructed_ordered_control_value(run, pair_key, metric):
    """Geomean control value rebuilt from a run's complete ordered Confluent pair.

    Requires a single unambiguous ordered control family matching the pair's
    shape; anything else (partial pair, duplicates, competing control
    variants) yields None so no ratio is fabricated.
    """
    shape_key = pair_key[:-1]
    control_samples = [
        item
        for item in run.get("results", [])
        if paired_order_identity(item) is not None
        and not item.get("environmentShiftSuspected", False)
        and _is_confluent(item)
        and _pair_identity(item)[:-1] == shape_key
    ]
    if len({_order_family_key(item) for item in control_samples}) != 1:
        return None
    if _complete_order_pair(control_samples) is None:
        return None
    value = geometric_mean([item.get(metric) for item in control_samples])
    return value if value else None


def _limit_observations_per_identity(runs):
    """Keep recent observations and clean metric baselines per configuration."""
    seen_counts = {}
    retained_baseline_counts = {}
    retained_runs = []

    for run in reversed(runs):
        retained_results = []
        for observation in reversed(run.get("results", [])):
            key = _identity(observation)
            seen_count = seen_counts.get(key, 0)
            seen_counts[key] = seen_count + 1

            baseline_keys = []
            for metric in _METRICS:
                baseline_key = (key, metric)
                if (
                    _finite_number(_history_metric_value(observation, metric))
                    and observation.get(f"{metric}Trend") != "regression"
                    and not observation.get("environmentShiftSuspected", False)
                    and retained_baseline_counts.get(baseline_key, 0) < HISTORY_LIMIT
                ):
                    baseline_keys.append(baseline_key)

            if seen_count >= HISTORY_LIMIT and not baseline_keys:
                continue

            for baseline_key in baseline_keys:
                retained_baseline_counts[baseline_key] = (
                    retained_baseline_counts.get(baseline_key, 0) + 1
                )
            retained_results.append(observation)

        if retained_results:
            retained_runs.append({
                **run,
                "results": list(reversed(retained_results)),
            })

    return list(reversed(retained_runs))


def _scenario_label(result):
    brokers = result.get("brokerCount", 1)
    label = (
        f"{result.get('scenario', 'unknown')} / {result.get('client', 'unknown')} / "
        f"{brokers} broker{'s' if brokers != 1 else ''} / "
        f"{result.get('messageSizeBytes', '?')}B / {result.get('durationMinutes', '?')}m"
    )
    roundtrip_messages = _roundtrip_messages(result)
    if roundtrip_messages is not None:
        label += f" / {roundtrip_messages} messages"
    order = paired_order_identity(result)
    if order is not None:
        label += f" / {order}"
    return label


def _metric_status(value, baseline, lower_is_regression):
    baseline_median = median(baseline)
    mad = median(abs(item - baseline_median) for item in baseline)
    half_width = max(
        MAD_MULTIPLIER * mad,
        abs(baseline_median) * RELATIVE_NOISE_FLOOR,
    )
    lower = baseline_median - half_width
    upper = baseline_median + half_width

    if lower <= value <= upper:
        status = "stable"
    elif value < lower:
        status = "regression" if lower_is_regression else "improvement"
    else:
        status = "improvement" if lower_is_regression else "regression"

    return status, baseline_median, mad, lower, upper


def _control_ratio_evaluation(baseline_runs, result, metric, definition, value, control_value):
    ratio = value / control_value
    ratio_history = _matching_control_ratios(baseline_runs, result, metric)
    evaluation = {
        "scenario": _scenario_label(result),
        "metric": f"{metric}ControlRatio",
        "metricLabel": f"{definition['label']} / Confluent",
        "current": ratio,
        "baselineCount": len(ratio_history),
        "median": None,
        "mad": None,
        "lower": None,
        "upper": None,
        "status": "insufficient-history",
        "repeatedRegression": False,
        "failureEligible": False,
        "corroboratesBaselineRegression": False,
    }
    if len(ratio_history) < MIN_BASELINE_RUNS:
        return evaluation

    status, center, mad, lower, upper = _metric_status(
        ratio,
        ratio_history,
        definition["lower_is_regression"],
    )
    evaluation.update({
        "median": center,
        "mad": mad,
        "lower": lower,
        "upper": upper,
        "status": status,
    })
    return evaluation


def evaluate_and_update(history, current_results, run_started_at):
    """Evaluate current results, append one compact run, and return failure state."""
    if history is not None and history.get("version") != HISTORY_VERSION:
        raise ValueError(f"Unsupported stress history version: {history.get('version')}")

    runs = history.get("runs", []) if history is not None else []
    if not isinstance(runs, list):
        raise ValueError("Stress history 'runs' must be a list")

    # A re-run of the same result set replaces its prior entry and never uses itself
    # as baseline data.
    baseline_runs = [run for run in runs if run.get("runStartedAtUtc") != run_started_at]
    evaluations = []
    observations = []
    should_fail = False
    environment_shift_suspected = False
    # Order-balanced pairs trend as one synthetic geomean aggregate per client
    # family (attached to the pre-rename plain series); the aggregates are
    # evaluated first so ordered diagnostics can borrow their family's verdict.
    aggregates, aggregated_metrics = _order_balanced_aggregates(current_results)
    trend_results = aggregates + list(current_results)
    controls = {
        _pair_identity(result): result
        for result in trend_results
        if _is_confluent(result)
    }
    family_throughput_regression = {}

    for result in trend_results:
        is_confluent = _is_confluent(result)
        is_aggregate = _AGGREGATE_METRICS_FIELD in result
        family_key = _order_family_key(result)
        # Ordered samples stay as diagnostics for the metrics their family's
        # aggregate trends: raw values are recorded, but they carry no band,
        # cannot gate, and never grow parallel per-order history series.
        diagnostic_metrics = (
            aggregated_metrics.get(family_key, frozenset())
            if not is_aggregate and paired_order_identity(result) is not None
            else frozenset()
        )
        prior = _matching_observations(baseline_runs, result)
        observation = _identity_record(result)
        if is_aggregate:
            observation["orderBalancedAggregate"] = True
        throughput_regression = False

        for metric, definition in _METRICS.items():
            value = _metric_value(result, metric, definition)
            backlog_substituted = False
            # Backlog substitution applies only to directly trended raw results:
            # synthetic aggregates have no raw delivery fields, and ordered
            # diagnostics must store the same comparison_rate their family's
            # aggregate geomeans.
            if (
                metric == "messagesPerSecond"
                and not is_confluent
                and not is_aggregate
                and metric not in diagnostic_metrics
            ):
                delivered = _delivered_rate_if_backlogged(
                    result, controls.get(_pair_identity(result))
                )
                if delivered is not None:
                    value = delivered
                    backlog_substituted = True
            if not _finite_number(value):
                continue

            if metric in diagnostic_metrics:
                observation[metric] = value
                continue

            trend_field = f"{metric}Trend"
            previous_trend = prior[-1].get(trend_field) if prior else None
            # Keep warned observations for consecutive-trend detection, but do not let
            # them widen the clean baseline used to judge the next run.
            history_values = [
                _history_metric_value(item, metric)
                for item in prior
                if item.get(trend_field) != "regression"
                and not item.get("environmentShiftSuspected", False)
            ]
            history_values = [
                item for item in history_values if _finite_number(item)
            ][-HISTORY_LIMIT:]

            evaluation = {
                "scenario": _scenario_label(result),
                "metric": metric,
                "metricLabel": definition["label"],
                "current": value,
                "baselineCount": len(history_values),
                "median": None,
                "mad": None,
                "lower": None,
                "upper": None,
                "status": "insufficient-history",
                "repeatedRegression": False,
                "failureEligible": False,
                "corroborated": None,
                "backlogDrainSubstituted": backlog_substituted,
            }

            if len(history_values) >= MIN_BASELINE_RUNS:
                status, center, mad, lower, upper = _metric_status(
                    value,
                    history_values,
                    definition["lower_is_regression"],
                )
                repeated = status == "regression" and previous_trend == "regression"
                evaluation.update({
                    "median": center,
                    "mad": mad,
                    "lower": lower,
                    "upper": upper,
                    "status": status,
                    "repeatedRegression": repeated,
                })

            if metric == "messagesPerSecond" and backlog_substituted:
                # The delivered mean was the trended value this run, so it must
                # also be the stored baseline value. Persisting the (inflated)
                # interval median would make _history_metric_value feed future
                # bands and ratio history a rate the gate never accepted,
                # ratcheting the baseline upward until the still-unchanged
                # delivered rate reads as a false repeated regression.
                observation[metric] = value
                observation["backlogDrainSubstituted"] = True
            elif metric == "messagesPerSecond" and median_interval_rate(result) is not None:
                # Store the median additively while the whole-run mean stays under
                # the existing key so pre-existing entries and their readers stay
                # valid.
                observation["medianIntervalMessagesPerSecond"] = median_interval_rate(result)
                observation[metric] = effective_rate(result)
            else:
                observation[metric] = value
            observation[f"{metric}Trend"] = evaluation["status"]

            ratio_evaluation = None
            control = controls.get(_pair_identity(result)) if not is_confluent else None
            control_value = (
                _metric_value(control, metric, definition) if control is not None else None
            )
            if _finite_number(control_value) and control_value != 0:
                ratio_evaluation = _control_ratio_evaluation(
                    baseline_runs,
                    result,
                    metric,
                    definition,
                    value,
                    control_value,
                )
                ratio_metric = ratio_evaluation["metric"]
                observation[ratio_metric] = ratio_evaluation["current"]
                observation[f"{ratio_metric}Trend"] = ratio_evaluation["status"]
                evaluation["corroborated"] = ratio_evaluation["status"] == "regression"
                evaluation["failureEligible"] = (
                    evaluation["repeatedRegression"] and evaluation["corroborated"]
                )
                ratio_evaluation["corroboratesBaselineRegression"] = evaluation[
                    "failureEligible"
                ]
            elif not is_confluent:
                evaluation["failureEligible"] = evaluation["repeatedRegression"]

            if is_confluent and evaluation["status"] == "regression":
                environment_shift_suspected = True
                evaluation["environmentShiftSuspected"] = True

            evaluations.append(evaluation)
            if ratio_evaluation is not None:
                evaluations.append(ratio_evaluation)
            should_fail = should_fail or evaluation["failureEligible"]
            if metric == "messagesPerSecond":
                throughput_regression = evaluation["status"] == "regression"

        if is_aggregate:
            family_throughput_regression[family_key] = throughput_regression
        elif "messagesPerSecond" in diagnostic_metrics:
            # Ordered samples have no per-order band, so intra-run breaches borrow
            # the family aggregate's throughput verdict for corroboration.
            throughput_regression = family_throughput_regression.get(family_key, False)

        intra_run = intra_run_throughput(result)
        if intra_run is not None:
            slope_breached = intra_run["slopeThresholdBreached"]
            threshold_metrics = (
                (
                    "steadyStatePeakRatio",
                    "Steady-state / peak",
                    intra_run["steadyStatePeakRatio"],
                    intra_run["steadyStatePeakRatioThreshold"],
                    intra_run["steadyStatePeakThresholdBreached"],
                ),
                (
                    "slopePercentPerMinute",
                    "Slope %/min",
                    intra_run["slopePercentPerMinute"],
                    intra_run["slopePercentPerMinuteThreshold"],
                    intra_run["slopeThresholdBreached"],
                ),
            )
            for metric, label, value, threshold, breached in threshold_metrics:
                trend_field = f"{metric}Trend"
                previous = prior[-1] if prior else {}
                previous_trend = previous.get(trend_field)
                previous_value = previous.get(metric)
                if (
                    previous_trend is None
                    and isinstance(previous_value, (int, float))
                    and isfinite(previous_value)
                ):
                    previous_trend = (
                        "regression" if previous_value < threshold else "stable"
                    )
                status = "regression" if breached else "stable"
                repeated = status == "regression" and previous_trend == "regression"
                corroborated = (
                    breached
                    if metric == "slopePercentPerMinute"
                    else slope_breached or throughput_regression
                )
                failure_eligible = (
                    breached and repeated and corroborated and not is_confluent
                )
                evaluations.append({
                    "scenario": _scenario_label(result),
                    "metric": metric,
                    "metricLabel": label,
                    "current": value,
                    "baselineCount": 0,
                    "median": None,
                    "mad": None,
                    "lower": threshold,
                    "upper": None,
                    "status": status,
                    "repeatedRegression": repeated,
                    "thresholdBreach": breached,
                    "corroborated": corroborated,
                    "failureEligible": failure_eligible,
                })
                observation[metric] = value
                observation[trend_field] = status
                should_fail = should_fail or failure_eligible

        observations.append(observation)

    # Environment shifts model runner-wide noise: one regressed Confluent control makes
    # every observation from the same run unsafe for future absolute baselines.
    if environment_shift_suspected:
        for observation in observations:
            observation["environmentShiftSuspected"] = True

    latency_evaluations = paired_latency_thresholds(current_results)
    evaluations.extend(latency_evaluations)
    should_fail = should_fail or any(
        item['thresholdBreach'] for item in latency_evaluations
    )

    current_run = {
        "runStartedAtUtc": run_started_at,
        "results": observations,
    }
    if environment_shift_suspected:
        current_run["environmentShiftSuspected"] = True
    updated_runs = baseline_runs + [current_run]
    updated = {
        "version": HISTORY_VERSION,
        "runs": _limit_observations_per_identity(updated_runs),
    }
    return evaluations, updated, should_fail


def format_markdown(evaluations):
    lines = [
        "## Stress Trend Analysis",
        "",
        (
            f"Current metrics and paired Dekaf/Confluent ratios are compared with up to "
            f"{HISTORY_LIMIT} matching runs. "
            f"The noise band is trailing median ± max({MAD_MULTIPLIER:g}×MAD, "
            f"{RELATIVE_NOISE_FLOOR:.0%} of median); "
            "a second consecutive adverse excursion becomes a failure candidate."
        ),
        (
            "Paired Dekaf baseline regressions fail only when the same-run ratio also "
            "regresses; unpaired scenarios retain the consecutive-regression gate. "
            "Confluent regressions remain environment warnings."
        ),
        (
            "Messages/sec trends on the median sampled interval rate when the run "
            "recorded interval samples (the rate the docs tables rank on); results "
            "and history entries without samples fall back to the whole-run mean. "
            "If a candidate's delivered/median drain ratio falls more than "
            f"{1 - DRAIN_RATIO_DIVERGENCE:.0%} below its same-run control's, the "
            "delivered whole-run mean is trended instead so delivery backlogs "
            "hidden by the duration-window median stay visible."
        ),
        (
            "Order-balanced sample pairs (run once per client order) trend as a single "
            "geometric-mean series per client that continues the pre-rename history "
            "band; the individual ordered samples appear only as intra-run diagnostics."
        ),
    ]

    if any(item.get("environmentShiftSuspected") for item in evaluations):
        lines.extend([
            "",
            "> Environment shift suspected: Confluent control regressed beyond its trailing band; "
            "this run is excluded from absolute baselines.",
        ])

    lines.extend([
        "",
        "| Scenario | Metric | Current | Baseline median | Band | Status |",
        "|----------|--------|--------:|----------------:|------|--------|",
    ])

    labels = {
        "insufficient-history": "Collecting baseline",
        "stable": "Within band",
        "improvement": "Improvement",
        "regression": "Regression",
    }

    for item in evaluations:
        if "thresholdBreach" in item:
            center = "-"
            if item.get("thresholdDirection") == "maximum":
                band = f"<= {item['upper']:,.2f}"
            else:
                band = f">= {item['lower']:,.2f}"
        elif item["median"] is None:
            center = "-"
            band = f"{item['baselineCount']}/{MIN_BASELINE_RUNS} runs"
        else:
            center = f"{item['median']:,.2f}"
            band = f"{item['lower']:,.2f} – {item['upper']:,.2f}"

        status = labels[item["status"]]
        if item.get("thresholdBreach"):
            if item.get("latencyThreshold"):
                status = "Threshold breach (fail)"
            elif item.get("failureEligible"):
                status = "Repeated threshold breach (fail)"
            elif item.get("corroborated") is False:
                status = "Threshold breach (uncorroborated warning)"
            else:
                status = "Threshold breach (warning)"
        elif item.get("environmentShiftSuspected"):
            status = "Environment shift suspected (warning)"
        elif item["repeatedRegression"]:
            if item.get("failureEligible"):
                status = "Repeated regression (fail)"
            elif item.get("corroborated") is False:
                status = "Repeated regression (control-normalized warning)"
            else:
                status = "Repeated regression (warning)"
        elif item.get("corroboratesBaselineRegression"):
            status = "Regression (corroborates fail)"
        elif item["status"] == "regression":
            status = "Regression (warning)"

        lines.append(
            f"| {item['scenario']} | {item['metricLabel']} | {item['current']:,.2f} | "
            f"{center} | {band} | {status} |"
        )

    lines.append("")
    return "\n".join(lines)


def _annotation_escape(value):
    return str(value).replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")


def emit_annotations(evaluations):
    for item in evaluations:
        if item["status"] not in {"regression", "improvement"}:
            continue

        if item.get("thresholdBreach"):
            level = "error" if item.get("failureEligible", True) else "warning"
            prefix = (
                "Latency threshold breach"
                if item.get("latencyThreshold")
                else "Intra-run threshold breach"
            )
        elif item.get("environmentShiftSuspected"):
            level = "warning"
            prefix = "Environment shift suspected"
        elif item["repeatedRegression"]:
            level = "error" if item.get("failureEligible") else "warning"
            prefix = (
                "Repeated regression"
                if item.get("failureEligible")
                else "Control-normalized regression warning"
            )
        elif item.get("corroboratesBaselineRegression"):
            level = "error"
            prefix = "Control ratio regression"
        elif item["status"] == "regression":
            level = "warning"
            prefix = "Regression"
        else:
            level = "notice"
            prefix = "Improvement"

        if "thresholdBreach" in item:
            if item.get("thresholdDirection") == "maximum":
                requirement = f"required <= {item['upper']:.2f}"
            else:
                requirement = f"required >= {item['lower']:.2f}"
            message = (
                f"{prefix}: {item['scenario']} {item['metricLabel']}={item['current']:.2f}; "
                f"{requirement}"
            )
        else:
            message = (
                f"{prefix}: {item['scenario']} {item['metricLabel']}={item['current']:.2f}; "
                f"baseline {item['median']:.2f}, band {item['lower']:.2f}-{item['upper']:.2f}"
            )
        print(f"::{level} title=Stress performance trend::{_annotation_escape(message)}")


def _load_json(path):
    with Path(path).open(encoding="utf-8") as file:
        return json.load(file)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results", required=True, help="Merged stress-test result JSON")
    parser.add_argument("--history", required=True, help="Committed compact history JSON")
    parser.add_argument("--output", required=True, help="Updated history JSON")
    parser.add_argument("--summary", help="GitHub step-summary file to append")
    parser.add_argument(
        "--github-output",
        help="GitHub Actions output file; reports repeated regressions via should_fail",
    )
    args = parser.parse_args(argv)

    result_document = _load_json(args.results)
    current_results = result_document.get("results")
    run_started_at = result_document.get("runStartedAtUtc")
    if not isinstance(current_results, list) or not run_started_at:
        raise ValueError("Merged results require runStartedAtUtc and a results list")

    history_path = Path(args.history)
    history = _load_json(history_path) if history_path.exists() else {"version": 1, "runs": []}
    evaluations, updated, should_fail = evaluate_and_update(history, current_results, run_started_at)

    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("w", encoding="utf-8", newline="\n") as file:
        json.dump(updated, file, indent=2)
        file.write("\n")

    report = format_markdown(evaluations)
    print(report)
    emit_annotations(evaluations)

    if args.summary:
        with Path(args.summary).open("a", encoding="utf-8", newline="\n") as file:
            file.write("\n" + report + "\n")

    if args.github_output:
        with Path(args.github_output).open("a", encoding="utf-8", newline="\n") as file:
            file.write(f"should_fail={str(should_fail).lower()}\n")
        return 0

    return 1 if should_fail else 0


if __name__ == "__main__":
    raise SystemExit(main())
