import pathlib,json,hashlib,datetime,sys,collections
R=pathlib.Path(__file__).resolve().parent;D=R/'outside-corpus-audit';batch=sys.argv[1];source=D/('source-inventory-batch'+batch+'.json');suffix=sys.argv[2] if len(sys.argv)>2 else 'reconciled'
if not suffix.replace('-','').isalnum():raise ValueError('Invalid reconciliation suffix')
out=D/('source-inventory-batch'+batch+'-'+suffix+'.json')
if out.exists():raise ValueError('Preserve prior reconciliation')
doc=json.loads(source.read_text())
for s in doc['sources']:
 p=R/'snapshots/blobs'/s['git_blob']
 if not p.exists():continue
 data=p.read_bytes()
 if hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()!=s['git_blob']:raise ValueError('Source cache mismatch '+s['path'])
 s.update(status='downloaded',local_path=str(p.relative_to(R)),sha256=hashlib.sha256(data).hexdigest(),size_bytes=len(data),retrieved_at=datetime.datetime.fromtimestamp(p.stat().st_mtime,datetime.timezone.utc).isoformat())
doc['reconciled_at']=datetime.datetime.now(datetime.timezone.utc).isoformat();out.write_text(json.dumps(doc,indent=2));print(json.dumps(dict(paths=len(doc['sources']),by_status=dict(collections.Counter(s['status'] for s in doc['sources']))),indent=2))
