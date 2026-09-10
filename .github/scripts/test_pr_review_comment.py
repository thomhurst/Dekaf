"""Exercise the trusted posting entry point without contacting GitHub."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parent
BASH = shutil.which('bash')


@unittest.skipIf(os.name == 'nt', 'The posting helper runs on Linux; use the Linux container test command.')
class ReviewPostingTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.folder = Path(self.directory.name)
        self.arguments = self.folder / 'arguments.txt'
        self.receipt = self.folder / 'receipt.json'
        gh = self.folder / 'gh'
        gh.write_text('#!/usr/bin/env bash\n'
                      'printf "%s\\n" "$@" > "$FAKE_GH_ARGUMENTS"\n'
                      'if [[ ${FAKE_GH_EXIT:-0} != 0 ]]; then exit "$FAKE_GH_EXIT"; fi\n'
                      'printf "%s\\n" "$FAKE_GH_URL"\n')
        gh.chmod(0o755)
        # Use the test interpreter when the helper invokes python3.
        python = self.folder / 'python3'
        python.write_text(f'#!/usr/bin/env bash\nexec "{Path(sys.executable).as_posix()}" "$@"\n')
        python.chmod(0o755)
        self.env = dict(os.environ, PATH=str(self.folder) + os.pathsep + os.environ['PATH'],
                        PR_NUMBER='42', GH_REPO='example/repo', REVIEW_RECEIPT=str(self.receipt),
                        FAKE_GH_ARGUMENTS=str(self.arguments), FAKE_GH_EXIT='0',
                        FAKE_GH_URL='https://github.com/example/repo/pull/42#issuecomment-123')

    def post(self, body):
        return subprocess.run([BASH, str(ROOT / 'pr-review-comment.sh'), body],
                              env=self.env, capture_output=True, text=True)

    def verify(self):
        return subprocess.run([sys.executable, str(ROOT / 'pr_review_comment.py'), '--verify-receipt'],
                              env=self.env, capture_output=True, text=True)

    def test_observed_test_comment_is_rejected_before_github(self):
        result = self.post('test comment body line one\nline two with `code`\n```csharp\nvar x = 1;\n```\nend')
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse(self.arguments.exists())
        self.assertFalse(self.receipt.exists())

    def test_invalid_verdicts_do_not_post(self):
        for body in ('', '--verify-receipt', 'Review without marker', '<!-- REVIEW_VERDICT: CLEAR -->',
                     'Review\n<!-- REVIEW_VERDICT: UNKNOWN -->',
                     'Review\n<!-- REVIEW_VERDICT: CLEAR -->\nMore text',
                     'Review\n<!-- REVIEW_VERDICT: CLEAR -->\n<!-- REVIEW_VERDICT: BLOCKING -->',
                     'Review\n```html\n<!-- REVIEW_VERDICT: CLEAR -->',
                     'Review\n~~~html\n<!-- REVIEW_VERDICT: CLEAR -->',
                     'Review\n````html\n```\n<!-- REVIEW_VERDICT: CLEAR -->'):
            with self.subTest(body=body):
                self.assertNotEqual(self.post(body).returncode, 0)
                self.assertFalse(self.arguments.exists())
                self.assertFalse(self.receipt.exists())

    def test_valid_verdicts_preserve_body_and_fixed_target(self):
        for verdict in ('CLEAR', 'BLOCKING'):
            body = 'Review `code` and $HOME literally.\n```csharp\nvar x = 1;\n```\n\n' + f'<!-- REVIEW_VERDICT: {verdict} -->\n'
            with self.subTest(verdict=verdict):
                result = self.post(body)
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertEqual(self.arguments.read_text(), 'pr\ncomment\n42\n--repo\nexample/repo\n--body\n' + body + '\n')
                self.assertEqual(json.loads(self.receipt.read_text())['verdict'], verdict)
                self.assertEqual(self.verify().returncode, 0)

    def test_failed_post_removes_previous_receipt(self):
        body = 'Reviewed the change.\n<!-- REVIEW_VERDICT: CLEAR -->'
        self.assertEqual(self.post(body).returncode, 0)
        self.env['FAKE_GH_EXIT'] = '1'
        self.assertNotEqual(self.post(body).returncode, 0)
        self.assertFalse(self.receipt.exists())
        self.assertNotEqual(self.verify().returncode, 0)

    def test_missing_malformed_or_wrong_target_receipt_fails(self):
        self.assertNotEqual(self.verify().returncode, 0)
        self.receipt.write_text('not json')
        self.assertNotEqual(self.verify().returncode, 0)
        self.assertEqual(self.post('Reviewed.\n<!-- REVIEW_VERDICT: CLEAR -->').returncode, 0)
        self.env['PR_NUMBER'] = '43'
        self.assertNotEqual(self.verify().returncode, 0)

    def test_invalid_followup_removes_previous_receipt(self):
        self.assertEqual(self.post('Reviewed.\n<!-- REVIEW_VERDICT: CLEAR -->').returncode, 0)
        self.assertNotEqual(self.post('--verify-receipt').returncode, 0)
        self.assertFalse(self.receipt.exists())

    def test_missing_or_wrong_posted_url_does_not_create_receipt(self):
        for url in ('', 'https://github.com/example/repo/pull/43#issuecomment-123', 'not a URL'):
            with self.subTest(url=url):
                self.env['FAKE_GH_URL'] = url
                self.assertNotEqual(self.post('Reviewed.\n<!-- REVIEW_VERDICT: CLEAR -->').returncode, 0)
                self.assertFalse(self.receipt.exists())


if __name__ == '__main__':
    unittest.main()
