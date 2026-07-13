import json
import tempfile
import unittest
from contextlib import redirect_stdout
from io import StringIO
from pathlib import Path

from stress_trend import (
    HISTORY_LIMIT,
    emit_annotations,
    evaluate_and_update,
    format_markdown,
    main,
)


def result(messages_per_second=1000.0, cpu_micros_per_message=2.0, **overrides):
    value = {
        "scenario": "producer",
        "client": "Dekaf",
        "brokerCount": 1,
        "durationMinutes": 15,
        "messageSizeBytes": 1000,
        "effectiveMessagesPerSecond": messages_per_second,
        "cpuMicrosPerMessage": cpu_micros_per_message,
        "throughput": {},
    }
    value.update(overrides)
    return value


def history_run(index, messages_per_second=1000.0, cpu_micros_per_message=2.0, **trends):
    observation = {
        "scenario": "producer",
        "client": "Dekaf",
        "brokerCount": 1,
        "durationMinutes": 15,
        "messageSizeBytes": 1000,
        "messagesPerSecond": messages_per_second,
        "cpuMicrosPerMessage": cpu_micros_per_message,
    }
    observation.update(trends)
    return {
        "runStartedAtUtc": f"2026-06-{index:02d}T02:00:00Z",
        "results": [observation],
    }


def paired_history_run(
    index,
    dekaf_messages_per_second=1000.0,
    confluent_messages_per_second=500.0,
    **trends,
):
    run = history_run(index, dekaf_messages_per_second, **trends)
    confluent = {
        **run["results"][0],
        "client": "Confluent",
        "messagesPerSecond": confluent_messages_per_second,
    }
    run["results"].append(confluent)
    return run


def intra_run_result(client="Dekaf", steady_ratio=0.6, slope=-8.0, **overrides):
    value = result(
        client=client,
        steadyStatePeakRatio=steady_ratio,
        intraRunDriftPercent=-35.9,
        throughputSlopePercentPerMinute=slope,
        steadyStatePeakRatioThreshold=0.85,
        throughputSlopePercentPerMinuteThreshold=-1.0,
        steadyStatePeakThresholdBreached=steady_ratio < 0.85,
        throughputSlopeThresholdBreached=slope < -1.0,
        intraRunThroughputThresholdBreached=steady_ratio < 0.85 or slope < -1.0,
        throughput={
            "elapsedSeconds": 360,
            "messagesPerSecondSamples": [1400, 1400, 1400],
        },
    )
    value.update(overrides)
    return value


