# Pair A1/B/A2 BenchmarkDotNet full-JSON cases and screen each case against the declared tolerances.
#
# Usage:
#   jq -nr --slurpfile a1 A1.json --slurpfile b B.json --slurpfile a2 A2.json \
#      --argjson tolerance 5 [--argjson alloc_floor 8] [--argjson min_warmup 20] \
#      --arg format json|markdown -f compare_bdn_reports.jq
#
# Each input file holds one JSON array of BenchmarkDotNet "Benchmarks" entries, for example
# `jq -s '[.[].Benchmarks[]]' *-report-full.json`. Cases pair by namespace, type, method and
# parameters. Means, allocations and warmup measurements are BenchmarkDotNet's own values;
# nothing is re-estimated.
#
# Arguments:
#   tolerance    percent tolerance on BDN mean time against each control (required).
#   alloc_floor  bytes per operation below which an allocation difference is cold-path or
#                amortized noise (default 0). The smallest managed object is 24 B, so a
#                difference under 8 B/op cannot be a per-operation allocation; it is reported
#                and retained but does not decide the screen when the floor is 8.
#   min_warmup   seconds of elapsed BDN workload warmup each phase must reach per case
#                (default 0). A shortfall makes that case INCONCLUSIVE and names the phase.
#
# Per-case screen, evaluated in this order:
#   INCONCLUSIVE a phase warmed up for less than min_warmup seconds (startup bias possible).
#   REGRESSION   candidate slower than both controls beyond the tolerance, or allocating more
#                bytes per operation than both controls beyond the allocation floor.
#   INCONCLUSIVE candidate slower or allocating more than one control but not the other, and
#                not bracketed by the controls (it lies outside the A1..A2 band).
#   IMPROVEMENT  candidate faster than both controls beyond the tolerance.
#   PASS         within the tolerance against both controls, or bracketed by drifting controls
#                (a candidate between two controls of identical code is not a demonstrated
#                loss; the note records the drift), and not allocating more than both.
# Control drift is retained as a diagnostic and does not override either decisive direction.
# A1 and A2 must report the same case set (same binary). Cases present in only the candidate
# or only the baseline are listed, never compared, and never decide the screen.
# Overall screen: REGRESSION if any case regresses; INCONCLUSIVE if any case is inconclusive;
# otherwise PASS. Scope, precision and correctness still require review.

