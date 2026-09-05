"""Consumer-only adapter: same Pareto rules; producer request size is inapplicable."""
import json
import os
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'scripts'))
import stress_aba

# Keep every protected metric and its threshold. Request size is producer-only.
stress_aba.METRICS = tuple(m for m in stress_aba.METRICS if m.key != 'averageRequest')


def validate(directory):
    result = stress_aba._single_result(directory)
    count = result['throughput']['totalMessages']
    latency = result['latency']
    if count <= 0 or result['throughput']['totalErrors'] != 0:
        raise ValueError(f'Empty or failed consumer: {directory}')
    if latency['count'] != count:
        raise ValueError(f'Latency count mismatch: {directory}: {latency}')
    if result['durationMinutes'] != 5:
        raise ValueError(f'Unexpected duration: {directory}')
    if result['throughput']['elapsedSeconds'] < 299:
        raise ValueError(f'Incomplete measured window: {directory}')
    if (directory / 'broker-state.txt').read_text().strip() != 'running':
        raise ValueError(f'Broker exited: {directory}')
    if 'Validated replays: warmup=' not in (directory / 'run.log').read_text():
        raise ValueError(f'Missing replay validation: {directory}')
    return result


def report(root):
    failed = False
    for number, before, candidate, after in [
        (2993, 'main-a', 'fetch', 'main-b'),
        (2992, 'main-b', 'pool', 'main-c'),
    ]:
        results = [validate(root / name) for name in (before, candidate, after)]
        comparison = stress_aba.compare(*results)
        baseline_sha = (root / before / 'sha.txt').read_text().strip()
        candidate_sha = (root / candidate / 'sha.txt').read_text().strip()
        text = stress_aba.markdown(comparison, baseline_sha, candidate_sha)
        text += '\nConsumer raw replay: 30 seconds warmup, five measured minutes per segment. '
        text += 'Latency is every MoveNextAsync delivery wait, including synchronous completions and replay stalls; '
        text += '0.1 microsecond histogram buckets. Not live-producer message age. '
        text += 'Profiler off; identical timing and offset-validation harness on each SHA. '
        text += 'Maximum latency and throughput slope require separate review.\n'
        (root / f'pareto-{number}.json').write_text(json.dumps(comparison, indent=2))
        (root / f'pareto-{number}.md').write_text(text)
        print(text)
        if summary := os.environ.get('GITHUB_STEP_SUMMARY'):
            with open(summary, 'a') as handle:
                handle.write(text)
        failed |= comparison['verdict'] != 'pass'
    return int(failed)


if __name__ == '__main__':
    if sys.argv[1] == 'validate':
        validate(Path(sys.argv[2]))
    else:
        raise SystemExit(report(Path(sys.argv[2])))
