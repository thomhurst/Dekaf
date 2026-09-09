"""Validate the explicit Reservoir 1.6.7/1.7.0 comparison without editing inputs."""
from pathlib import Path
import sys
from stress_fixtures import BUILD_INPUTS


def validate(candidate, baseline):
    for name in BUILD_INPUTS:
        candidate_path, baseline_path = Path(candidate) / name, Path(baseline) / name
        candidate_bytes = candidate_path.read_bytes() if candidate_path.exists() else None
        baseline_bytes = baseline_path.read_bytes() if baseline_path.exists() else None
        if name == 'Directory.Packages.props':
            before = b'<PackageVersion Include="Reservoir" Version="1.6.7" />'
            after = b'<PackageVersion Include="Reservoir" Version="1.7.0" />'
            if baseline_bytes is None or baseline_bytes.count(before) != 1:
                raise ValueError('Expected exactly one Reservoir 1.6.7 baseline dependency')
            if candidate_bytes != baseline_bytes.replace(before, after):
                raise ValueError('Only the exact Reservoir 1.6.7 to 1.7.0 dependency change is permitted')
        elif candidate_bytes != baseline_bytes:
            raise ValueError(f'Unrelated product build input differs: {name}')


if __name__ == '__main__':
    validate(sys.argv[1], sys.argv[2])
