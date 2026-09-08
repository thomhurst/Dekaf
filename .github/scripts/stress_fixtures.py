"""Reject comparisons that require replacing pinned product build inputs."""

import argparse
from pathlib import Path


BUILD_INPUTS = (
    "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props",
    "global.json", "NuGet.Config", "nuget.config",
)


def validate(candidate, baseline):
    candidate, baseline = Path(candidate), Path(baseline)
    for name in BUILD_INPUTS:
        candidate_path, baseline_path = candidate / name, baseline / name
        candidate_bytes = candidate_path.read_bytes() if candidate_path.exists() else None
        baseline_bytes = baseline_path.read_bytes() if baseline_path.exists() else None
        if candidate_bytes != baseline_bytes:
            raise ValueError(
                f"Product build input {name} differs. Preserve both pinned revisions; "
                "this change needs a dedicated comparison fixture before measurement."
            )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("candidate")
    parser.add_argument("baseline")
    args = parser.parse_args()
    validate(args.candidate, args.baseline)


if __name__ == "__main__":
    main()
