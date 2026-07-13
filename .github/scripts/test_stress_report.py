import unittest

from stress_report import (
    format_consumer_fetch_timeline,
    format_roundtrip_validation_table,
    format_connection_scale_timeline,
    format_producer_request_diagnostics,
    format_throughput_table,
    generate_scenario_tables,
    intra_run_throughput,
    paired_latency_thresholds,
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
    def test_producer_request_diagnostics_surface_per_broker_fragmentation(self):
        value = stress_result("Dekaf", effective_rate=1400)
        value["producerDeliveryDiagnostics"] = {
            "brokerProduceRequests": [{
                "brokerId": 1,
                "requestCount": 2500,
                "requestsPerSecond": 500,
                "averageRequestBytes": 262144,
            }]
        }

        report = "\n".join(format_producer_request_diagnostics([value], "Producer"))

        self.assertIn("Producer Request Diagnostics - Producer", report)
        self.assertIn("| Dekaf | 1 | 2,500 | 500.00 | 256.00 KB |", report)
    def test_consumer_fetch_timeline_surfaces_published_diagnostics(self):
        value = stress_result("Dekaf", effective_rate=2500)
        value["scenario"] = "consumer"
        value["throughput"]["intervalSamples"] = [{
            "capturedAtUtc": "2026-07-13T10:01:00+00:00",
            "elapsedSeconds": 60,
            "messagesPerSecond": 2500,
        }]
        value["consumerFetchDiagnostics"] = {
            "samples": [{
                "capturedAtUtc": "2026-07-13T10:01:00+00:00",
                "intervalSeconds": 60,
                "fetchRequestsPerSecond": 2,
                "bytesPerFetch": 524288,
                "averageFetchRttMs": 7.5,
                "pendingFetchDepth": 2,
                "prefetchBufferDepth": 3,
                "prefetchDepth": 5,
                "prefetchedBytes": 8388608,
            }],
            "connectionReapEvents": [{
                "occurredAtUtc": "2026-07-13T10:00:55+00:00",
            }],
        }

        report = "\n".join(format_consumer_fetch_timeline([value], "Consumer"))
        published_report = "\n".join(generate_scenario_tables([value]))

        self.assertIn("Consumer Fetch Timeline - Consumer", report)
        self.assertIn("512.00 KB", report)
        self.assertIn("7.50ms", report)
        self.assertIn("2 / 3 / 5", report)
        self.assertIn("8.00 MB", report)
        self.assertIn("1 reap", report)
        self.assertIn("60s / 2,500 msg/s", report)
        self.assertIn("Consumer Fetch Timeline - Consumer", published_report)

    def test_connection_scale_timeline_correlates_nearest_throughput_sample(self):
        value = stress_result("Dekaf", effective_rate=1400)
        value["throughput"]["intervalSamples"] = [
            {
                "capturedAtUtc": "2026-07-12T02:20:40+00:00",
                "elapsedSeconds": 10.0,
                "messagesPerSecond": 1000.0,
            },
            {
                "capturedAtUtc": "2026-07-12T02:20:50+00:00",
                "elapsedSeconds": 20.0,
                "messagesPerSecond": 2000.0,
            },
        ]
        value["producerDeliveryDiagnostics"] = {
            "connectionScaleEvents": [{
                "occurredAtUtc": "2026-07-12T02:20:47+00:00",
                "brokerId": 1,
                "oldConnectionCount": 1,
                "newConnectionCount": 2,
                "bufferUtilization": 0.75,
                "bufferPressureDelta": 120,
                "sendLoopPressureDelta": 150,
            }]
        }

        report = "\n".join(format_connection_scale_timeline([value], "Producer"))

        self.assertIn("1→2", report)
        self.assertIn("20.0s / 2,000 msg/s", report)
        self.assertIn("120/150", report)

    def test_scenario_tables_surface_latency_outlier_diagnostics(self):
        value = stress_result("Dekaf", effective_rate=1400)
        value["scenario"] = "producer"
        value["throughput"]["intervalSamples"] = [
            {
                "capturedAtUtc": "2026-07-12T02:20:42+00:00",
                "elapsedSeconds": 2.0,
                "messagesPerSecond": 1400.0,
                "gen2Collections": 0,
                "gcPauseDurationMs": 0,
            },
            {
                "capturedAtUtc": "2026-07-12T02:20:46+00:00",
                "elapsedSeconds": 6.0,
                "messagesPerSecond": 1300.0,
                "gen2Collections": 1,
                "gcPauseDurationMs": 12.5,
            },
        ]
        value["latency"] = {
            "droppedOutlierSamples": 2,
            "outlierSamples": [{
                "messageIndex": 42,
                "startedAtUtc": "2026-07-12T02:20:44+00:00",
                "completedAtUtc": "2026-07-12T02:20:46+00:00",
                "latencyUs": 2_000_000,
            }],
        }

        report = "\n".join(generate_scenario_tables([value]))

        self.assertIn("Delivery Latency Outliers", report)
        self.assertIn("| Dekaf | 42 |", report)
        self.assertIn("GC pause", report)
        self.assertIn("Gen2 +1 / pause +12.5ms", report)
        self.assertIn("2 additional latency outlier sample(s)", report)

    def test_paired_latency_thresholds_flag_high_p99(self):
        confluent = stress_result("Confluent", effective_rate=1000)
        confluent.update({
            "scenario": "producer-acks-all",
            "brokerCount": 3,
            "latency": {"p50Us": 7_250, "p99Us": 39_550},
        })
        dekaf = stress_result("Dekaf", effective_rate=1400)
        dekaf.update({
            "scenario": "producer-acks-all",
            "brokerCount": 3,
            "latency": {"p50Us": 48_850, "p99Us": 3_019_550},
        })

        evaluations = paired_latency_thresholds([confluent, dekaf])

        self.assertEqual(2, len(evaluations))
        by_metric = {item["metric"]: item for item in evaluations}
        self.assertTrue(by_metric["latencyP50Ratio"]["thresholdBreach"])
        self.assertTrue(by_metric["latencyP99Ratio"]["thresholdBreach"])
        self.assertAlmostEqual(
            3_019_550 / 39_550,
            by_metric["latencyP99Ratio"]["current"],
        )

    def test_paired_latency_thresholds_ignore_unpaired_and_confluent_results(self):
        unpaired = stress_result("Dekaf", effective_rate=1400)
        unpaired["latency"] = {"p50Us": 50_000, "p99Us": 3_000_000}

        self.assertEqual([], paired_latency_thresholds([unpaired]))

    def test_paired_latency_thresholds_enforce_absolute_p95_target(self):
        dekaf = stress_result("Dekaf", effective_rate=1400)
        dekaf.update({
            "latency": {"p50Us": 5_000, "p95Us": 31_000, "p99Us": 40_000},
            "deliveryLatencyTargetMs": 10,
        })

        evaluations = paired_latency_thresholds([dekaf])

        self.assertEqual(1, len(evaluations))
        evaluation = evaluations[0]
        self.assertEqual("latencyP95TargetRatio", evaluation["metric"])
        self.assertAlmostEqual(3.1, evaluation["current"])
        self.assertEqual(3.0, evaluation["upper"])
        self.assertTrue(evaluation["thresholdBreach"])

    def test_paired_latency_thresholds_enforce_target_without_pairing_multi_connection_results(self):
        confluent = stress_result("Confluent", effective_rate=1000)
        confluent["latency"] = {"p50Us": 10_000, "p99Us": 50_000}
        multi_connection = stress_result("Dekaf (3conn)", effective_rate=2000)
        multi_connection.update({
            "latency": {"p50Us": 50_000, "p95Us": 31_000, "p99Us": 500_000},
            "deliveryLatencyTargetMs": 10,
        })

        evaluations = paired_latency_thresholds([confluent, multi_connection])

        self.assertEqual(1, len(evaluations))
        self.assertEqual("latencyP95TargetRatio", evaluations[0]["metric"])
        self.assertTrue(evaluations[0]["thresholdBreach"])

    def test_paired_latency_thresholds_require_matching_roundtrip_bound(self):
        confluent = stress_result("Confluent", effective_rate=1000)
        confluent.update({
            "scenario": "producer-roundtrip",
            "roundTripValidation": {"expectedMessages": 250_000},
            "latency": {"p50Us": 10_000, "p99Us": 50_000},
        })
        dekaf = stress_result("Dekaf", effective_rate=1400)
        dekaf.update({
            "scenario": "producer-roundtrip",
            "roundTripValidation": {"expectedMessages": 1_000_000},
            "latency": {"p50Us": 50_000, "p99Us": 500_000},
        })

        self.assertEqual([], paired_latency_thresholds([confluent, dekaf]))

    def test_intra_run_throughput_detects_front_loaded_collapse(self):
        value = stress_result("Dekaf", effective_rate=1400)
        value.update({
            "steadyStatePeakRatio": 0.62,
            "intraRunDriftPercent": -31.03,
            "throughputSlopePercentPerMinute": -4.2,
            "steadyStatePeakRatioThreshold": 0.85,
            "throughputSlopePercentPerMinuteThreshold": -1.0,
            "steadyStatePeakThresholdBreached": True,
            "throughputSlopeThresholdBreached": True,
            "intraRunThroughputThresholdBreached": True,
        })
        value["throughput"]["messagesPerSecondSamples"] = [1400, 1400, 1400]

        metrics = intra_run_throughput(value)

        self.assertIsNotNone(metrics)
        self.assertAlmostEqual(-31.03, metrics["driftPercent"], places=2)
        self.assertLess(metrics["steadyStatePeakRatio"], 0.85)
        self.assertLess(metrics["slopePercentPerMinute"], -1.0)
        self.assertTrue(metrics["thresholdBreached"])

    def test_throughput_table_surfaces_drift_and_slope(self):
        value = stress_result("Dekaf", effective_rate=1400)
        value.update({
            "steadyStatePeakRatio": 0.6,
            "intraRunDriftPercent": -35.9,
            "throughputSlopePercentPerMinute": -8.0,
            "steadyStatePeakRatioThreshold": 0.85,
            "throughputSlopePercentPerMinuteThreshold": -1.0,
            "steadyStatePeakThresholdBreached": True,
            "throughputSlopeThresholdBreached": True,
            "intraRunThroughputThresholdBreached": True,
        })

        report = "\n".join(format_throughput_table([value], "Producer Throughput"))

        self.assertIn("Drift", report)
        self.assertIn("Slope %/min", report)
        self.assertIn("-35.9%", report)

    def test_throughput_table_surfaces_request_cpu_and_standing_cores(self):
        value = stress_result("Dekaf", effective_rate=1400)
        value.update({
            "cpuTimeSeconds": 0.2,
            "produceRequestCount": 500,
        })

        report = "\n".join(format_throughput_table([value], "Producer Throughput"))

        self.assertIn("CPU μs/request", report)
        self.assertIn("Standing cores", report)
        self.assertIn("| 200.00 | 400.00 |", report)

    def test_intra_run_throughput_accepts_stable_samples(self):
        value = stress_result("Dekaf", effective_rate=1400)
        value.update({
            "steadyStatePeakRatio": 0.99,
            "intraRunDriftPercent": 0.0,
            "throughputSlopePercentPerMinute": 0.0,
            "steadyStatePeakRatioThreshold": 0.85,
            "throughputSlopePercentPerMinuteThreshold": -1.0,
            "steadyStatePeakThresholdBreached": False,
            "throughputSlopeThresholdBreached": False,
            "intraRunThroughputThresholdBreached": False,
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
        self.assertEqual("-", dekaf_columns[4])
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
