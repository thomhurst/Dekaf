"""Exercise the trusted posting entry point without contacting GitHub."""
import hashlib
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
        self.comment = self.folder / 'comment.json'
        gh = self.folder / 'gh'
        gh.write_text(f'#!{sys.executable}\n' + '''import json, os, sys
from pathlib import Path
if os.environ['FAKE_GH_EXIT'] != '0': sys.exit(int(os.environ['FAKE_GH_EXIT']))
if sys.argv[1] == 'api':
    if sys.argv[2] == 'repos/example/repo/pulls/42':
        print(json.dumps({'head': {'sha': os.environ['FAKE_HEAD_SHA']}}))
    elif sys.argv[2] == 'repos/example/repo/issues/comments/123':
        print(Path(os.environ['FAKE_COMMENT']).read_text())
    else: sys.exit(1)
else:
    Path(os.environ['FAKE_GH_ARGUMENTS']).write_text('\\n'.join(sys.argv[1:]) + '\\n')
    Path(os.environ['FAKE_COMMENT']).write_text(json.dumps({
        'body': sys.argv[-1], 'user': {'login': 'github-actions[bot]'},
        'created_at': '2026-09-10T00:01:00Z',
        'issue_url': 'https://api.github.com/repos/example/repo/issues/42'}))
    print(os.environ['FAKE_GH_URL'])
''')
        gh.chmod(0o755)
        # Use the test interpreter when the helper invokes python3.
        python = self.folder / 'python3'
        python.write_text(f'#!/usr/bin/env bash\nexec "{Path(sys.executable).as_posix()}" "$@"\n')
        python.chmod(0o755)
        self.env = dict(os.environ, PATH=str(self.folder) + os.pathsep + os.environ['PATH'],
                        PR_NUMBER='42', GH_REPO='example/repo', REVIEW_RECEIPT=str(self.receipt),
                        FAKE_GH_ARGUMENTS=str(self.arguments), FAKE_GH_EXIT='0',
                        FAKE_COMMENT=str(self.comment), FAKE_HEAD_SHA='a' * 40,
                        REVIEW_HEAD_SHA='a' * 40, REVIEW_STARTED_AT='2026-09-10T00:00:00Z',
                        GITHUB_RUN_ID='1234', GITHUB_RUN_ATTEMPT='2',
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
                binding = '<!-- REVIEW_RUN: 1234/2; HEAD: ' + 'a' * 40 + ' -->\n\n'
                self.assertEqual(self.arguments.read_text(), 'pr\ncomment\n42\n--repo\nexample/repo\n--body\n' + binding + body + '\n')
                self.assertEqual(json.loads(self.receipt.read_text())['verdict'], verdict)
                self.assertEqual(self.verify().returncode, 0)

    def test_failed_post_removes_previous_receipt(self):
        body = 'Reviewed the change.\n<!-- REVIEW_VERDICT: CLEAR -->'
        self.assertEqual(self.post(body).returncode, 0)
        self.env['FAKE_GH_EXIT'] = '1'
        self.assertNotEqual(self.post(body).returncode, 0)
        self.assertFalse(self.receipt.exists())
        self.assertNotEqual(self.verify().returncode, 0)

    def test_review_can_discuss_the_verdict_token(self):
        body = 'The helper must validate the literal `REVIEW_VERDICT:` token.\n<!-- REVIEW_VERDICT: CLEAR -->'
        result = self.post(body)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(self.verify().returncode, 0)

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

    def test_forged_receipt_without_a_github_comment_fails(self):
        self.receipt.write_text(json.dumps(dict(repository='example/repo', pr='42', verdict='CLEAR',
                                               comment_url=self.env['FAKE_GH_URL'], head_sha='a' * 40,
                                               body_sha256=hashlib.sha256(b'Reviewed.\n<!-- REVIEW_VERDICT: CLEAR -->').hexdigest())))
        self.assertNotEqual(self.verify().returncode, 0)

    def test_changed_deleted_stale_or_wrong_author_comment_fails(self):
        for change in ('body', 'deleted', 'created_at', 'user', 'issue_url'):
            with self.subTest(change=change):
                self.assertEqual(self.post('Reviewed.\n<!-- REVIEW_VERDICT: CLEAR -->').returncode, 0)
                data = json.loads(self.comment.read_text())
                if change == 'deleted':
                    self.comment.unlink()
                else:
                    data[change] = {'body': 'Changed.\n<!-- REVIEW_VERDICT: CLEAR -->',
                                    'created_at': '2026-09-09T23:00:00Z',
                                    'user': {'login': 'someone-else'},
                                    'issue_url': 'https://api.github.com/repos/example/repo/issues/43'}[change]
                    self.comment.write_text(json.dumps(data))
                self.assertNotEqual(self.verify().returncode, 0)

    def test_changed_pr_head_fails(self):
        self.assertEqual(self.post('Reviewed.\n<!-- REVIEW_VERDICT: CLEAR -->').returncode, 0)
        self.env['FAKE_HEAD_SHA'] = 'b' * 40
        self.assertNotEqual(self.verify().returncode, 0)

    def test_forged_receipt_cannot_reuse_another_runs_comment(self):
        self.assertEqual(self.post('Reviewed.\n<!-- REVIEW_VERDICT: CLEAR -->').returncode, 0)
        comment = json.loads(self.comment.read_text())
        comment['body'] = comment['body'].replace('REVIEW_RUN: 1234/2;', 'REVIEW_RUN: 9999/2;')
        self.comment.write_text(json.dumps(comment))
        receipt = json.loads(self.receipt.read_text())
        receipt['body_sha256'] = hashlib.sha256(comment['body'].encode()).hexdigest()
        self.receipt.write_text(json.dumps(receipt))
        self.assertNotEqual(self.verify().returncode, 0)


if __name__ == '__main__':
    unittest.main()
