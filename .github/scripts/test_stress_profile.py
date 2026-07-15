import subprocess
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
SCRIPT = REPO_ROOT / "tools" / "profile-stress-test.sh"
WORKFLOW = REPO_ROOT / ".github" / "workflows" / "stress-tests.yml"


class StressProfileScriptTests(unittest.TestCase):
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

    def test_uses_measured_marker_and_exact_pid_cleanup(self):
        script = SCRIPT.read_text(encoding="utf-8")

        self.assertIn('grep -Eq "$TRACE_START_PATTERN"', script)
        self.assertIn('dotnet-counters collect', script)
        self.assertIn('dotnet-stack report --process-id "$STRESS_PID"', script)
        self.assertIn('dotnet-trace collect', script)
        self.assertIn('taskkill //F //PID "$STRESS_PID"', script)
        self.assertNotIn("//IM", script)


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
        self.assertIn(
            'if [ "$lane" = "all" ] || [ "$client" != "dekaf" ]',
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

    def test_keeps_target_and_profiler_affinity_separate(self):
        self.assertIn('STRESS_CPUSET="$CLIENT_CPUS"', self.workflow)
        self.assertIn('PROFILER_CPUSET="5"', self.workflow)


if __name__ == "__main__":
    unittest.main()
