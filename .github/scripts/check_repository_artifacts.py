"""Reject generated evidence and standalone benchmark harnesses in a PR's diff."""
import argparse
import re
import subprocess
from pathlib import PurePosixPath


OUTPUT_DIRECTORIES = {
    'performance-evidence', 'benchmark-results', 'benchmarkresults',
    'benchmarkdotnet.artifacts', 'testresults', '.artifacts', '.performance',
}
OUTPUT_SUFFIXES = ('.log', '.binlog', '.nettrace', '.etlx', '.speedscope.json')
REPORT_NAME = re.compile(r'.*-report-(?:full\.json|github\.md|default\.md)|.*-report\.(?:csv|html)$')
LEGACY_SOURCE_SUFFIXES = ('.cs', '.csproj', '.fsproj', '.vbproj', '.py', '.sh', '.ps1')
TOOL_PROJECTS = {
    f'tools/{name.lower()}/{name.lower()}.csproj' for name in (
        'Dekaf.Benchmarks', 'Dekaf.Fuzzing', 'Dekaf.Pipeline', 'Dekaf.Profiling',
        'Dekaf.StressTests', 'Dekaf.StressTests.Tests', 'Dekaf.TraceAnalyzer',
    )
}


def violation(path, contents=''):
    """Classify repository paths, without rejecting ordinary docs or JSON/CSV fixtures."""
    name = PurePosixPath(path.replace('\\', '/').lower())
    if OUTPUT_DIRECTORIES.intersection(name.parts) or str(name).endswith(OUTPUT_SUFFIXES):
        return 'Generated evidence belongs in GitHub job summaries and uploaded artifacts.'
    if REPORT_NAME.fullmatch(name.name):
        return 'Upload the original BenchmarkDotNet report as a job artifact; do not commit it.'
    if len(name.parts) > 1 and name.parts[0] == 'tools' and name.parts[1].endswith('evidence'):
        return 'Move benchmark coverage into tools/Dekaf.Benchmarks; do not extend an evidence project.'
    if str(name).startswith('.github/benchmarks/') and str(name).endswith(LEGACY_SOURCE_SUFFIXES):
        return 'Legacy executable harnesses cannot be added or extended; use tools/Dekaf.Benchmarks.'
    if name.suffix in ('.csproj', '.fsproj', '.vbproj') and str(name) != 'tools/dekaf.benchmarks/dekaf.benchmarks.csproj':
        if name.parts[0] == 'tools' and str(name) not in TOOL_PROJECTS:
            return 'Use an existing tools project; new standalone tool projects require a reviewed policy change.'
        if ('benchmark' in name.stem or 'harness' in name.stem
                or re.search(r'<(?:PackageReference|Reference)\b[^>]*\bInclude\s*=\s*[\"\']BenchmarkDotNet(?:[.\"\'])', contents, re.I)):
            return 'Use the existing tools/Dekaf.Benchmarks project instead of a standalone benchmark project.'
    return None


def changed_paths(base, head):
    # --no-renames exposes renamed generated files as additions; deletions are allowed.
    data = subprocess.check_output([
        'git', 'diff', '--name-only', '-z', '--no-renames', '--diff-filter=ACMRT',
        f'{base}...{head}', '--',
    ])
    return [path.decode('utf-8') for path in data.split(b'\0') if path]


def annotation(value):
    return value.replace('%', '%25').replace('\r', '%0D').replace('\n', '%0A')


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base', required=True)
    parser.add_argument('--head', default='HEAD')
    args = parser.parse_args(argv)
    failures = []
    for path in changed_paths(args.base, args.head):
        contents = ''
        if PurePosixPath(path).suffix.lower() in ('.csproj', '.fsproj', '.vbproj'):
            contents = subprocess.check_output(['git', 'show', f'{args.head}:{path}']).decode('utf-8-sig')
        reason = violation(path, contents)
        if reason:
            failures.append((path, reason))
            print(f'::error::{annotation(path)}: {annotation(reason)}')
    print(f'Repository artifact policy: {len(failures)} violation(s).')
    return int(bool(failures))


if __name__ == '__main__':
    raise SystemExit(main())
