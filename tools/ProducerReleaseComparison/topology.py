"""Keep every VM-reported SMT sibling on the same side of the comparison."""
import json, os, subprocess, sys
from pathlib import Path

def choose(rows):
    groups = {}
    for cpu, core, socket in rows:
        groups.setdefault((socket, core), []).append(cpu)
    if len(groups) < 2: raise RuntimeError('Need separate client and broker core groups')
    client = sorted(groups[sorted(groups)[-1]])
    broker = sorted(cpu for key, cpus in groups.items() for cpu in cpus if cpu not in client)
    if len(client) != 2 or len(broker) < 2:
        raise RuntimeError(f'Expected a two-thread client core, got {client}; broker={broker}')
    return client, broker, groups

if __name__ == '__main__':
    raw = subprocess.check_output(['lscpu', '-p=CPU,CORE,SOCKET'], text=True)
    rows = [tuple(map(int, line.split(','))) for line in raw.splitlines() if line and not line.startswith('#')]
    allowed = os.sched_getaffinity(0)
    rows = [row for row in rows if row[0] in allowed]
    client, broker, groups = choose(rows)
    report = dict(client=client, broker=broker, groups=[dict(socket=s,core=c,cpus=cpus) for (s,c),cpus in groups.items()],
        originalSplitSharesReportedCore=any(set(cpus)&{6,7} and set(cpus)&set(range(6)) for cpus in groups.values()))
    Path(sys.argv[1]).write_text(json.dumps(report, indent=2))
    print('CLIENT_CPUS=' + ','.join(map(str,client)))
    print('BROKER_CPUS=' + ','.join(map(str,broker)))
