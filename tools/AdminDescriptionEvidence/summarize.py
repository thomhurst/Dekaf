"""Publish the assessed first campaign; refuse to reuse its findings for another run."""
import argparse
import json
from pathlib import Path


def summarize(root):
    metadata = json.loads((root / 'provenance.json').read_text())
    if str(metadata['github_run']) != '34157878028':
        raise ValueError('The qualitative assessment in this report applies only to run 34157878028')
    assessment = json.loads((root / 'control-assessment.json').read_text())
    lines = [f"# Administrative comparison {metadata['github_run']}", '',
             '**Verdict: INCONCLUSIVE. No performance tradeoff is approved.**', '',
             f"[Actions run](https://github.com/thomhurst/Dekaf/actions/runs/{metadata['github_run']})", '',
             f"- A1/A2: `{metadata['A']}`", f"- B: `{metadata['B']}`", f"- Harness: `{metadata['harness']}`",
             f"- Main at start/end: `{metadata['main_at_start']}` / `{metadata.get('main_at_end', 'unavailable')}`",
             f"- Runner: `{metadata['image']}` / `{metadata['image_version']}`, `{metadata['platform']}`.",
             '- Hardware: four logical CPUs, AMD EPYC 9V74; exact hardware and SDK/runtime output are in the artifact.',
             f"- Fresh-process workload warmup: {metadata['warmup_seconds']} seconds; measured probe: {metadata['measured_seconds']} seconds.",
             '- Release/net10.0, workstation GC, tiered JIT and PGO enabled. No profiling agent or Kafka broker.', '',
             'The same VM ran A1, B, A2 sequentially, after building and validating both products. '
             'The fixture invokes public administrative APIs against cached protocol responses. '
             'Latency starts before the fixture invocation and ends after its complete result is observed. '
             'The closed-loop load has concurrency one; completed calls, every latency tick bucket and every maximum are retained. '
             'CPU/allocation scope includes process-wide probe bookkeeping. Producer/consumer message metrics and broker CPU are not applicable.', '',
             '## Decision', '',
             '- Inventory p50 is 5.40% above A2 (2.63% above A1), exceeding the predeclared 5% limit against one control.',
             '- Inventory maximum-latency control drift is +5.06%; deletion maximum-latency control drift is -5.24%. Both exceed the 5% bound.',
             '- The first measured interval records 103–110 JIT compilations in the control probes. '
             'Inventory maxima occur in that first interval; legacy-description maxima occur in the second. '
             'Thirty seconds of workload warmup did not establish a clean measurement boundary.',
             '- These findings prevent PASS. They do not establish a confirmed product regression; startup activity, '
             'control drift and precision remain unresolved. No probe samples are trimmed and no controls are averaged.',
             '- This is the first hosted campaign for these product SHAs. Earlier Windows/pre-rebase diagnostics are not comparable controls. '
             'A revised experiment will prime the complete observer/result pipeline before longer workload warmup, retain BDN worker builds, '
             'and disable BDN statistical outlier removal. Product SHAs and acceptance tolerances remain unchanged if main remains current.', '',
             '## Protected control metrics', '',
             'Latency and CPU units are ns/call; allocations are bytes/call. Allocation deltas are absolute bytes; other deltas are percentages.', '',
             '| Case | Metric | A1 | B | A2 | B vs A1 | B vs A2 | A2 vs A1 | Point-estimate concern |',
             '|---|---|---:|---:|---:|---:|---:|---:|---|']
    for case, result in assessment.items():
        for row in result['metrics']:
            allocation = row['metric'] == 'AllocatedBytesPerCall'
            keys = ['B_minus_A1', 'B_minus_A2', 'A2_minus_A1'] if allocation else ['B_vs_A1', 'B_vs_A2', 'A2_vs_A1']
            deltas = [f"{row[key]:+.3f} B" if allocation else f"{row[key] * 100:+.3f}%" for key in keys]
            flags = []
            if row['candidate_exceeds_limit']: flags.append('candidate limit')
            if row['control_drift_exceeds_limit']: flags.append('control drift')
            lines.append('| ' + ' | '.join([case, row['metric'], *[f"{row[key]:,.3f}" for key in ['A1', 'B', 'A2']],
                                            *deltas, ', '.join(flags) or 'none at point estimate']) + ' |')
    lines += ['', '## New capability characterization (B only)', '',
              'These APIs do not exist on A; the rows below are not a before/after comparison.', '',
              '| Case | Completed calls/s | CPU ns/call | p50 ns | p99 ns | Max ns | Bytes/call |',
              '|---|---:|---:|---:|---:|---:|---:|']
    for case in metadata['candidate_only']:
        data = json.loads((root / 'B' / case.replace(':', '-') / 'measured.json').read_text())
        lines.append('| ' + case + ' | ' + ' | '.join(f"{data[key]:,.3f}" for key in
            ['CallsPerSecond', 'CpuNsPerCall', 'P50Ns', 'P99Ns', 'MaxNs', 'AllocatedBytesPerCall']) + ' |')
    lines += ['', '## Completion, startup and finite-interval stability', '',
              'All runs completed without reported unexpected failures/timeouts. Every histogram population equals its completed-call count. '
              'One-second snapshots retain CPU, latency distributions, GC, heap/RSS, JIT and thread-pool activity. '
              'The observer deliberately retains histograms throughout each phase; process memory includes that growing evidence. '
              'These finite intervals do not prove long-run leak absence.', '',
              '| Phase/case | Measured seconds | Completed | Warmup completed | JIT last five warmup rows | JIT first measured row / total | GC 0/1/2 | RSS start/end MiB |',
              '|---|---:|---:|---:|---:|---:|---|---|']
    for phase in ['A1', 'B', 'A2']:
        for path in sorted((root / phase).glob('*/measured.json')):
            data = json.loads(path.read_text())
            warmup = json.loads(path.with_name('warmup.json').read_text())
            start, end = data['Start'], data['End']
            first = data['Intervals'][0]
            warm_jit = sum(row['End']['JitMethods'] - row['Start']['JitMethods'] for row in warmup['Intervals'][-5:])
            jit = f"{first['End']['JitMethods'] - first['Start']['JitMethods']} / {end['JitMethods'] - start['JitMethods']}"
            gc = '/'.join(str(end[f'Gen{i}'] - start[f'Gen{i}']) for i in range(3))
            rss = f"{start['RssBytes'] / 2**20:.2f} / {end['RssBytes'] / 2**20:.2f}"
            lines.append(f"| {phase}/{path.parent.name} | {data['Seconds']:.3f} | {data['Completed']:,} | {warmup['Completed']:,} | {warm_jit} | {jit} | {gc} | {rss} |")
    lines += ['', '## BenchmarkDotNet', '',
              'MemoryDiagnoser allocations are administrative bytes per call. Mean intervals below are BDN 99.9% confidence bounds, '
              'not per-call latency percentiles. This first harness used BDN default outlier handling: reported N can be below the '
              '12 requested measurements. Full raw Measurements and logs remain available, including excluded iterations; these trimmed '
              'summary means are not used to waive any protected probe metric. The revised harness will retain every iteration in its summary.', '',
              '| Phase | Case | Mean ns | Lower ns | Upper ns | Summary N | Bytes/call |',
              '|---|---|---:|---:|---:|---:|---:|']
    for phase in ['A1', 'B', 'A2']:
        for path in sorted((root / phase / 'bdn/results').glob('*-full.json')):
            for benchmark in json.loads(path.read_text())['Benchmarks']:
                stats = benchmark['Statistics']
                ci = stats['ConfidenceInterval']
                lines.append(f"| {phase} | {benchmark['Parameters']} | {stats['Mean']:.3f} | {ci['Lower']:.3f} | {ci['Upper']:.3f} | {stats['N']} | {benchmark['Memory']['BytesAllocatedPerOperation']} |")
    lines += ['', '## Retention', '',
              '[Raw artifact](https://github.com/thomhurst/Dekaf/actions/runs/34157878028/artifacts/10032354642) '
              'contains exact product/harness sources, fixture adaptations, probe/benchmark-host binaries, build/validation logs, '
              'all measured samples and runtime series. The 260-file SHA-256 inventory was verified after download to '
              '`C:/git/Dekaf-evidence/pr-3128/run-34157878028/`. BDN-generated worker build directories were cleaned by BDN; '
              'they are not claimed as retained. The next experiment will explicitly preserve them.']
    return '\n'.join(lines) + '\n'


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('root', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(summarize(args.root), encoding='utf-8')
