"""Budget exact-SHA producer comparisons without changing paid-lane limits."""

import argparse
import json
import math


def budget(matrix, duration_minutes, warmup_seconds):
    if not math.isfinite(duration_minutes) or duration_minutes <= 0 or warmup_seconds < 20:
        raise ValueError("Duration must be positive and producer warmup must be at least 20 seconds")
    # Six warmup drains plus the measured drain, each with the existing 30s ceiling.
    drain_minutes = 7 * 30 / 60
    warmup_minutes = warmup_seconds / 60
    validation_minutes = 2 * (1 + warmup_minutes + drain_minutes)
    # Includes both product builds, fresh broker starts, teardown and artifact upload.
    setup_minutes = 15
    for lane in matrix["include"]:
        if not lane.get("baseline_sha"):
            continue
        segments = 4 if lane.get("aba_second_candidate") else 3
        required = math.ceil(
            segments * (duration_minutes + warmup_minutes + drain_minutes)
            + validation_minutes + setup_minutes
        )
        lane["timeout_minutes"] = max(lane["timeout_minutes"], required)
        if lane["timeout_minutes"] > 360:
            raise ValueError("Requested A-B-A exceeds the 360-minute hosted job limit; reduce duration or warmup")
    return matrix


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--matrix", required=True)
    parser.add_argument("--duration-minutes", type=float, required=True)
    parser.add_argument("--warmup-seconds", type=int, required=True)
    args = parser.parse_args()
    print(json.dumps(budget(json.loads(args.matrix), args.duration_minutes, args.warmup_seconds)))


if __name__ == "__main__":
    main()
