import json
import tempfile
import unittest
from contextlib import redirect_stdout
from io import StringIO
from pathlib import Path

from stress_trend import HISTORY_LIMIT, evaluate_and_update, main


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


class StressTrendTests(unittest.TestCase):
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

    def test_history_merge_wait_retries_one_transient_state_query_failure(self):
        workflow = (
            Path(__file__).parent.parent / "workflows" / "stress-tests.yml"
        ).read_text(encoding="utf-8")
        step = workflow[
            workflow.index("      - name: Wait for History Merge"):
            workflow.index("  regression-gate:")
        ]

        self.assertEqual(2, step.count('state=$(gh pr view "$PR_NUMBER"'))
        self.assertIn("Failed to read stress history PR state; retrying once", step)
        self.assertIn("Failed to read stress history PR state after retry", step)


if __name__ == "__main__":
    unittest.main()
