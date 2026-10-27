"""Bounded first retrieval of SDK snapshot files, never repeat failed requests.
Previous bulk run was stopped before its FIFO queue reached the SDK repository.
Fetch at most one request per second; stop the whole run on any HTTP error.
"""
import pathlib,json,urllib.request,base64,hashlib,time,datetime
R=pathlib.Path(__file__).resolve().parent
sources=json.loads((R/'source-selection.json').read_text())['sources']
receipts=json.loads((R/'download-receipts.json').read_text())
sdk=[s for s in sources if s['repository']=='platform/prebuilts/sdk']
for i,s in enumerate(sdk):
 oid=s['git_blob'];p=R/'snapshots/blobs'/oid
 if p.exists() or oid in receipts:continue
 try:
  b=base64.b64decode(urllib.request.urlopen(s['url']+'?format=TEXT',timeout=30).read())
  assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==oid
  p.write_bytes(b);receipts[oid]=dict(status='downloaded',local_path=str(p.relative_to(R)),sha256=hashlib.sha256(b).hexdigest(),size_bytes=len(b),retrieved_at=datetime.datetime.now(datetime.timezone.utc).isoformat())
  print(i+1,'/',len(sdk),s['path'],len(b),flush=True)
 except Exception as e:
  receipts[oid]=dict(status='unavailable',error=str(e));print('STOP',s['path'],str(e),flush=True);(R/'download-receipts.json').write_text(json.dumps(receipts,indent=2));break
 (R/'download-receipts.json').write_text(json.dumps(receipts,indent=2));time.sleep(1)