def named($name; $default): ($ARGS.named[$name] // $default);
def case_key: [.Namespace, .Type, .Method, .Parameters] | map(select(. != null and . != "")) | join(" ");
def allocated: [.Metrics[]? | select(.Descriptor.Id == "Allocated Memory") | .Value] | first;
def warmup_seconds: ([.Measurements[]? | select(.IterationMode == "Workload" and .IterationStage == "Warmup") | .Nanoseconds] | add // 0) / 1e9;
def by_case: map({key: case_key, value: {mean_ns: .Statistics.Mean, stddev_ns: .Statistics.StandardDeviation, n: .Statistics.N,
                                         allocated_bytes: allocated, warmup_seconds: warmup_seconds}}) | from_entries;
def percent(x; control): (x / control - 1) * 100;
def round2: (. * 100 | round) / 100 | if . == 0 then 0 else . end;
def signed: round2 | if . > 0 then "+\(.)%" else "\(.)%" end;
def screen($t; $floor):
  if (.short_warmup | length) > 0 then "INCONCLUSIVE"
  elif (.b_vs_a1_percent > $t and .b_vs_a2_percent > $t)
    or (.alloc_vs_a1_bytes > $floor and .alloc_vs_a2_bytes > $floor) then "REGRESSION"
  elif ((.b_vs_a1_percent > $t or .b_vs_a2_percent > $t) and (.bracketed | not))
    or ((.alloc_vs_a1_bytes > $floor or .alloc_vs_a2_bytes > $floor) and (.alloc_bracketed | not)) then "INCONCLUSIVE"
  elif .b_vs_a1_percent < -$t and .b_vs_a2_percent < -$t then "IMPROVEMENT"
  else "PASS" end;
def notes($t; $floor; $warm):
  [(.short_warmup[] | "warmup \(.) below \($warm) s"),
   (if (.b_vs_a1_percent > $t or .b_vs_a2_percent > $t) and .bracketed
      then "bracketed by controls that drift \(.drift_percent | signed); not a demonstrated loss" else empty end),
   (if (.alloc_vs_a1_bytes > 0 or .alloc_vs_a2_bytes > 0) and (.alloc_vs_a1_bytes <= $floor or .alloc_vs_a2_bytes <= $floor)
      then "allocation delta within \($floor) B/op floor" else empty end),
   (if (.alloc_vs_a1_bytes > $floor or .alloc_vs_a2_bytes > $floor) and .alloc_bracketed
      then "allocation bracketed by drifting controls" else empty end),
   (if .control_drift_exceeds_tolerance then "control drift \(.drift_percent | signed)" else empty end)];

(named("alloc_floor"; 0)) as $floor
| (named("min_warmup"; 0)) as $warm
| ($a1[0] | by_case) as $x | ($b[0] | by_case) as $y | ($a2[0] | by_case) as $z
| if ($x | length) == 0 then error("No measured baseline cases")
  elif ($x | keys) != ($z | keys) then error("Baseline phases A1 and A2 report different case sets")
  else . end
| (($x | keys) - ($y | keys)) as $baseline_only
| (($y | keys) - ($x | keys)) as $candidate_only
| [$x | keys[] | select(. as $k | $y | has($k)) | {case: ., A1: $x[.], B: $y[.], A2: $z[.]}
    | . + {b_vs_a1_percent: percent(.B.mean_ns; .A1.mean_ns),
           b_vs_a2_percent: percent(.B.mean_ns; .A2.mean_ns),
           drift_percent: percent(.A2.mean_ns; .A1.mean_ns),
           alloc_vs_a1_bytes: (.B.allocated_bytes - .A1.allocated_bytes),
           alloc_vs_a2_bytes: (.B.allocated_bytes - .A2.allocated_bytes)}
    | . + {control_drift_exceeds_tolerance: ((.drift_percent | fabs) > $tolerance),
           bracketed: (.B.mean_ns >= ([.A1.mean_ns, .A2.mean_ns] | min) and .B.mean_ns <= ([.A1.mean_ns, .A2.mean_ns] | max)),
           alloc_bracketed: (.B.allocated_bytes >= ([.A1.allocated_bytes, .A2.allocated_bytes] | min)
                             and .B.allocated_bytes <= ([.A1.allocated_bytes, .A2.allocated_bytes] | max)),
           short_warmup: [["A1", "B", "A2"][] as $p | select((.[$p].warmup_seconds) < $warm) | $p]}
    | . + {screen: screen($tolerance; $floor)}
    | . + {notes: notes($tolerance; $floor; $warm)}]
| if length == 0 then error("No common cases between baseline and candidate") else . end
| {tolerance_percent: $tolerance, allocation_floor_bytes: $floor, minimum_warmup_seconds: $warm,
   cases: ., baseline_only: $baseline_only, candidate_only: $candidate_only,
   screen: (if any(.[]; .screen == "REGRESSION") then "REGRESSION"
            elif any(.[]; .screen == "INCONCLUSIVE") then "INCONCLUSIVE"
            else "PASS" end)}
| if $format == "markdown" then
    (["| Case | A1 ns/op | B ns/op | A2 ns/op | B vs A1 | B vs A2 | A1 to A2 drift | Allocated B/op A1 / B / A2 | Warmup s A1 / B / A2 | Screen | Notes |",
      "| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | --- | --- |"]
     + [.cases[] | "| \(.case | gsub("[|]"; "/")) | \(.A1.mean_ns | round2) | \(.B.mean_ns | round2) | \(.A2.mean_ns | round2) | \(.b_vs_a1_percent | signed) | \(.b_vs_a2_percent | signed) | \(.drift_percent | signed) | \(.A1.allocated_bytes) / \(.B.allocated_bytes) / \(.A2.allocated_bytes) | \(.A1.warmup_seconds | round2) / \(.B.warmup_seconds | round2) / \(.A2.warmup_seconds | round2) | \(.screen) | \(.notes | join("; ")) |"]
     + (if (.candidate_only | length) > 0 then ["", "Candidate-only cases (not compared): " + (.candidate_only | join(", "))] else [] end)
     + (if (.baseline_only | length) > 0 then ["", "Baseline-only cases (not compared): " + (.baseline_only | join(", "))] else [] end)
     + ["", "Micro screen: **\(.screen)** at \(.tolerance_percent)% mean time, \(.allocation_floor_bytes) B/op allocation floor and \(.minimum_warmup_seconds) s minimum warmup against both controls. REGRESSION: worse than both controls beyond tolerance. INCONCLUSIVE: worse than one control only while outside the control band, or a phase warmed up for less than the minimum. A candidate bracketed by drifting controls passes with a note. Control drift is diagnostic; review precision, correctness and workload scope."])
    | join("\n")
  else . end
