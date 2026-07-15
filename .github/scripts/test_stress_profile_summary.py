import tempfile
import unittest
from pathlib import Path

from stress_profile_summary import (
    build_summary,
    load_counter_rows,
    parse_topn,
    summarize_counters,
    topn_movement_table,
)

# Real shape from dotnet-trace PrintReportHelper: index + '. ' + name padded to
# 70 chars + inclusive% in a 20-wide field left-padded by 4 + exclusive%.
TOPN_SAMPLE = (
    "Top 3 Functions (Inclusive)                                                  "
    "Inclusive           Exclusive           \n"
    " 1. BrokerSender.SendLoopAsync(class System.Threading.CancellationToken)   "
    "    45.67%              12.34%              \n"
    " 2. RecordAccumulator.Append(!0&,!1&)                                       "
    "    30.5%               8%                  \n"
    " 3. Missing Symbol                                                          "
    "    10%                 10%                 \n"
)

COUNTERS_SAMPLE = (
    "Timestamp,Provider,Counter Name,Counter Type,Mean/Increment\n"
    "07/15/2026 15:00:00,System.Runtime,CPU Usage (%),Metric,50\n"
    "07/15/2026 15:00:00,Dekaf,dekaf.producer.buffer.used_bytes,Metric,1000\n"
    "07/15/2026 15:00:01,System.Runtime,CPU Usage (%),Metric,70\n"
    "07/15/2026 15:00:01,Dekaf,dekaf.producer.buffer.used_bytes,Metric,3000\n"
    "07/15/2026 15:00:02,System.Runtime,CPU Usage (%),Metric,90\n"
    "07/15/2026 15:00:02,Dekaf,dekaf.producer.buffer.used_bytes,Metric,5000\n"
    "07/15/2026 15:00:03,System.Runtime,CPU Usage (%),Metric,10\n"
)


class TopNParsingTests(unittest.TestCase):
    def parse(self, content):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "w.topN-inclusive.txt"
            path.write_text(content, encoding="utf-8")
            return parse_topn(path)

    def test_parses_fixed_width_rows_and_skips_header(self):
        percents = self.parse(TOPN_SAMPLE)

        self.assertEqual(
            45.67,
            percents["BrokerSender.SendLoopAsync(class System.Threading.CancellationToken)"],
        )
        self.assertEqual(30.5, percents["RecordAccumulator.Append(!0&,!1&)"])
        self.assertEqual(10.0, percents["Missing Symbol"])
        self.assertEqual(3, len(percents))

    def test_duplicate_truncated_names_keep_larger_percent(self):
        content = (
            " 1. Same.Name(x)                                                            "
            "    40%                 5%                  \n"
            " 2. Same.Name(x)                                                            "
            "    15%                 5%                  \n"
        )

        self.assertEqual({"Same.Name(x)": 40.0}, self.parse(content))

    def test_missing_file_returns_empty(self):
        self.assertEqual({}, parse_topn(Path("does-not-exist.txt")))


class CounterAggregationTests(unittest.TestCase):
    def load_rows(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "runtime-counters.csv"
            path.write_text(COUNTERS_SAMPLE, encoding="utf-8")
            return load_counter_rows(path)

    def test_rows_are_anchored_by_timestamp_tick_index(self):
        rows = self.load_rows()

        ticks = sorted({row[0] for row in rows})
        self.assertEqual([0, 1, 2, 3], ticks)

    def test_window_aggregation_computes_mean_and_max(self):
        rows = self.load_rows()
        windows = [(0, 2, "healthy"), (2, 2, "collapse")]

        aggregates = summarize_counters(rows, windows, counter_skew_seconds=0)

        cpu = aggregates[("System.Runtime", "CPU Usage (%)", "Metric")]
        count, total, peak = cpu["healthy"]
        self.assertEqual(2, count)
        self.assertEqual(60.0, total / count)
        self.assertEqual(70.0, peak)
        count, total, peak = cpu["collapse"]
        self.assertEqual(50.0, total / count)
        self.assertEqual(90.0, peak)

    def test_counter_skew_shifts_rows_between_windows(self):
        rows = self.load_rows()
        windows = [(2, 2, "late")]

        aggregates = summarize_counters(rows, windows, counter_skew_seconds=2)

        cpu = aggregates[("System.Runtime", "CPU Usage (%)", "Metric")]
        count, total, _ = cpu["late"]
        self.assertEqual(2, count)
        self.assertEqual(60.0, total / count)


class MovementTableTests(unittest.TestCase):
    def test_ranks_by_absolute_delta_between_first_and_last_window(self):
        windows = [(0, 30, "healthy"), (60, 30, "collapse")]
        per_window = {
            "healthy": {"Stable.Method()": 20.0, "Collapsing.Method()": 5.0},
            "collapse": {"Stable.Method()": 21.0, "Collapsing.Method()": 45.0},
        }

        lines = topn_movement_table(per_window, windows)

        table = "\n".join(lines)
        self.assertIn("Collapsing.Method()", table)
        self.assertIn("+40.00", table)
        collapsing_row = next(i for i, l in enumerate(lines) if "Collapsing" in l)
        stable_row = next(i for i, l in enumerate(lines) if "Stable" in l)
        self.assertLess(collapsing_row, stable_row)

    def test_single_window_reports_nothing_to_compare(self):
        windows = [(0, 30, "only")]
        lines = topn_movement_table({"only": {"A.B()": 1.0}}, windows)

        self.assertIn("Fewer than two parseable topN reports", "\n".join(lines))


class BuildSummaryTests(unittest.TestCase):
    def test_end_to_end_summary_contains_both_sections(self):
        with tempfile.TemporaryDirectory() as tmp:
            profile_dir = Path(tmp)
            (profile_dir / "profile-metadata.txt").write_text(
                "trace_profile=gc\n"
                "trace_windows=0:2:healthy,2:2:collapse\n"
                "measured_start_epoch=100\n"
                "counters_start_epoch=100\n"
                "stress_args=--duration 15\n",
                encoding="utf-8",
            )
            (profile_dir / "runtime-counters.csv").write_text(
                COUNTERS_SAMPLE, encoding="utf-8"
            )
            (profile_dir / "healthy.topN-inclusive.txt").write_text(
                TOPN_SAMPLE, encoding="utf-8"
            )
            (profile_dir / "collapse.topN-inclusive.txt").write_text(
                TOPN_SAMPLE.replace("45.67%", "80.00%"), encoding="utf-8"
            )

            summary = build_summary(profile_dir)

        self.assertIn("## Counter aggregates by window", summary)
        self.assertIn("dekaf.producer.buffer.used_bytes", summary)
        self.assertIn("## Top-method movement across windows", summary)
        self.assertIn("BrokerSender.SendLoopAsync", summary)

    def test_missing_metadata_degrades_gracefully(self):
        with tempfile.TemporaryDirectory() as tmp:
            summary = build_summary(tmp)

        self.assertIn("nothing to summarize", summary)


if __name__ == "__main__":
    unittest.main()
