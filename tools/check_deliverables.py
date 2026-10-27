#!/usr/bin/env python3
"""Check the tracked publication surface against an explicit deliverable allowlist."""
import argparse
import fnmatch
import json
from pathlib import Path
import subprocess

def violations(root, paths, policy):
    errors = []
    for name in paths:
        path = Path(name)
        if path.is_absolute() or '..' in path.parts or '\\' in name:
            errors.append(name + ': unsafe path')
            continue
        if not any(fnmatch.fnmatchcase(name, pattern) for pattern in policy['allow']):
            errors.append(name + ': outside deliverable allowlist')
            continue
        if any(fnmatch.fnmatchcase(name, pattern) for pattern in policy['deny']):
            errors.append(name + ': generated, historical or data artifact')
        full = root / path
        if full.is_symlink():
            errors.append(name + ': symlink is not a portable deliverable')
        if full.is_file() and full.stat().st_size > policy['maximum_file_bytes']:
            errors.append(name + ': unexpectedly large publication file')
    return errors

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    args = p.parse_args()
    root = args.root.resolve()
    policy = json.loads((root / 'deliverables.json').read_text())
    result = subprocess.run(['git', '-C', str(root), 'ls-files', '-z'], capture_output=True, check=True)
    paths = [x for x in result.stdout.decode().split('\0') if x]
    errors = violations(root, paths, policy)
    for error in errors:
        print(error)
    print(json.dumps(dict(tracked_files=len(paths),violations=len(errors))))
    raise SystemExit(bool(errors))

if __name__ == '__main__':
    main()
