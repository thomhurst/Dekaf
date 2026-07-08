import unittest

from stress_report import format_throughput_table


def stress_result(client, effective_rate, median_rate=None):
    result = {
        "client": client,
        "durationMinutes": 15,
        "messageSizeBytes": 1000,
        "effectiveMessagesPerSecond": effective_rate,
        "effectiveMegabytesPerSecond": effective_rate * 1000 / (1024 * 1024),
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


if __name__ == "__main__":
    unittest.main()
