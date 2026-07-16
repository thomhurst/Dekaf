import subprocess
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
SCRIPT = REPO_ROOT / "tools" / "profile-stress-test.sh"
WORKFLOW = REPO_ROOT / ".github" / "workflows" / "stress-tests.yml"


class StressProfileScriptTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.script = SCRIPT.read_text(encoding="utf-8")

    def run_validation(self, windows):
        command = (
            f'PROFILE_VALIDATE_ONLY=true TRACE_WINDOWS="{windows}" '
            "bash tools/profile-stress-test.sh "
            "--duration 15 --scenario producer --client dekaf"
        )
        return subprocess.run(
            ["bash", "-c", command],
            capture_output=True,
            cwd=REPO_ROOT,
            text=True,
            check=False,
        )

    def test_accepts_sorted_windows_inside_measured_duration(self):
        result = self.run_validation(
            "60:30:healthy,330:30:pre-collapse,420:30:collapse,600:30:degraded"
        )

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("Valid profile", result.stdout)

    def test_rejects_overlapping_windows(self):
        result = self.run_validation("60:30:healthy,80:30:overlap")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("sorted and non-overlapping", result.stderr)

    def test_rejects_window_after_measured_duration(self):
        result = self.run_validation("895:30:too-late")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("ends after", result.stderr)

    def test_rejects_duplicate_window_labels(self):
        result = self.run_validation("60:30:same,120:30:same")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("Duplicate window label", result.stderr)

    def test_rejects_invalid_stack_snapshot_count(self):
        command = (
            'PROFILE_VALIDATE_ONLY=true PROFILE_STACK_SNAPSHOTS=0 '
            "bash tools/profile-stress-test.sh "
            "--duration 15 --scenario producer --client dekaf"
        )
        result = subprocess.run(
            ["bash", "-c", command],
            capture_output=True,
            cwd=REPO_ROOT,
            text=True,
            check=False,
        )

        self.assertNotEqual(0, result.returncode)
        self.assertIn("PROFILE_STACK_SNAPSHOTS", result.stderr)

    def test_uses_measured_marker_and_exact_pid_cleanup(self):
        script = self.script

        self.assertIn('grep -Eq "$TRACE_START_PATTERN"', script)
        self.assertIn('dotnet-counters collect', script)
        self.assertIn('dotnet-stack report --process-id "$STRESS_PID"', script)
        self.assertIn('dotnet-trace collect', script)
        self.assertIn('taskkill //F //PID "$STRESS_PID"', script)
        self.assertNotIn("//IM", script)

    def test_collects_dekaf_meter_alongside_runtime_counters(self):
        script = self.script

        self.assertIn('PROFILE_COUNTER_PROVIDERS="${PROFILE_COUNTER_PROVIDERS:-System.Runtime,Dekaf}"', script)
        self.assertIn('--counters "$PROFILE_COUNTER_PROVIDERS"', script)
        self.assertIn('counters_start_epoch=', script)

    def test_full_profile_is_verbose_and_includes_tpl_provider(self):
        script = self.script

        # Level 5 is load-bearing: GCAllocationTick is verbose-only, so a level-4
        # "full" profile silently drops allocation-by-type data.
        self.assertIn("0x000000000049C001:5", script)
        self.assertIn("System.Threading.Tasks.TplEventSource:0x1C3:4", script)

    def test_spreads_stack_snapshots_inside_traced_window(self):
        script = self.script

        self.assertIn('${label}.stacks.${snap}.txt', script)
        self.assertIn("snapshot_gap", script)

    def test_gcdump_is_opt_in_and_captured_after_the_trace(self):
        script = self.script

        self.assertIn('PROFILE_GCDUMP="${PROFILE_GCDUMP:-false}"', script)
        self.assertIn("dotnet-gcdump collect", script)
        # Ordering matters: the gcdump-induced blocking GC must not pollute the
        # traced window, so collection happens after the trace wait completes.
        self.assertLess(
            script.index('wait "$trace_pid"'),
            script.index("dotnet-gcdump collect"),
        )

    def test_generates_event_aggregation_and_summary(self):
        script = self.script

        # The analyzer is built once up front (fail-fast, no per-window MSBuild)
        # and aggregation is gated on the providers actually carrying CLR events.
        self.assertIn('ANALYZER_EXE=', script)
        self.assertIn('dotnet build "$REPO_ROOT/tools/Dekaf.TraceAnalyzer"', script)
        self.assertIn('*Microsoft-Windows-DotNETRuntime*', script)
        self.assertIn('"$ANALYZER_EXE" "$trace_path"', script)
        self.assertIn("stress_profile_summary.py", script)
        self.assertIn("profile-summary.md", script)


class StressProfileWorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.workflow = WORKFLOW.read_text(encoding="utf-8")

    def test_exposes_reusable_profile_inputs(self):
        for profile_input in (
            "profile_mode:",
            "profile_windows:",
            "profile_counters:",
            "profile_stacks:",
        ):
            self.assertIn(profile_input, self.workflow)

    def test_profiles_only_one_explicit_dekaf_lane(self):
        # Normal manual dispatches are Dekaf-only; full_run forces lane=all, so
        # the same guard also rejects profiling on a publish run.
        self.assertIn(
            'if [ "$profile_mode" != "off" ] && [ "$lane" = "all" ]',
            self.workflow,
        )
        self.assertIn('.paired_samples = 1', self.workflow)
        self.assertIn('.run_3conn = false', self.workflow)
        self.assertIn('.artifact_suffix = "-profile"', self.workflow)

    def test_installs_pinned_tools_only_for_profile_dispatch(self):
        self.assertIn(
            "if: github.event_name == 'workflow_dispatch' && github.event.inputs.profile_mode != 'off'",
            self.workflow,
        )
        self.assertIn(
            "dotnet tool install --global dotnet-counters --version 9.0.661903",
            self.workflow,
        )
        self.assertIn(
            "dotnet tool install --global dotnet-trace --version 9.0.661903",
            self.workflow,
        )
        self.assertIn(
            "dotnet tool install --global dotnet-gcdump --version 9.0.661903",
            self.workflow,
        )

    def test_gcdump_capture_is_wired_for_gc_dispatches(self):
        self.assertIn(
            'PROFILE_GCDUMP="$([ "$PROFILE_MODE" = "gc" ] && echo true || echo false)"',
            self.workflow,
        )

    def test_extra_args_stay_separate_argv_entries(self):
        # A space-separated EXTRA_ARGS string appended as one quoted element
        # reaches the client as a single unparseable argument; the array keeps
        # multi-token additions safe on both the paired and 3conn paths.
        self.assertIn("EXTRA_ARGS=()", self.workflow)
        self.assertIn('STRESS_ARGS+=("${EXTRA_ARGS[@]}")', self.workflow)
        self.assertIn('"${EXTRA_ARGS[@]}" \\', self.workflow)

    def test_keeps_target_and_profiler_affinity_separate(self):
        self.assertIn('STRESS_CPUSET="$CLIENT_CPUS"', self.workflow)
        self.assertIn('PROFILER_CPUSET="5"', self.workflow)


if __name__ == "__main__":
    unittest.main()