class StressTrendTests(unittest.TestCase):
    def test_paired_latency_threshold_breach_fails_without_history(self):
        confluent = result(
            client="Confluent",
            latency={"p50Us": 10_000, "p99Us": 50_000},
        )
        dekaf = result(
            client="Dekaf",
            latency={"p50Us": 15_000, "p99Us": 150_000},
        )

        evaluations, _, should_fail = evaluate_and_update(
            {"version": 1, "runs": []},
            [confluent, dekaf],
            "2026-07-01T02:00:00Z",
        )

        latency = [item for item in evaluations if item.get("latencyThreshold")]
        self.assertTrue(should_fail)
        self.assertEqual(2, len(latency))
        by_metric = {item["metric"]: item for item in latency}
        self.assertFalse(by_metric["latencyP50Ratio"]["thresholdBreach"])
        self.assertTrue(by_metric["latencyP99Ratio"]["thresholdBreach"])
        markdown = format_markdown(latency)
        self.assertIn("Threshold breach (fail)", markdown)
        self.assertNotIn("Repeated threshold breach", markdown)

    def test_first_intra_run_threshold_breach_warns_without_failing(self):
        evaluations, updated, should_fail = evaluate_and_update(
            {"version": 1, "runs": []},
            [intra_run_result()],
            "2026-07-01T02:00:00Z",
        )

        intra_run = [item for item in evaluations if item.get("thresholdBreach")]
        self.assertFalse(should_fail)
        self.assertEqual(
            {"steadyStatePeakRatio", "slopePercentPerMinute"},
            {item["metric"] for item in intra_run},
        )
        self.assertTrue(all(not item["repeatedRegression"] for item in intra_run))
        self.assertIn("Threshold breach (warning)", format_markdown(intra_run))
        current = updated["runs"][-1]["results"][0]
        self.assertEqual("regression", current["steadyStatePeakRatioTrend"])
        self.assertEqual("regression", current["slopePercentPerMinuteTrend"])

    def test_flat_in_band_steady_state_breach_is_not_failure(self):
        runs = [history_run(i) for i in range(1, 4)]
        runs.append(history_run(
            4,
            steadyStatePeakRatio=0.7,
            steadyStatePeakRatioTrend="regression",
            slopePercentPerMinute=0.1,
            slopePercentPerMinuteTrend="stable",
        ))

        evaluations, _, should_fail = evaluate_and_update(
            {"version": 1, "runs": runs},
            [intra_run_result(steady_ratio=0.7, slope=0.1)],
            "2026-07-01T02:00:00Z",
        )

        steady = next(
            item for item in evaluations if item["metric"] == "steadyStatePeakRatio"
        )
        self.assertTrue(steady["repeatedRegression"])
        self.assertFalse(steady["corroborated"])
        self.assertFalse(steady["failureEligible"])
        self.assertFalse(should_fail)

    def test_second_consecutive_slope_breach_fails_for_dekaf(self):
        runs = [history_run(i) for i in range(1, 4)]
        runs.append(history_run(
            4,
            steadyStatePeakRatio=0.7,
            steadyStatePeakRatioTrend="regression",
            slopePercentPerMinute=-2.0,
            slopePercentPerMinuteTrend="regression",
        ))

        evaluations, _, should_fail = evaluate_and_update(
            {"version": 1, "runs": runs},
            [intra_run_result(steady_ratio=0.7, slope=-2.5)],
            "2026-07-01T02:00:00Z",
        )

        slope = next(
            item for item in evaluations if item["metric"] == "slopePercentPerMinute"
        )
        self.assertTrue(slope["repeatedRegression"])
        self.assertTrue(slope["failureEligible"])
        self.assertIn("Repeated threshold breach (fail)", format_markdown([slope]))
        self.assertTrue(should_fail)

    def test_legacy_intra_run_value_preserves_breach_streak(self):
        runs = [history_run(i) for i in range(1, 4)]
        runs.append(history_run(
            4,
            steadyStatePeakRatio=0.7,
            slopePercentPerMinute=-2.0,
        ))

        evaluations, _, should_fail = evaluate_and_update(
            {"version": 1, "runs": runs},
            [intra_run_result(steady_ratio=0.7, slope=-2.5)],
            "2026-07-01T02:00:00Z",
        )

        intra_run = [
            item for item in evaluations
            if item["metric"] in {"steadyStatePeakRatio", "slopePercentPerMinute"}
        ]
        self.assertTrue(all(item["repeatedRegression"] for item in intra_run))
        self.assertTrue(all(item["failureEligible"] for item in intra_run))
        self.assertTrue(should_fail)

    def test_second_corroborated_steady_state_breach_fails_for_dekaf(self):
        runs = [history_run(i) for i in range(1, 4)]
        runs.append(history_run(
            4,
            messages_per_second=700.0,
            messagesPerSecondTrend="regression",
            steadyStatePeakRatio=0.7,
            steadyStatePeakRatioTrend="regression",
            slopePercentPerMinute=0.1,
            slopePercentPerMinuteTrend="stable",
        ))

        evaluations, _, should_fail = evaluate_and_update(
            {"version": 1, "runs": runs},
            [intra_run_result(
                steady_ratio=0.7,
                slope=0.1,
                effectiveMessagesPerSecond=650.0,
            )],
            "2026-07-01T02:00:00Z",
        )

        steady = next(
            item for item in evaluations if item["metric"] == "steadyStatePeakRatio"
        )
        self.assertTrue(steady["repeatedRegression"])
        self.assertTrue(steady["corroborated"])
        self.assertTrue(steady["failureEligible"])
        self.assertTrue(should_fail)

    def test_confluent_intra_run_breaches_warn_but_never_fail(self):
        runs = [
            history_run(
                i,
                client="Confluent",
                steadyStatePeakRatio=0.7,
                steadyStatePeakRatioTrend="regression",
                slopePercentPerMinute=-2.0,
                slopePercentPerMinuteTrend="regression",
            )
            for i in range(1, 5)
        ]

        evaluations, _, should_fail = evaluate_and_update(
            {"version": 1, "runs": runs},
            [intra_run_result(client="Confluent", steady_ratio=0.7, slope=-2.5)],
            "2026-07-01T02:00:00Z",
        )

        intra_run = [item for item in evaluations if item.get("thresholdBreach")]
        self.assertTrue(all(item["repeatedRegression"] for item in intra_run))
        self.assertTrue(all(not item["failureEligible"] for item in intra_run))
        self.assertNotIn("(fail)", format_markdown(intra_run))
        self.assertFalse(should_fail)

    def test_confluent_baseline_regressions_mark_environment_shift_but_never_fail(self):
        runs = [
            history_run(i, client="Confluent")
            for i in range(1, 4)
        ]
        runs.append(history_run(
            4,
            messages_per_second=700.0,
            client="Confluent",
            messagesPerSecondTrend="regression",
        ))

        evaluations, updated, should_fail = evaluate_and_update(
            {"version": 1, "runs": runs},
            [result(client="Confluent", messages_per_second=650.0)],
            "2026-07-01T02:00:00Z",
        )

        throughput = next(
            item for item in evaluations if item["metric"] == "messagesPerSecond"
        )
        self.assertTrue(throughput["repeatedRegression"])
        self.assertFalse(throughput["failureEligible"])
        self.assertTrue(throughput["environmentShiftSuspected"])
        self.assertTrue(updated["runs"][-1]["environmentShiftSuspected"])
        self.assertTrue(
            updated["runs"][-1]["results"][0]["environmentShiftSuspected"]
        )
        self.assertIn("Environment shift suspected", format_markdown(evaluations))
        with redirect_stdout(StringIO()) as annotations:
            emit_annotations(evaluations)
        self.assertIn("::warning", annotations.getvalue())
        self.assertIn("Environment shift suspected", annotations.getvalue())
        self.assertFalse(should_fail)

    def test_environment_shift_excludes_every_observation_in_same_run(self):
        runs = []
        for index in range(1, 4):
            run = history_run(index, client="Confluent")
            run["results"].append({
                **run["results"][0],
                "scenario": "consumer",
                "client": "Dekaf",
            })
            runs.append(run)
        previous = history_run(
            4,
            messages_per_second=700.0,
            client="Confluent",
            messagesPerSecondTrend="regression",
        )
        previous["results"].append({
            **previous["results"][0],
            "scenario": "consumer",
            "client": "Dekaf",
        })
        runs.append(previous)

        _, updated, should_fail = evaluate_and_update(
            {"version": 1, "runs": runs},
            [
                result(client="Confluent", messages_per_second=650.0),
                result(scenario="consumer", messages_per_second=1_000.0),
            ],
            "2026-07-01T02:00:00Z",
        )

        current = updated["runs"][-1]
        self.assertTrue(current["environmentShiftSuspected"])
        self.assertTrue(all(
            observation["environmentShiftSuspected"]
            for observation in current["results"]
        ))
        self.assertFalse(should_fail)

    def test_paired_proportional_regressions_do_not_fail_when_control_ratio_is_stable(self):
        runs = [paired_history_run(i) for i in range(1, 4)]
        runs.append(paired_history_run(
            4,
            dekaf_messages_per_second=700.0,
            confluent_messages_per_second=350.0,
            messagesPerSecondTrend="regression",
        ))

        evaluations, _, should_fail = evaluate_and_update(
            {"version": 1, "runs": runs},
            [
                result(messages_per_second=650.0),
                result(client="Confluent", messages_per_second=325.0),
            ],
            "2026-07-01T02:00:00Z",
        )

        dekaf_throughput = next(
            item for item in evaluations
            if item["metric"] == "messagesPerSecond"
            and item["scenario"].split(" / ")[1] == "Dekaf"
        )
        ratio = next(
            item for item in evaluations
            if item["metric"] == "messagesPerSecondControlRatio"
        )
        self.assertTrue(dekaf_throughput["repeatedRegression"])
        self.assertFalse(dekaf_throughput["corroborated"])
        self.assertFalse(dekaf_throughput["failureEligible"])
        self.assertEqual("stable", ratio["status"])
        self.assertFalse(should_fail)

    def test_paired_dekaf_regression_fails_when_control_ratio_also_regresses(self):
        runs = [paired_history_run(i) for i in range(1, 4)]
        runs.append(paired_history_run(
            4,
            dekaf_messages_per_second=700.0,
            confluent_messages_per_second=350.0,
            messagesPerSecondTrend="regression",
        ))

        evaluations, _, should_fail = evaluate_and_update(
            {"version": 1, "runs": runs},
            [
                result(messages_per_second=550.0),
                result(client="Confluent", messages_per_second=325.0),
            ],
            "2026-07-01T02:00:00Z",
        )

        dekaf_throughput = next(
            item for item in evaluations
            if item["metric"] == "messagesPerSecond"
            and item["scenario"].split(" / ")[1] == "Dekaf"
        )
        ratio = next(
            item for item in evaluations
            if item["metric"] == "messagesPerSecondControlRatio"
        )
        self.assertTrue(dekaf_throughput["corroborated"])
        self.assertTrue(dekaf_throughput["failureEligible"])
        self.assertEqual("regression", ratio["status"])
        self.assertTrue(should_fail)

    def test_environment_shift_observations_do_not_enter_absolute_baseline(self):
        runs = [history_run(i) for i in range(1, 4)]
        runs.append({
            **history_run(4, messages_per_second=5000.0),
            "environmentShiftSuspected": True,
        })
        runs[-1]["results"][0]["environmentShiftSuspected"] = True

        evaluations, _, _ = evaluate_and_update(
            {"version": 1, "runs": runs},
            [result(messages_per_second=1000.0)],
            "2026-07-01T02:00:00Z",
        )

        throughput = next(
            item for item in evaluations if item["metric"] == "messagesPerSecond"
        )
        self.assertEqual(3, throughput["baselineCount"])
        self.assertEqual(1000.0, throughput["median"])

    def test_environment_shift_observations_do_not_enter_ratio_baseline(self):
        runs = [paired_history_run(i) for i in range(1, 4)]
        environment_shift = paired_history_run(
            4,
            dekaf_messages_per_second=5000.0,
            confluent_messages_per_second=1000.0,
        )
        for observation in environment_shift["results"]:
            observation["environmentShiftSuspected"] = True
        runs.append(environment_shift)

        evaluations, _, _ = evaluate_and_update(
            {"version": 1, "runs": runs},
            [result(), result(client="Confluent", messages_per_second=500.0)],
            "2026-07-01T02:00:00Z",
        )

        ratio = next(
            item for item in evaluations
            if item["metric"] == "messagesPerSecondControlRatio"
        )
        self.assertEqual(3, ratio["baselineCount"])
        self.assertEqual(2.0, ratio["median"])

    def test_control_ratio_history_keeps_dekaf_connection_profiles_separate(self):
        runs = [
            paired_history_run(
                i,
                scenario="consumer",
                consumerConnectionsPerBroker=2,
            )
            for i in range(1, 4)
        ]
        for run in runs:
            run["results"][1]["consumerConnectionsPerBroker"] = 1

        evaluations, _, should_fail = evaluate_and_update(
            {"version": 1, "runs": runs},
            [
                result(scenario="consumer", consumerConnectionsPerBroker=3),
                result(
                    scenario="consumer",
                    client="Confluent",
                    consumerConnectionsPerBroker=1,
                    messages_per_second=500.0,
                ),
            ],
            "2026-07-01T02:00:00Z",
        )

        ratio = next(
            item for item in evaluations
            if item["metric"] == "messagesPerSecondControlRatio"
        )
        self.assertEqual(0, ratio["baselineCount"])
        self.assertEqual("insufficient-history", ratio["status"])
        self.assertFalse(should_fail)

    def test_empty_history_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "Unsupported stress history version"):
            evaluate_and_update({}, [result()], "2026-07-01T02:00:00Z")

    def test_first_adverse_excursion_warns_without_failing(self):
        history = {"version": 1, "runs": [history_run(i) for i in range(1, 4)]}

        evaluations, updated, should_fail = evaluate_and_update(
            history,
            [result(messages_per_second=700.0, cpu_micros_per_message=3.0)],
            "2026-07-01T02:00:00Z",
        )

        self.assertFalse(should_fail)
        statuses = {item["metric"]: item["status"] for item in evaluations}
        self.assertEqual("regression", statuses["messagesPerSecond"])
        self.assertEqual("regression", statuses["cpuMicrosPerMessage"])
        current = updated["runs"][-1]["results"][0]
        self.assertEqual("regression", current["messagesPerSecondTrend"])
        self.assertEqual("regression", current["cpuMicrosPerMessageTrend"])

    def test_second_consecutive_regression_fails(self):
        runs = [history_run(i) for i in range(1, 4)]
        runs.append(history_run(
            4,
            messages_per_second=700.0,
            cpu_micros_per_message=3.0,
            messagesPerSecondTrend="regression",
            cpuMicrosPerMessageTrend="regression",
        ))

        evaluations, _, should_fail = evaluate_and_update(
            {"version": 1, "runs": runs},
            [result(messages_per_second=650.0, cpu_micros_per_message=3.2)],
            "2026-07-01T02:00:00Z",
        )

        self.assertTrue(should_fail)
        repeated = {item["metric"] for item in evaluations if item["repeatedRegression"]}
        self.assertEqual({"messagesPerSecond", "cpuMicrosPerMessage"}, repeated)

    def test_warned_run_does_not_widen_next_baseline(self):
        runs = [
            history_run(1, messages_per_second=950.0),
            history_run(2, messages_per_second=1000.0),
            history_run(3, messages_per_second=1050.0),
            history_run(
                4,
                messages_per_second=890.0,
                messagesPerSecondTrend="regression",
            ),
        ]

        evaluations, _, should_fail = evaluate_and_update(
            {"version": 1, "runs": runs},
            [result(messages_per_second=890.0)],
            "2026-07-01T02:00:00Z",
        )

        throughput = next(
            item for item in evaluations if item["metric"] == "messagesPerSecond"
        )
        self.assertEqual(3, throughput["baselineCount"])
        self.assertEqual(1000.0, throughput["median"])
        self.assertEqual(900.0, throughput["lower"])
        self.assertEqual(1100.0, throughput["upper"])
        self.assertEqual("regression", throughput["status"])
        self.assertTrue(throughput["repeatedRegression"])
        self.assertTrue(should_fail)

    def test_regression_streak_does_not_evict_clean_baseline(self):
        runs = [
            history_run(1, messages_per_second=950.0),
            history_run(2, messages_per_second=1000.0),
            history_run(3, messages_per_second=1050.0),
        ]
        runs.extend(
            history_run(
                index,
                messages_per_second=890.0,
                messagesPerSecondTrend="regression",
            )
            for index in range(4, 12)
        )

        _, updated, _ = evaluate_and_update(
            {"version": 1, "runs": runs},
            [result(messages_per_second=890.0)],
            "2026-07-01T02:00:00Z",
        )
        evaluations, _, should_fail = evaluate_and_update(
            updated,
            [result(messages_per_second=890.0)],
            "2026-07-08T02:00:00Z",
        )

        throughput = next(
            item for item in evaluations if item["metric"] == "messagesPerSecond"
        )
        self.assertEqual(3, throughput["baselineCount"])
        self.assertEqual("regression", throughput["status"])
        self.assertTrue(throughput["repeatedRegression"])
        self.assertTrue(should_fail)

    def test_improvement_is_flagged_but_never_fails(self):
        history = {"version": 1, "runs": [history_run(i) for i in range(1, 4)]}

        evaluations, _, should_fail = evaluate_and_update(
            history,
            [result(messages_per_second=1300.0, cpu_micros_per_message=1.0)],
            "2026-07-01T02:00:00Z",
        )

        self.assertFalse(should_fail)
        self.assertEqual({"improvement"}, {item["status"] for item in evaluations})

    def test_zero_mad_uses_relative_noise_floor(self):
        history = {"version": 1, "runs": [history_run(i) for i in range(1, 4)]}

        evaluations, _, should_fail = evaluate_and_update(
            history,
            [result(messages_per_second=995.0)],
            "2026-07-01T02:00:00Z",
        )

        throughput = next(
            item for item in evaluations if item["metric"] == "messagesPerSecond"
        )
        self.assertEqual(0.0, throughput["mad"])
        self.assertEqual(990.0, throughput["lower"])
        self.assertEqual(1010.0, throughput["upper"])
        self.assertEqual("stable", throughput["status"])
        self.assertFalse(should_fail)

    def test_different_configuration_does_not_supply_baseline(self):
        history = {"version": 1, "runs": [history_run(i) for i in range(1, 4)]}

        evaluations, _, should_fail = evaluate_and_update(
            history,
            [result(messages_per_second=100.0, messageSizeBytes=4096)],
            "2026-07-01T02:00:00Z",
        )

        self.assertFalse(should_fail)
        self.assertEqual({"insufficient-history"}, {item["status"] for item in evaluations})

    def test_consumer_seed_batch_size_preserves_legacy_baseline_and_separates_new_shapes(self):
        history = {
            "version": 1,
            "runs": [history_run(i, scenario="consumer") for i in range(1, 4)],
        }

        legacy_evaluations, _, _ = evaluate_and_update(
            history,
            [result(scenario="consumer", consumerSeedBatchSizeBytes=16_384)],
            "2026-07-01T02:00:00Z",
        )
        large_batch_evaluations, _, _ = evaluate_and_update(
            history,
            [result(scenario="consumer", consumerSeedBatchSizeBytes=1_048_576)],
            "2026-07-01T02:00:00Z",
        )

        self.assertNotEqual(
            {"insufficient-history"},
            {item["status"] for item in legacy_evaluations},
        )
        self.assertEqual(
            {"insufficient-history"},
            {item["status"] for item in large_batch_evaluations},
        )

    def test_consumer_connection_count_separates_new_performance_profile(self):
        history = {
            "version": 1,
            "runs": [history_run(i, scenario="consumer-batch") for i in range(1, 4)],
        }

        legacy_evaluations, _, _ = evaluate_and_update(
            history,
            [result(scenario="consumer-batch", consumerConnectionsPerBroker=2)],
            "2026-07-01T02:00:00Z",
        )
        three_connection_evaluations, updated, _ = evaluate_and_update(
            history,
            [result(scenario="consumer-batch", consumerConnectionsPerBroker=3)],
            "2026-07-01T02:00:00Z",
        )

        self.assertNotEqual(
            {"insufficient-history"},
            {item["status"] for item in legacy_evaluations},
        )
        self.assertEqual(
            {"insufficient-history"},
            {item["status"] for item in three_connection_evaluations},
        )
        self.assertEqual(
            3,
            updated["runs"][-1]["results"][0]["consumerConnectionsPerBroker"],
        )

    def test_different_roundtrip_bound_does_not_supply_baseline(self):
        history = {
            "version": 1,
            "runs": [
                history_run(
                    i,
                    scenario="producer-roundtrip",
                    roundTripMessages=250_000,
                )
                for i in range(1, 4)
            ],
        }

        evaluations, updated, should_fail = evaluate_and_update(
            history,
            [result(
                messages_per_second=100.0,
                scenario="producer-roundtrip",
                roundTripValidation={"expectedMessages": 1_000},
            )],
            "2026-07-01T02:00:00Z",
        )

        self.assertFalse(should_fail)
        self.assertEqual({"insufficient-history"}, {item["status"] for item in evaluations})
        self.assertEqual(1_000, updated["runs"][-1]["results"][0]["roundTripMessages"])

    def test_history_is_bounded_and_same_run_is_replaced(self):
        runs = [history_run(i) for i in range(1, HISTORY_LIMIT + 1)]
        duplicate_timestamp = runs[-1]["runStartedAtUtc"]

        _, updated, _ = evaluate_and_update(
            {"version": 1, "runs": runs},
            [result(messages_per_second=1100.0)],
            duplicate_timestamp,
        )

        self.assertEqual(HISTORY_LIMIT, len(updated["runs"]))
        self.assertEqual(1, sum(run["runStartedAtUtc"] == duplicate_timestamp for run in updated["runs"]))
        self.assertEqual(1100.0, updated["runs"][-1]["results"][0]["messagesPerSecond"])

    def test_history_limit_is_applied_per_configuration(self):
        scheduled_runs = [history_run(i) for i in range(1, HISTORY_LIMIT + 1)]
        ad_hoc_runs = [
            history_run(i, durationMinutes=60)
            for i in range(HISTORY_LIMIT + 1, (HISTORY_LIMIT * 2) + 1)
        ]

        evaluations, updated, _ = evaluate_and_update(
            {"version": 1, "runs": scheduled_runs + ad_hoc_runs},
            [result(messages_per_second=1000.0)],
            "2026-07-01T02:00:00Z",
        )

        throughput = next(
            item for item in evaluations if item["metric"] == "messagesPerSecond"
        )
        self.assertEqual(HISTORY_LIMIT, throughput["baselineCount"])

        retained_durations = [
            observation["durationMinutes"]
            for run in updated["runs"]
            for observation in run["results"]
        ]
        self.assertEqual(HISTORY_LIMIT, retained_durations.count(15))
        self.assertEqual(HISTORY_LIMIT, retained_durations.count(60))

    def test_cli_reports_repeated_regression_without_hiding_updated_history(self):
        runs = [history_run(i) for i in range(1, 4)]
        runs.append(history_run(
            4,
            messages_per_second=700.0,
            messagesPerSecondTrend="regression",
        ))
        run_started_at = "2026-07-01T02:00:00Z"

        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            results_path = directory / "results.json"
            history_path = directory / "history.json"
            output_path = directory / "updated-history.json"
            github_output_path = directory / "github-output.txt"
            results_path.write_text(json.dumps({
                "runStartedAtUtc": run_started_at,
                "results": [result(messages_per_second=650.0)],
            }), encoding="utf-8")
            history_path.write_text(json.dumps({
                "version": 1,
                "runs": runs,
            }), encoding="utf-8")

            with redirect_stdout(StringIO()):
                exit_code = main([
                    "--results", str(results_path),
                    "--history", str(history_path),
                    "--output", str(output_path),
                    "--github-output", str(github_output_path),
                ])

            self.assertEqual(0, exit_code)
            self.assertEqual(
                "should_fail=true\n",
                github_output_path.read_text(encoding="utf-8"),
            )
            updated = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual(run_started_at, updated["runs"][-1]["runStartedAtUtc"])
            self.assertEqual(
                "regression",
                updated["runs"][-1]["results"][0]["messagesPerSecondTrend"],
            )

    def test_cli_does_not_report_processing_error_as_regression(self):
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            results_path = directory / "results.json"
            history_path = directory / "history.json"
            output_path = directory / "updated-history.json"
            github_output_path = directory / "github-output.txt"
            results_path.write_text(json.dumps({
                "runStartedAtUtc": "2026-07-01T02:00:00Z",
                "results": [result()],
            }), encoding="utf-8")
            history_path.write_text(json.dumps({
                "version": 999,
                "runs": [],
            }), encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "Unsupported stress history version"):
                main([
                    "--results", str(results_path),
                    "--history", str(history_path),
                    "--output", str(output_path),
                    "--github-output", str(github_output_path),
                ])

            self.assertFalse(github_output_path.exists())

    def test_workflow_creates_merged_results_before_searching_for_results(self):
        workflow = (
            Path(__file__).parent.parent / "workflows" / "stress-tests.yml"
        ).read_text(encoding="utf-8")
        step = workflow[
            workflow.index("      - name: Detect Performance Trends"):
            workflow.index("      - name: Upload Merged Results")
        ]

        create_directory = step.index("mkdir -p merged-results")
        find_result = step.index("result_file=$(find merged-results")
        self.assertLess(create_directory, find_result)

    def test_workflow_regression_gate_consumes_all_failure_kinds(self):
        workflow = (
            Path(__file__).parent.parent / "workflows" / "stress-tests.yml"
        ).read_text(encoding="utf-8")

        self.assertIn(
            "regression_failure: ${{ steps.stress-trends.outputs.should_fail }}",
            workflow,
        )
        self.assertIn(
            "needs.generate-summary.outputs.regression_failure == 'true'",
            workflow,
        )

    def test_history_merge_uses_admin_token(self):
        workflow = (
            Path(__file__).parent.parent / "workflows" / "stress-tests.yml"
        ).read_text(encoding="utf-8")
        step = workflow[
            workflow.index("      - name: Auto-merge Pull Request"):
            workflow.index("  regression-gate:")
        ]

        self.assertIn("GH_TOKEN: ${{ secrets.BENCHMARK_MERGE_TOKEN }}", step)
        self.assertIn(
            'gh pr merge "$PR_NUMBER" --squash --delete-branch --admin',
            step,
        )


if __name__ == "__main__":
    unittest.main()
