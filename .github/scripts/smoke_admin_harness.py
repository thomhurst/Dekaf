"""Build baseline administrative fixtures and replay short captures; never acceptance."""
import importlib.util
from pathlib import Path
import subprocess
from admin_timing import validate_timing


ROOT = Path(__file__).resolve().parents[2]
CASES = {
    'AdminDescriptionEvidence': 'legacy:16',
    'AdminShareOffsetEvidence': 'legacy:1',
    'AdminMemberRemovalEvidence': 'legacy:1',
    'AdminMutationEvidence': 'legacy-create:16',
}


def main():
    for project, case in CASES.items():
        source = ROOT / 'tools' / project
        destination = ROOT / '.artifacts/admin-smoke' / project
        destination.mkdir(parents=True, exist_ok=True)
        commands = [
            ['dotnet', 'build', str(source / 'Runner.csproj'), '-c', 'Release',
             '--disable-build-servers', '-p:Candidate=false'],
            ['dotnet', str(source / 'bin/Release/net10.0/Dekaf.Benchmarks.dll'),
             'probe', case, str(destination), '.2', '.2'],
        ]
        for name, command in zip(('build', 'capture'), commands):
            with (destination / f'{name}.log').open('w', encoding='utf-8') as log:
                subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, check=True)
        mutation = project == 'AdminMutationEvidence'
        spec = importlib.util.spec_from_file_location(
            project, source / ('validate_results.py' if mutation else 'run_comparison.py'))
        validator = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(validator)
        validate = validator.validate if mutation else validator.validate_probe
        for phase in ('warmup', 'measured'):
            validate_timing(validate(destination / f'{phase}.json', .2))
        print(f'{project}: build, capture and accounting replay passed', flush=True)


if __name__ == '__main__':
    main()
