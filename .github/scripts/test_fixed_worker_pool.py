import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import unittest


def bash_path():
    if os.name == "nt":
        git = shutil.which("git")
        candidate = Path(git).parent.parent / "bin/bash.exe" if git else None
        return str(candidate) if candidate and candidate.is_file() else None
    return shutil.which("bash")


def workflow_script(step_name):
    workflow = (Path(__file__).resolve().parents[1] / "workflows/stress-tests.yml").read_text(encoding="utf-8")
    step = workflow.split(f"      - name: {step_name}\n", 1)[1].split("      - name:", 1)[0]
    script = re.match(r"(?:^          .*\n|^\n)*", step.split("        run: |\n", 1)[1], re.MULTILINE).group()
    return re.sub(r"^          ", "", script, flags=re.MULTILINE)


def worker_validation_script():
    # Include the dispatch validation that runs before the paid-job configuration.
    return workflow_script("Select lanes for this run").split("# Manual full_run", 1)[0]


class FixedWorkerPoolWorkflowTests(unittest.TestCase):
    @unittest.skipUnless(bash_path(), "bash is required to exercise workflow configuration")
    @unittest.skipUnless(shutil.which("jq"), "jq is required to exercise complete lane selection")
    def test_fixed_worker_configuration_retains_and_initializes_requested_workers(self):
        # Run the actual selector and configuration, with the test interpreter on Windows too.
        script = 'python3() { "$TEST_PYTHON" "$@"; }\n' + workflow_script("Select lanes for this run")
        script += workflow_script("Configure fixed worker-pool experiment")
        for requested in ("0", "0000", "0000000", "1", "08", "032", "00032", "1024", "0000001024"):
            with self.subTest(requested=requested), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                (root / "aba-results").mkdir()
                (root / ".github/scripts").mkdir(parents=True)
                shutil.copyfile(Path(__file__).with_name("stress_timeout.py"), root / ".github/scripts/stress_timeout.py")
                environment = dict(os.environ, FIXED_WORKER_THREADS=requested,
                                   EVENT_NAME="workflow_dispatch", LANE="outbox-1b",
                                   BASELINE_SHA="a" * 40, FULL_RUN="false",
                                   DISPATCH_SHAPE="cheap", PROFILE_MODE="off",
                                   CONSUMER_FETCH_DIAGNOSTICS="false", ADAPTIVE_CONNECTIONS="false",
                                   ABA_SECOND_CANDIDATE="false", DURATION_MINUTES="5",
                                   PRODUCER_WARMUP_SECONDS="180", GITHUB_OUTPUT="output.txt",
                                   TEST_PYTHON=Path(sys.executable).as_posix(),
                                   GITHUB_ENV="github-env.txt", GITHUB_STEP_SUMMARY="summary.txt")
                (root / "workflow.sh").write_text(script, encoding="utf-8", newline="\n")
                result = subprocess.run([bash_path(), "-e", "-o", "pipefail", "workflow.sh"], cwd=root, env=environment,
                                        capture_output=True, encoding="utf-8", timeout=30)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                outputs = dict(line.split("=", 1) for line in (root / "output.txt").read_text().splitlines())
                lanes = json.loads(outputs["matrix"])["include"]
                self.assertEqual(1, len(lanes))
                self.assertEqual("outbox-1b", lanes[0]["lane"])
                self.assertEqual("a" * 40, lanes[0]["baseline_sha"])
                self.assertEqual("false", outputs["full_run"])
                if int(requested) == 0:
                    self.assertFalse((root / "github-env.txt").exists())
                    continue
                configured = dict(line.split("=", 1) for line in (root / "github-env.txt").read_text().splitlines())
                self.assertEqual(int(requested), int(configured["DOTNET_ThreadPool_ForceMinWorkerThreads"], 16))
                self.assertEqual(int(requested), int(configured["DOTNET_ThreadPool_ForceMaxWorkerThreads"], 16))
                self.assertEqual("-1", configured.get("DOTNET_ThreadPool_ThreadTimeoutMs"))
                self.assertEqual(str(int(requested)), configured.get("DEKAF_STRESS_FIXED_WORKER_THREADS"))

    @unittest.skipUnless(bash_path(), "bash is required to exercise workflow validation")
    def test_dispatch_rejects_invalid_worker_counts(self):
        for requested in ("-1", "1025", "01025", "99999", "18446744073709551648",
                          "1+1", "0x20", " 32", "32 ", "1.5", "x", "-0"):
            with self.subTest(requested=requested):
                environment = dict(os.environ, FIXED_WORKER_THREADS=requested,
                                   EVENT_NAME="workflow_dispatch", LANE="outbox-1b",
                                   BASELINE_SHA="a" * 40, FULL_RUN="false")
                result = subprocess.run([bash_path(), "-e", "-o", "pipefail", "-c", worker_validation_script()], env=environment,
                                        capture_output=True, encoding="utf-8", timeout=30)
                self.assertNotEqual(0, result.returncode)
                self.assertIn("fixed_worker_threads must be an integer from 0 to 1024", result.stdout)

    @unittest.skipUnless(bash_path(), "bash is required to exercise workflow validation")
    def test_fixed_workers_require_non_publishing_comparison_after_normalization(self):
        for requested in ("", "0", "0000", "32", "032"):
            for baseline, full_run in (("", "false"), ("a" * 40, "true")):
                with self.subTest(requested=requested, baseline=baseline, full_run=full_run):
                    environment = dict(os.environ, FIXED_WORKER_THREADS=requested,
                                       EVENT_NAME="workflow_dispatch", LANE="outbox-1b",
                                       BASELINE_SHA=baseline, FULL_RUN=full_run)
                    result = subprocess.run([bash_path(), "-e", "-o", "pipefail", "-c", worker_validation_script()], env=environment,
                                            capture_output=True, encoding="utf-8", timeout=30)
                    if int(requested or "0") == 0:
                        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                    else:
                        self.assertNotEqual(0, result.returncode)
                        self.assertIn("A fixed worker pool requires a non-publishing exact-SHA comparison", result.stdout)
