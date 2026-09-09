"""Isolate harness subprocesses and retain Linux host pressure beside workload evidence."""
import argparse
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import time


def select_affinity(rows):
    cores = {}
    for cpu, core, socket in rows:
        cores.setdefault((socket, core), []).append(cpu)
    if len(cores) < 2:
        raise ValueError('At least two physical cores required for workload isolation')
    groups = [sorted(cores[key]) for key in sorted(cores)]
    return {'consumer': ','.join(map(str, groups[-1])),
            'infrastructure': ','.join(str(cpu) for group in groups[:-1] for cpu in group)}


def topology():
    # The wrapper pins descendants to infrastructure CPUs. Preserve the original allowed
    # set so a nested driver can still discover and explicitly pin its measured child.
    allowed = os.environ.get('DEKAF_RUNNER_CPUS')
    cpus = sorted(map(int, allowed.split(','))) if allowed else sorted(os.sched_getaffinity(0))
    rows = []
    for cpu in cpus:
        folder = Path(f'/sys/devices/system/cpu/cpu{cpu}/topology')
        rows.append((cpu, int((folder / 'core_id').read_text()),
                     int((folder / 'physical_package_id').read_text())))
    return rows


def configure_affinity():
    rows = topology()
    layout = select_affinity(rows)
    os.environ['DEKAF_RUNNER_CPUS'] = ','.join(str(row[0]) for row in rows)
    os.sched_setaffinity(0, set(map(int, layout['infrastructure'].split(','))))
    return rows, layout


def snapshot(proc=Path('/proc')):
    # Raw counters allow later attribution without changing sample inclusion or tolerances.
    values = {'monotonic_ns': time.monotonic_ns(), 'utc_ns': time.time_ns()}
    for name in ('stat', 'meminfo', 'vmstat', 'loadavg', 'pressure/cpu', 'pressure/memory', 'pressure/io'):
        values[name] = (proc / name).read_text()
    disk = os.statvfs('.')
    values['disk_available_bytes'] = disk.f_bavail * disk.f_frsize
    values['sample_end_monotonic_ns'] = time.monotonic_ns()
    return values


def monitor(command, output):
    output.mkdir(parents=True, exist_ok=False)
    rows, layout = configure_affinity()
    (output / 'plan.json').write_text(json.dumps({
        'command': command, 'topology': rows, 'affinity': layout,
        'image': os.getenv('ImageVersion'), 'interval_seconds': 1,
        'scope': 'Host CPU/steal, memory/swap, PSI and disk; includes broker and infrastructure. '
                 'Never substitute host CPU for client CPU per completed operation.'
    }, indent=2))
    child = None
    try:
        with (output / 'series.jsonl').open('w') as stream:
            # Verify required counters before launching expensive work.
            stream.write(json.dumps(snapshot()) + '\n')
            stream.flush()
            child = subprocess.Popen(command, start_new_session=True)
            while True:
                code = child.poll()
                stream.write(json.dumps(snapshot()) + '\n')
                stream.flush()
                if code is not None:
                    (output / 'completion.json').write_text(json.dumps({'exit_code': code}))
                    return code
                time.sleep(1)
    finally:
        if child is not None and child.poll() is None:
            try:
                os.killpg(child.pid, signal.SIGTERM)
            except ProcessLookupError:
                pass  # The child may exit between poll and signal.
            try:
                child.wait(timeout=5)
            except subprocess.TimeoutExpired:
                try:
                    os.killpg(child.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                child.wait()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('command', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ['--'] else args.command
    if not command:
        parser.error('A workload command is required after --')
    sys.exit(monitor(command, args.output))
