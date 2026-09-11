"""Budget selected stress samples, including workload warmup and bounded drains."""

import argparse
import json
import math


def budget(matrix, duration_minutes, warmup_seconds, adaptive_connections=False):
    if not math.isfinite(duration_minutes) or duration_minutes <= 0 or warmup_seconds < 20:
        raise ValueError("Duration must be positive and workload warmup must be at least 20 seconds")
    # Six warmup drains plus the measured drain, each with the existing 30s ceiling.
    drain_minutes = 7 * 30 / 60
    # Includes both product builds, fresh broker starts, teardown and artifact upload.
    setup_minutes = 15
    for lane in matrix["include"]:
        # Outbox measures idle and active phases, each with its own full warmup.
        warmup_minutes = warmup_seconds / 60 * (2 if lane.get("scenario") == "outbox" else 1)
        if lane.get("baseline_sha"):
            segments = 4 if lane.get("aba_second_candidate") else 3
            validation = 2 * (1 + warmup_minutes + drain_minutes)
        elif lane.get("scenario") in (
            "producer", "producer-idempotent", "producer-acks-all",
            "producer-async", "producer-async-idempotent",
        ):
            segments = lane.get("paired_samples", 1) * (2 if lane.get("client") == "all" else 1)
            segments += int(lane.get("run_3conn", False))
            segments += int(lane.get("run_adaptive", False) and not adaptive_connections)
            lane["producer_samples"] = segments
            validation = 0
        elif str(lane.get("scenario", "")).startswith("consumer") or lane.get("scenario") in ("hosted-share", "outbox"):
            segments = lane.get("paired_samples", 1) * (2 if lane.get("client") == "all" else 1)
            # The existing workflow field gates validation and its expected result count.
            lane["producer_samples"] = segments
            validation = 0
        else:
            continue
        required = math.ceil(
            segments * (duration_minutes + warmup_minutes + drain_minutes)
            + validation + setup_minutes
        )
        lane["timeout_minutes"] = max(lane["timeout_minutes"], required)
        if lane["timeout_minutes"] > 360:
            raise ValueError("Requested stress run exceeds the 360-minute hosted job limit; reduce duration or warmup")
    return matrix


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--matrix", required=True)
    parser.add_argument("--duration-minutes", type=float, required=True)
    parser.add_argument("--warmup-seconds", type=int, required=True)
    parser.add_argument("--adaptive-connections", choices=("true", "false"), default="false")
    args = parser.parse_args()
    print(json.dumps(budget(json.loads(args.matrix), args.duration_minutes, args.warmup_seconds,
                            adaptive_connections=args.adaptive_connections == "true")))


if __name__ == "__main__":
    main()
