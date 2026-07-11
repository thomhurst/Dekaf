import unittest

from stress_report import (
    format_roundtrip_validation_table,
    format_throughput_table,
    generate_scenario_tables,
    intra_run_throughput,
)


def stress_result(client, effective_rate, median_rate=None, is_message_bounded=False):
    result = {
        "client": client,
        "durationMinutes": 15,
        "messageSizeBytes": 1000,
        "effectiveMessagesPerSecond": effective_rate,
        "effectiveMegabytesPerSecond": effective_rate * 1000 / (1024 * 1024),
        "isMessageBounded": is_message_bounded,
        "throughput": {
            "averageMessagesPerSecond": effective_rate,
            "averageMegabytesPerSecond": effective_rate * 1000 / (1024 * 1024),
            "totalErrors": 0,
            "totalMessages": 1000,
            "elapsedSeconds": 1,
            "messagesPerSecondSamples": [],
        },
    }

    if median_rate is not None:
        result["medianIntervalMessagesPerSecond"] = median_rate

    return result


class StressReportTests(unittest.TestCase):
    def test_intra_run_throughput_detects_front_loaded_collapse(self):
        value = stress_result("Dekaf", effective_rate=1400)
        value["throughput"].update({
            "elapsedSeconds": 900,
            "messagesPerSecondSamples": [
                1700, 1800, 1750, 2000, 2050, 1950,
                1350, 1300, 1250, 1200, 1280, 1270,
            ],
        })

        metrics = intra_run_throughput(value)

        self.assertIsNotNone(metrics)
        self.assertAlmostEqual(-31.03, metrics["driftPercent"], places=2)
        self.assertLess(metrics["steadyStatePeakRatio"], 0.85)
        self.assertLess(metrics["slopePercentPerMinute"], -1.0)
        self.assertTrue(metrics["thresholdBreached"])

    def test_throughput_table_surfaces_drift_and_slope(self):
        value = stress_result("Dekaf", effective_rate=1400)
        value["throughput"].update({
            "elapsedSeconds": 360,
            "messagesPerSecondSamples": [2000, 1900, 1800, 1400, 1300, 1200],
        })

        report = "\n".join(format_throughput_table([value], "Producer Throughput"))

        self.assertIn("Drift", report)
        self.assertIn("Slope %/min", report)
        self.assertIn("-35.9%", report)

    def test_intra_run_throughput_accepts_stable_samples(self):
        value = stress_result("Dekaf", effective_rate=1400)
        value["throughput"].update({
            "elapsedSeconds": 360,
            "messagesPerSecondSamples": [1400, 1410, 1390, 1405, 1395, 1400],
        })

        metrics = intra_run_throughput(value)

        self.assertIsNotNone(metrics)
        self.assertFalse(metrics["thresholdBreached"])

    def test_throughput_table_orders_and_ratios_by_median_when_available(self):
        lines = format_throughput_table(
            [
                stress_result("Dekaf", effective_rate=2000, median_rate=900),
                stress_result("Confluent", effective_rate=1000, median_rate=1200),
            ],
            "Producer Throughput",
            include_ratio=True,
        )

        rows = [line for line in lines if line.startswith("| Dekaf") or line.startswith("| Confluent")]

        self.assertTrue(rows[0].startswith("| Confluent"))
        self.assertTrue(rows[1].startswith("| Dekaf"))
        self.assertIn("Comparison Ratio", "\n".join(lines))
        self.assertIn("| 0.75x |", rows[1])

    def test_throughput_table_falls_back_to_headline_rate_without_median(self):
        lines = format_throughput_table(
            [
                stress_result("Dekaf", effective_rate=2000),
                stress_result("Confluent", effective_rate=1000),
            ],
            "Producer Throughput",
            include_ratio=True,
        )

        rows = [line for line in lines if line.startswith("| Dekaf") or line.startswith("| Confluent")]

        self.assertTrue(rows[0].startswith("| Dekaf"))
        self.assertIn("| 2.00x |", rows[0])

    def test_consumer_table_reports_seed_batch_size(self):
        result = stress_result("Dekaf", effective_rate=2000)
        result["consumerSeedBatchSizeBytes"] = 16_384

        lines = format_throughput_table([result], "Consumer Throughput")

        self.assertIn("16,384B seed batches", lines[0])

    def test_message_bounded_table_ignores_producer_only_median(self):
        lines = format_throughput_table(
            [
                stress_result(
                    "Dekaf",
                    effective_rate=2000,
                    median_rate=900,
                    is_message_bounded=True,
                ),
                stress_result(
                    "Confluent",
                    effective_rate=1000,
                    median_rate=1200,
                    is_message_bounded=True,
                ),
            ],
            "Producer Round-Trip",
            include_ratio=True,
        )

        rows = [line for line in lines if line.startswith("| Dekaf") or line.startswith("| Confluent")]
        dekaf_columns = [column.strip() for column in rows[0].strip("|").split("|")]

        self.assertTrue(rows[0].startswith("| Dekaf"))
        self.assertEqual("-", dekaf_columns[3])
        self.assertIn("| 2.00x |", rows[0])

    def test_transactional_scenario_reports_verification_counts(self):
        transactional = stress_result("Dekaf", effective_rate=750)
        transactional.update({
            "scenario": "producer-transactional",
            "transactionVerification": {
                "acceptedMessages": 1000,
                "committedMessages": 750,
                "abortedMessages": 250,
                "deliveredMessages": 750,
                "duplicateMessages": 0,
                "shortfallMessages": 0,
                "leakedAbortedMessages": 0,
                "unexpectedMessages": 0,
                "missingSentinelPartitions": 0,
                "isSuccessful": True,
            },
        })

        lines = generate_scenario_tables([transactional])
        report = "\n".join(lines)

        self.assertIn("Producer (Transactional EOS)", report)
        self.assertIn("Transaction Verification", report)
        self.assertIn("| Dekaf | 1,000 | 750 | 250 | 750 | 0 | 0 | 0 | 0 | 0 | PASS |", report)

    def test_roundtrip_validation_table_reports_strict_failures(self):
        result = stress_result("Dekaf", effective_rate=1000)
        result["roundTripValidation"] = {
            "expectedMessages": 100,
            "consumedMessages": 99,
            "missingMessages": 1,
            "duplicateMessages": 0,
            "corruptMessages": 0,
            "outOfOrderMessages": 0,
            "mispartitionedMessages": 0,
            "unexpectedMessages": 0,
            "timedOut": False,
            "isSuccess": False,
        }

        lines = format_roundtrip_validation_table([result])

        self.assertIn("Round-Trip Validation", "\n".join(lines))
        self.assertIn("| Dekaf | 100 | 99 | 1 |", "\n".join(lines))
        self.assertIn("| FAIL |", "\n".join(lines))


if __name__ == "__main__":
    unittest.main()
