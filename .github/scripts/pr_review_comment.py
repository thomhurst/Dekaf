"""Validate the review contract and retain proof of a successful posting."""
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
            or data.get('verdict') not in ('CLEAR', 'BLOCKING') or not data.get('comment_url')):
        raise ValueError('no validated review was posted to this pull request')


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
    posted = subprocess.run(['gh', 'pr', 'comment', pr, '--repo', repo, '--body', body],
                            check=True, capture_output=True, text=True)
    url = posted.stdout.strip()
    if not re.fullmatch(r'https://[^/\s]+/' + re.escape(repo) + r'/pull/' + re.escape(pr) + r'#issuecomment-\d+', url):
        raise ValueError('GitHub did not return the posted review comment URL')
    receipt.write_text(json.dumps(dict(repository=repo, pr=pr, verdict=result, comment_url=url)), encoding='utf-8')
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
