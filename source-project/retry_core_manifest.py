"""One conservative same-URL retry after a long cooldown from HTTP429.
Rate limiting is distinct from a permission denial. Never bypass HTTP403.
"""
import pathlib,json,urllib.request,urllib.error,base64,hashlib,datetime
R=pathlib.Path(__file__).resolve().parent
s=next(s for s in json.loads((R/'source-selection.json').read_text())['sources'] if s['repository']=='platform/frameworks/base' and s['path']=='core/res/AndroidManifest.xml')
receipts=json.loads((R/'download-receipts.json').read_text())
original=R/'download-receipts.initial-bulk-and-sdk.json'
if not original.exists():original.write_text(json.dumps(receipts,indent=2))
attempt=dict(started_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),url=s['url']+'?format=TEXT',git_blob=s['git_blob'],prior_status=receipts.get(s['git_blob']),reason='Conservative same-URL retry after approximately thirty-minute cooldown from temporary HTTP429; no route/identity change')
try:
 r=urllib.request.urlopen(s['url']+'?format=TEXT',timeout=45);b=base64.b64decode(r.read());assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==s['git_blob'];p=R/'snapshots/blobs'/s['git_blob'];p.write_bytes(b)
 receipt=dict(status='downloaded',local_path=str(p.relative_to(R)),sha256=hashlib.sha256(b).hexdigest(),size_bytes=len(b),retrieved_at=datetime.datetime.now(datetime.timezone.utc).isoformat());receipts[s['git_blob']]=receipt;attempt.update(status=r.status,receipt=receipt)
except urllib.error.HTTPError as e:attempt.update(status=e.code,retry_after=e.headers.get('Retry-After'),error=str(e))
except Exception as e:attempt.update(status='failed',error=str(e))
with (R/'retry-receipts.jsonl').open('a') as f:f.write(json.dumps(attempt)+'\n')
(R/'download-receipts.json').write_text(json.dumps(receipts,indent=2));print(json.dumps(attempt),flush=True)
