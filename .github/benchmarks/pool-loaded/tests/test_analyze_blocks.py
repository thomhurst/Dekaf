import copy
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SOURCE = Path(__file__).resolve().parents[1] / 'analyze-blocks.py'
SPEC = importlib.util.spec_from_file_location('analyze_blocks', SOURCE)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def fixture():
    intervals = [dict(Count=0, MinTicks=0, MaxTicks=0) for _ in range(42)]
    intervals[0] = dict(Count=60, MinTicks=2, MaxTicks=9)
    intervals[10] = dict(Count=40, MinTicks=90, MaxTicks=99)
    blocks = [dict(StartSecond=index * 10, Count=0, LatencyOverflowCount=0, Buckets=[]) for index in range(5)]
    blocks[0].update(Count=60, Buckets=[dict(Index=0, Count=60)])
    blocks[1].update(Count=40, Buckets=[dict(Index=9, Count=40)])
    result = dict(Failure=None, IntervalsStableAfterDrain=True, IntervalCaptureComplete=True,
                  Sent=100, Acknowledged=100, Consumed=100, ConfiguredSeconds=11, Seconds=11.5, OfferedSeconds=11.1,
                  AllocatedBytes=1000, BytesPerCompleted=10, CpuMs=2, CpuUsPerCompleted=20,
                  CompletedPerSecond=100 / 11.5, AllocatedBytesStart=10000, AllocatedBytesEnd=11000,
                  CpuMillisecondsStart=10, CpuMillisecondsEnd=12)
    for boundary in ['Delivery', 'Completion']:
        result[boundary + 'Latency'] = dict(Count=100, MinUs=2, MaxUs=99, P50Us=5, P95Us=95, P99Us=95, OverflowCount=0)
        result[boundary + 'Intervals'] = dict(IntervalSeconds=1, TicksPerSecond=1_000_000,
                                              OutsideCapacity=dict(Count=0, MinTicks=0, MaxTicks=0), Intervals=copy.deepcopy(intervals))
        result[boundary + 'Blocks'] = dict(BlockSeconds=10, BucketWidthUs=10, BucketCount=500_000,
                                           CapacitySeconds=42, OutsideCapacityCount=0, Blocks=copy.deepcopy(blocks))
    return result


class BlockAnalysisTests(unittest.TestCase):
    def test_reconstructs_percentile_bounds_and_preserves_partial_and_empty_blocks(self):
        result = MODULE.analyze(fixture())
        for boundary in ['Delivery', 'Completion']:
            blocks = result['distributions'][boundary]
            self.assertEqual(blocks[0]['p50'], dict(rank=30, lowerUs=0, upperExclusiveUs=10, midpointUs=5))
            self.assertEqual(blocks[1]['p99'], dict(rank=39, lowerUs=90, upperExclusiveUs=100, midpointUs=95))
            self.assertEqual(blocks[1]['observedSeconds'], 1.5)
            self.assertEqual(blocks[1]['maxTicks'], 99)
            self.assertEqual(blocks[2]['observedSeconds'], 0)
            self.assertIsNone(blocks[2]['p50'])
            self.assertEqual(len(blocks), 5)

    def test_rejects_incomplete_and_inconsistent_evidence(self):
        mutations = [
            ('undrained', lambda x: x.update(IntervalsStableAfterDrain=False)),
            ('incomplete', lambda x: x.update(IntervalCaptureComplete=False)),
            ('failed', lambda x: x.update(Failure='timeout')),
            ('completion count', lambda x: x.update(Acknowledged=99)),
            ('nan duration', lambda x: x.update(Seconds=float('nan'))),
            ('negative allocated bytes', lambda x: x.update(AllocatedBytes=-1000, BytesPerCompleted=-10)),
            ('decreasing allocation counter', lambda x: x.update(AllocatedBytesEnd=9000)),
            ('wrong allocation denominator', lambda x: x.update(BytesPerCompleted=11)),
            ('wrong CPU denominator', lambda x: x.update(CpuUsPerCompleted=21)),
            ('decreasing CPU counter', lambda x: x.update(CpuMillisecondsEnd=9)),
            ('wrong throughput denominator', lambda x: x.update(CompletedPerSecond=100)),
            ('capacity overflow', lambda x: x['DeliveryBlocks'].update(OutsideCapacityCount=1)),
            ('interval overflow type', lambda x: x['DeliveryIntervals']['OutsideCapacity'].update(Count=False)),
            ('latency overflow', lambda x: x['DeliveryBlocks']['Blocks'][0].update(LatencyOverflowCount=1)),
            ('missing block', lambda x: x['DeliveryBlocks']['Blocks'].pop()),
            ('block boundary', lambda x: x['DeliveryBlocks']['Blocks'][1].update(StartSecond=9)),
            ('bucket count', lambda x: x['DeliveryBlocks']['Blocks'][0]['Buckets'][0].update(Count=59)),
            ('bucket index', lambda x: x['DeliveryBlocks']['Blocks'][0]['Buckets'][0].update(Index=500_000)),
            ('duplicate bucket', lambda x: x['DeliveryBlocks']['Blocks'][0]['Buckets'].append(dict(Index=0, Count=1))),
            ('global percentile', lambda x: x['CompletionLatency'].update(P99Us=85)),
            ('global maximum', lambda x: x['CompletionLatency'].update(MaxUs=100)),
            ('block maximum', lambda x: x['DeliveryIntervals']['Intervals'][0].update(MaxTicks=11)),
            ('empty extrema', lambda x: x['DeliveryIntervals']['Intervals'][1].update(MaxTicks=1)),
        ]
        for name, mutate in mutations:
            with self.subTest(name=name):
                data = fixture()
                mutate(data)
                with self.assertRaises(ValueError):
                    MODULE.analyze(data)

    def test_equal_total_with_moved_interval_counts_is_rejected(self):
        data = fixture()
        data['DeliveryIntervals']['Intervals'][0]['Count'] = 59
        data['DeliveryIntervals']['Intervals'][10]['Count'] = 41
        with self.assertRaisesRegex(ValueError, 'Block count'):
            MODULE.analyze(data)

    def test_cli_reports_invalid_data_and_preserves_existing_output(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / 'phase.json'
            output = root / 'analysis.json'
            source.write_text(json.dumps(fixture()), encoding='utf-8')
            command = [sys.executable, str(SOURCE), str(source), '--output', str(output)]
            passed = subprocess.run(command, capture_output=True, text=True, encoding='utf-8')
            self.assertEqual(passed.returncode, 0, passed.stderr)
            original = output.read_bytes()
            self.assertEqual(json.loads(original)['status'], 'COLLECTION_VALIDATED')
            duplicate = subprocess.run(command, capture_output=True, text=True, encoding='utf-8')
            self.assertNotEqual(duplicate.returncode, 0)
            self.assertEqual(output.read_bytes(), original)
            invalid = fixture()
            invalid['IntervalCaptureComplete'] = False
            source.write_text(json.dumps(invalid), encoding='utf-8')
            failed_output = root / 'invalid.json'
            command[-1] = str(failed_output)
            failed = subprocess.run(command, capture_output=True, text=True, encoding='utf-8')
            self.assertEqual(failed.returncode, 1)
            self.assertEqual(json.loads(failed_output.read_text(encoding='utf-8'))['status'], 'INVALID_COLLECTION')


if __name__ == '__main__':
    unittest.main()
