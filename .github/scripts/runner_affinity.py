"""Keep Kafka clients and broker/build work on separate physical cores."""
import os
from pathlib import Path


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
    # Preserve the original allowed CPUs so nested drivers can pin measured children.
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
