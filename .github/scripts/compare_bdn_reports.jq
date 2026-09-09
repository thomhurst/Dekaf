# Pair A1/B/A2 BenchmarkDotNet full-JSON cases and screen each case against the declared tolerance.
#
# Usage:
#   jq -nr --slurpfile a1 A1.json --slurpfile b B.json --slurpfile a2 A2.json \
#      --argjson tolerance 5 --arg format json|markdown -f compare_bdn_reports.jq
#
# Each input file holds one JSON array of BenchmarkDotNet "Benchmarks" entries, for example
# `jq -s '[.[].Benchmarks[]]' *-report-full.json`. Cases pair by namespace, type, method and
# parameters. Means and allocations are BenchmarkDotNet's own values; nothing is re-estimated.
#
# Per-case screen, evaluated in this order:
#   NOISE        control drift (A2 vs A1) beyond the tolerance; the case cannot be judged.
#   REGRESSION   candidate slower than both controls beyond the tolerance, or allocating more
#                bytes per operation than both controls.
#   INCONCLUSIVE candidate slower than one control beyond the tolerance but not the other.
#   IMPROVEMENT  candidate faster than both controls beyond the tolerance.
#   PASS         within the tolerance against both controls and not allocating more than both.
# Overall screen: REGRESSION if any case regresses; INCONCLUSIVE if any case is NOISE or
# INCONCLUSIVE; otherwise PASS.

def case_key: [.Namespace, .Type, .Method, .Parameters] | map(select(. != null and . != "")) | join(" ");
def allocated: [.Metrics[] | select(.Descriptor.Id == "Allocated Memory") | .Value] | first;
def by_case: map({key: case_key, value: {mean_ns: .Statistics.Mean, n: .Statistics.N, allocated_bytes: allocated}}) | from_entries;
def percent(x; control): (x / control - 1) * 100;
def round2: (. * 100 | round) / 100 | if . == 0 then 0 else . end;
def signed: round2 | if . > 0 then "+\(.)%" else "\(.)%" end;
def screen($t):
  if (.drift_percent | fabs) > $t then "NOISE"
  elif (.b_vs_a1_percent > $t and .b_vs_a2_percent > $t)
    or (.B.allocated_bytes > .A1.allocated_bytes and .B.allocated_bytes > .A2.allocated_bytes) then "REGRESSION"
  elif .b_vs_a1_percent > $t or .b_vs_a2_percent > $t then "INCONCLUSIVE"
  elif .b_vs_a1_percent < -$t and .b_vs_a2_percent < -$t then "IMPROVEMENT"
  else "PASS" end;

($a1[0] | by_case) as $x | ($b[0] | by_case) as $y | ($a2[0] | by_case) as $z
| if ($x | length) == 0 then error("No measured cases")
  elif ($x | keys) != ($y | keys) or ($x | keys) != ($z | keys) then error("Phase case sets differ")
  else . end
| [$x | keys[] | {case: ., A1: $x[.], B: $y[.], A2: $z[.]}
    | . + {b_vs_a1_percent: percent(.B.mean_ns; .A1.mean_ns),
           b_vs_a2_percent: percent(.B.mean_ns; .A2.mean_ns),
           drift_percent: percent(.A2.mean_ns; .A1.mean_ns)}
    | . + {screen: screen($tolerance)}]
| {tolerance_percent: $tolerance, cases: .,
   screen: (if any(.[]; .screen == "REGRESSION") then "REGRESSION"
            elif any(.[]; .screen == "NOISE" or .screen == "INCONCLUSIVE") then "INCONCLUSIVE"
            else "PASS" end)}
| if $format == "markdown" then
    (["| Case | A1 ns/op | B ns/op | A2 ns/op | B vs A1 | B vs A2 | A1 to A2 drift | Allocated B/op A1 / B / A2 | Screen |",
      "| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |"]
     + [.cases[] | "| \(.case | gsub("[|]"; "/")) | \(.A1.mean_ns | round2) | \(.B.mean_ns | round2) | \(.A2.mean_ns | round2) | \(.b_vs_a1_percent | signed) | \(.b_vs_a2_percent | signed) | \(.drift_percent | signed) | \(.A1.allocated_bytes) / \(.B.allocated_bytes) / \(.A2.allocated_bytes) | \(.screen) |"]
     + ["", "Micro screen: **\(.screen)** at \(.tolerance_percent)% mean time and 0 B/op allocation against both controls. REGRESSION: worse than both controls beyond tolerance. NOISE: control drift beyond tolerance. INCONCLUSIVE: worse than one control only."])
    | join("\n")
  else . end
