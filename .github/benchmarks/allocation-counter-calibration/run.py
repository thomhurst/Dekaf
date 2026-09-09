"""Calibrate allocation-event sums; this never updates a PR acceptance gate."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True, type=Path)
parser.add_argument('--trace', default='dotnet-trace')
args = parser.parse_args()
source = Path(__file__).resolve().parent
output = args.output.resolve()
output.mkdir(parents=True, exist_ok=False)


def run(command, log, environment=None):
    with log.open('w', encoding='utf-8') as stream:
        subprocess.run(list(map(str, command)), stdout=stream, stderr=subprocess.STDOUT,
                       env=environment, cwd=source, check=True)


plan = dict(scope='Local allocation-counter calibration, not product performance acceptance',
            agreement_tolerance=.01, required_events_lost=0, worker_count=4,
            cases=[dict(name=f'datas{mode}-{size}', datas=mode, size=size, serial_bytes=0)
                   for mode in [0, 1] for size in [1000, 100000]] +
                  [dict(name='heap-shrink', datas=1, size=1000, serial_bytes=8_000_000_000)],
            limitation='Agreement in this calibration is not a general error bound or a long-run Linux result')
(output / 'plan.json').write_text(json.dumps(plan, indent=2))
repository_sources = output / 'source/repository'
repository_sources.mkdir(parents=True)
for name in ['global.json', 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props']:
    path = source.parents[2] / name
    if path.exists():
        shutil.copy2(path, repository_sources / name)
shutil.copy2(__file__, output / 'source/run.py')
run(['dotnet', '--info'], output / 'runtime.log')
run([args.trace, '--version'], output / 'trace-version.log')
for name in ['Workload', 'Inspector']:
    run(['dotnet', 'build', source / name / f'{name}.csproj', '-c', 'Release',
         '--no-incremental', '--disable-build-servers'], output / f'{name}-build.log')
    shutil.copytree(source / name / 'bin/Release/net10.0', output / 'binaries' / name)
    target = output / 'source' / name
    target.mkdir(parents=True)
    for path in (source / name).iterdir():
        if path.suffix in {'.cs', '.csproj'}:
            shutil.copy2(path, target / path.name)
run(['dotnet', 'build-server', 'shutdown'], output / 'build-server-shutdown.log')
results = []
for case in plan['cases']:
    folder = output / case['name']
    folder.mkdir()
    env = dict(os.environ, DOTNET_GCDynamicAdaptationMode=str(case['datas']))
    trace = folder / 'capture.nettrace'
    command = [args.trace, 'collect', '--providers',
               'Microsoft-Windows-DotNETRuntime:0x1:5,Dekaf-AllocationCounterCalibration',
               '--buffersize', '256', '--output', trace, '--', 'dotnet',
               output / 'binaries/Workload/Workload.dll', case['size'], folder / 'known.json']
    if case['serial_bytes']:
        command.append(case['serial_bytes'])
    run(command, folder / 'capture.log', env)
    run(['dotnet', output / 'binaries/Inspector/Inspector.dll', trace, folder / 'known.json',
         folder / 'result.json'], folder / 'analysis.log')
    result = json.loads((folder / 'result.json').read_text())
    known = json.loads((folder / 'known.json').read_text())
    if known['ServerGC'] is not True or known['DynamicAdaptationMode'] != str(case['datas']):
        raise ValueError('Runtime configuration does not match the declared case')
    heaps = [item['Heaps'] for item in result['HeapHistory']]
    transitions = [value for index, value in enumerate(heaps) if index == 0 or value != heaps[index - 1]]
    summary = dict(case=case['name'], verdict=result['Verdict'], events_lost=result['EventsLost'],
                   events=result['Events'], known_bytes=result['KnownWorkerAllocatedBytes'],
                   event_bytes=result['EventAllocatedBytes'], error=result['RelativeError'],
                   global_bytes=result['GlobalAllocatedDelta'], heap_transitions=transitions)
    results.append(summary)
    (output / 'summary.json').write_text(json.dumps(results, indent=2))
    print(json.dumps(summary), flush=True)
(output / 'inventory.json').write_text(json.dumps([
    dict(path=str(path.relative_to(output)), bytes=path.stat().st_size,
         sha256=hashlib.sha256(path.read_bytes()).hexdigest())
    for path in output.rglob('*') if path.is_file() and path.name != 'inventory.json'], indent=2))
