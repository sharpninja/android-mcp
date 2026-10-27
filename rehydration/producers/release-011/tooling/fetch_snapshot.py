"""Materialize exact already-selected snapshot blobs; never changes inventory.
Dry-run by default. --download uses the original Google URL at <=1 request/sec,
honors Retry-After on temporary 429, and stops on every other HTTP error.
"""
import pathlib, json, sys, urllib.request, urllib.error, hashlib, base64, time, email.utils, datetime
inv = pathlib.Path(sys.argv[1]).resolve()
root = inv.parent
sources = json.loads(inv.read_text())['sources']
groups = {}
for s in sources:
    if s['status'] == 'downloaded':
        groups.setdefault(s['git_blob'], s)
pending = [s for s in groups.values() if not (root / s['local_path']).exists()]
print('Missing snapshot blobs:', len(pending), flush=True)
if '--download' not in sys.argv:
    raise SystemExit('Dry run; pass --download for permitted source retrieval')
for i, s in enumerate(pending):
    for retry in range(3):
        try:
            response = urllib.request.urlopen(s['url'] + '?format=TEXT', timeout=45)
            data = base64.b64decode(response.read())
            if not hashlib.sha256(data).hexdigest() == s['sha256']:
                raise ValueError("Validation failed: hashlib.sha256(data).hexdigest() == s['sha256']")
            if not hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\x00' + data).hexdigest() == s['git_blob']:
                raise ValueError("Validation failed: hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\\x00' + data).hexdigest() == s['git_blob']")
            p = root / s['local_path']
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_bytes(data)
            break
        except urllib.error.HTTPError as e:
            if e.code != 429 or retry == 2:
                raise
            header = e.headers.get('Retry-After')
            pause = 60 * (retry + 1)
            if header:
                try:
                    pause = max(pause, float(header))
                except ValueError:
                    pause = max(pause, (email.utils.parsedate_to_datetime(header) - datetime.datetime.now(datetime.timezone.utc)).total_seconds())
            while pause > 0:
                step = min(pause, 60)
                time.sleep(step)
                pause -= step
    print(i + 1, '/', len(pending), s['path'], flush=True)
    time.sleep(1)
