import json,pathlib,hashlib,datetime,collections
R=pathlib.Path(__file__).resolve().parent
sources=json.loads((R/'source-selection.json').read_text())['sources']
receipts=json.loads((R/'download-receipts.json').read_text())
for s in sources:
 if '/aidl_api/' in '/'+s['path']:s['scope']='frozen-aidl-api'
 p=R/'snapshots/blobs'/s['git_blob']
 if p.exists():
  b=p.read_bytes();s.update(status='downloaded',local_path=str(p.relative_to(R)),size_bytes=len(b),sha256=hashlib.sha256(b).hexdigest(),retrieved_at=datetime.datetime.fromtimestamp(p.stat().st_mtime,datetime.timezone.utc).isoformat())
 else:s.update(receipts.get(s['git_blob'],dict(status='not-retrieved',error='No verified bytes or persisted attempt receipt; acquisition queue stopped. Attempt state at interruption boundary is unknown; no retry performed.')))
(R/'source-inventory.json').write_text(json.dumps(dict(schema_version='2.0.0',baseline='android-17.0.0_r1',sdk_public_surface='37.0',sources=sources),indent=2))
print(dict(collections.Counter(s['status'] for s in sources)))
