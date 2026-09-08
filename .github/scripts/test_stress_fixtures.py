import tempfile
import unittest
from pathlib import Path

from stress_fixtures import BUILD_INPUTS, validate


class StressFixtureTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.candidate = Path(self.directory.name) / "candidate"
        self.baseline = Path(self.directory.name) / "baseline"
        self.candidate.mkdir()
        self.baseline.mkdir()

    def test_identical_build_inputs_are_preserved(self):
        for name in BUILD_INPUTS:
            (self.candidate / name).write_bytes(b"pinned build inputs")
            (self.baseline / name).write_bytes(b"pinned build inputs")
        validate(self.candidate, self.baseline)
        for name in BUILD_INPUTS:
            self.assertEqual(b"pinned build inputs", (self.baseline / name).read_bytes())

    def test_changed_product_inputs_fail_without_modifying_baseline(self):
        for name in BUILD_INPUTS:
            with self.subTest(name=name):
                candidate_path, baseline_path = self.candidate / name, self.baseline / name
                candidate_path.write_bytes(b"candidate package/compiler/SDK settings")
                baseline_path.write_bytes(b"baseline settings")
                with self.assertRaisesRegex(ValueError, "dedicated comparison fixture"):
                    validate(self.candidate, self.baseline)
                self.assertEqual(b"baseline settings", baseline_path.read_bytes())
                candidate_path.unlink()
                baseline_path.unlink()

    def test_added_or_removed_build_inputs_fail(self):
        for root in (self.candidate, self.baseline):
            with self.subTest(root=root.name):
                path = root / "Directory.Build.targets"
                path.write_text("<Project />", encoding="utf-8")
                with self.assertRaises(ValueError):
                    validate(self.candidate, self.baseline)
                path.unlink()

    def test_workflow_does_not_overlay_product_build_inputs(self):
        workflow = (Path(__file__).resolve().parents[1] / "workflows/stress-tests.yml").read_text(encoding="utf-8")
        self.assertIn("python3 .github/scripts/stress_fixtures.py . baseline-source", workflow)
        self.assertLess(workflow.index("stress_fixtures.py"), workflow.index("rsync -a"))
        self.assertNotIn("cp Directory.Build.props Directory.Packages.props global.json baseline-source/", workflow)
        self.assertIn("git -C baseline-source diff --exit-code -- src", workflow)


if __name__ == "__main__":
    unittest.main()
