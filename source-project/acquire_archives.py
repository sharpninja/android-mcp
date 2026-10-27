"""Official Gitiles text-only subtree archives. Same Google host/identity/route.
Bound each archive to 32 MiB compressed/128 MiB expanded. Verify every selected
member against the enumerated Git blob. No filesystem extraction of tar paths.
Stop on any HTTP error and honor Retry-After before a future run.
"""
import pathlib,json,urllib.request,urllib.error,tarfile,io,hashlib,datetime,time,sys
R=pathlib.Path(__file__).resolve().parent
sources=json.loads((R/'source-selection.json').read_text())['sources'];lookup={(s['repository'],s['path']):s for s in sources};receipts=json.loads((R/'download-receipts.json').read_text());candidates=json.loads((R/'archive-candidates.json').read_text())
for c in candidates[:1] if '--probe' in sys.argv else candidates:
 prefix=c['prefix']+'/';targets=[s for s in sources if s['repository']==c['repository'] and s['path'].startswith(prefix) and not (R/'snapshots/blobs'/s['git_blob']).exists()]
 if not targets:continue
 url=f"https://android.googlesource.com/{c['repository']}/+archive/{c['revision']}/{c['prefix']}.tar.gz";attempt=dict(started_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),url=url,repository=c['repository'],revision=c['revision'],prefix=c['prefix'],strategy='official bounded text-subtree archive',selected_pending=len(targets));stop=False
 try:
  response=urllib.request.urlopen(url,timeout=60);data=response.read(32*1024*1024+1)
  if len(data)>32*1024*1024:raise ValueError('Archive exceeds compressed size bound')
  attempt.update(status=response.status,archive_sha256=hashlib.sha256(data).hexdigest(),archive_size_bytes=len(data));expanded=0;stored=0
  with tarfile.open(fileobj=io.BytesIO(data),mode='r:gz') as archive:
   for m in archive:
    if not m.isfile():continue
    expanded+=m.size
    if expanded>128*1024*1024 or m.size>16*1024*1024:raise ValueError('Archive exceeds expanded/member size bound')
    name=m.name[2:] if m.name.startswith('./') else m.name
    if name.startswith('/') or '..' in pathlib.PurePosixPath(name).parts:raise ValueError('Unsafe archive member')
    s=lookup.get((c['repository'],prefix+name))
    if s is None:continue
    b=archive.extractfile(m).read();oid=hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
    if oid!=s['git_blob']:raise ValueError('Git blob mismatch '+name)
    p=R/'snapshots/blobs'/oid
    if not p.exists():p.write_bytes(b);stored+=1
    receipts[oid]=dict(status='downloaded',local_path=str(p.relative_to(R)),sha256=hashlib.sha256(b).hexdigest(),size_bytes=len(b),retrieved_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),transport_url=url)
  attempt.update(expanded_size_bytes=expanded,stored_new_blobs=stored)
 except urllib.error.HTTPError as e:attempt.update(status=e.code,error=str(e),retry_after=e.headers.get('Retry-After'));stop=True
 except Exception as e:attempt.update(status='failed',error=str(e));stop=True
 with (R/'archive-attempts.jsonl').open('a') as f:f.write(json.dumps(attempt)+'\n')
 (R/'download-receipts.json').write_text(json.dumps(receipts,indent=2));print(json.dumps(attempt),flush=True)
 if stop:break
 time.sleep(1)
