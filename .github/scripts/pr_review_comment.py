"""Validate the review contract and retain proof of a successful posting."""
from datetime import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys


def verdict(body):
    lines = body.rstrip().splitlines()
    marker = re.fullmatch(r'<!-- REVIEW_VERDICT: (CLEAR|BLOCKING) -->', lines[-1]) if lines else None
    marker_lines = [line for line in lines if re.fullmatch(r'<!-- REVIEW_VERDICT: .* -->', line)]
    if marker is None or len(marker_lines) != 1:
        raise ValueError('review must end with exactly one CLEAR or BLOCKING verdict marker')
    if not '\n'.join(lines[:-1]).strip():
        raise ValueError('review must contain an explanation before its verdict')
    fence = None
    for line in lines[:-1]:
        match = re.match(r' {0,3}(`{3,}|~{3,})(.*)$', line)
        if match:
            delimiter, suffix = match.groups()
            if fence is None:
                fence = delimiter
            elif delimiter[0] == fence[0] and len(delimiter) >= len(fence) and not suffix.strip():
                fence = None
    if fence is not None:
        raise ValueError('review verdict must be outside a code fence')
    return marker[1]


def verify_receipt(receipt, repo, pr):
    data = json.loads(receipt.read_text(encoding='utf-8'))
    if (not isinstance(data, dict) or data.get('repository') != repo or data.get('pr') != pr
            or data.get('verdict') not in ('CLEAR', 'BLOCKING')
            or data.get('head_sha') != os.environ['REVIEW_HEAD_SHA']):
        raise ValueError('no validated review was posted to this pull request')
    identity = comment_identity(data.get('comment_url', ''), repo, pr)
    comment = github_json(f'repos/{repo}/issues/comments/{identity}')
    api_url = os.environ.get('GITHUB_API_URL', 'https://api.github.com').rstrip('/')
    if (comment['issue_url'] != f'{api_url}/repos/{repo}/issues/{pr}'
            or comment['user']['login'] != 'github-actions[bot]'):
        raise ValueError('review comment has the wrong pull request or author')
    created = datetime.fromisoformat(comment['created_at'].replace('Z', '+00:00'))
    started = datetime.fromisoformat(os.environ['REVIEW_STARTED_AT'].replace('Z', '+00:00'))
    if created < started:
        raise ValueError('review comment predates this invocation')
    body = comment['body']
    if not body.startswith(review_binding() + '\n\n'):
        raise ValueError('review comment belongs to another invocation or head')
    if verdict(body) != data['verdict'] or hashlib.sha256(body.encode()).hexdigest() != data.get('body_sha256'):
        raise ValueError('posted review body differs from the receipt')
    if github_json(f'repos/{repo}/pulls/{pr}')['head']['sha'] != os.environ['REVIEW_HEAD_SHA']:
        raise ValueError('pull request head changed after review started')


def github_json(endpoint):
    result = subprocess.run(['gh', 'api', endpoint], check=True, capture_output=True, text=True)
    return json.loads(result.stdout)


def review_binding():
    return (f'<!-- REVIEW_RUN: {os.environ["GITHUB_RUN_ID"]}/{os.environ["GITHUB_RUN_ATTEMPT"]}; '
            f'HEAD: {os.environ["REVIEW_HEAD_SHA"]} -->')


def comment_identity(url, repo, pr):
    match = re.fullmatch(r'https://[^/\s]+/' + re.escape(repo) + r'/pull/' + re.escape(pr) + r'#issuecomment-(\d+)', url)
    if match is None:
        raise ValueError('GitHub did not return the posted review comment URL')
    return match[1]


def main():
    repo, pr = os.environ['GH_REPO'], os.environ['PR_NUMBER']
    receipt = Path(os.environ['REVIEW_RECEIPT'])
    if sys.argv[1:] == ['--verify-receipt']:
        verify_receipt(receipt, repo, pr)
        return
    # An unsuccessful final attempt must not reuse an earlier successful receipt.
    receipt.unlink(missing_ok=True)
    body = sys.argv[2] if len(sys.argv) == 3 and sys.argv[1] == 'post' else ''
    result = verdict(body)
    # Keep the original Markdown intact and bind the public evidence to this run.
    body = review_binding() + '\n\n' + body
    posted = subprocess.run(['gh', 'pr', 'comment', pr, '--repo', repo, '--body', body],
                            check=True, capture_output=True, text=True)
    url = posted.stdout.strip()
    comment_identity(url, repo, pr)
    receipt.write_text(json.dumps(dict(repository=repo, pr=pr, verdict=result, comment_url=url,
                                      head_sha=os.environ['REVIEW_HEAD_SHA'],
                                      body_sha256=hashlib.sha256(body.encode()).hexdigest())), encoding='utf-8')
    print(url)


if __name__ == '__main__':
    try:
        main()
    except ValueError as error:
        print(f'review posting failed: {error}', file=sys.stderr)
        sys.exit(1)
    except (KeyError, OSError, subprocess.CalledProcessError) as error:
        # Do not echo a failed subprocess command: it includes the entire review body.
        print(f'review posting failed: {type(error).__name__}', file=sys.stderr)
        sys.exit(1)
