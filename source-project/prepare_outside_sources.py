"""Freeze every matching path in a completed outside-corpus audit snapshot."""
import pathlib,json,hashlib,re,datetime,collections,sys
R=pathlib.Path(__file__).resolve().parent;A=R/'outside-corpus-audit';batch=sys.argv[1] if len(sys.argv)>1 else '070';audit=json.loads((A/('classification-batch'+batch+'.json')).read_text());out=A/('source-inventory-batch'+batch+'.json')
if out.exists():raise ValueError('Preserve frozen outside-source inventory')
sources=[]
for p in audit['projects']:
 if p['classification']!='relevant':continue
 for item in p['matches']:
  path=item['path'];oid=item['git_blob'];scope='frozen-aidl-api' if '/aidl_api/' in '/'+path else 'test-or-example' if re.search(r'(^|/)(tests?|examples?|samples?)(/|$)',path,re.I) else 'source'
  s=dict(repository=p['name'],revision=p['revision'],path=path,git_blob=oid,url='https://android.googlesource.com/'+p['name']+'/+/'+p['revision']+'/'+path,kind={'signature':'source-signature','permission-annotation-definition':'evidence'}.get(item['kind'],item['kind']),scope=scope,license='Original headers and repository license evidence; no blanket relicensing',api_level=None,channel='unknown',source_release_context='Pinned additional project discovered from AOSP17r1 manifest; shipping applicability unknown',status='not-retrieved',discovery_evidence=p['tree_evidence'])
  b=R/'snapshots/blobs'/oid
  if b.exists():
   data=b.read_bytes()
   if hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()!=oid:raise ValueError('Existing source cache mismatch')
   s.update(status='downloaded',local_path=str(b.relative_to(R)),sha256=hashlib.sha256(data).hexdigest(),size_bytes=len(data),retrieved_at=datetime.datetime.fromtimestamp(b.stat().st_mtime,datetime.timezone.utc).isoformat())
  sources.append(s)
doc=dict(created_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),complete=False,audit_snapshot='outside-corpus-audit/classification-batch'+batch+'.json',sources=sources)
out.write_text(json.dumps(doc,indent=2));print(json.dumps(dict(paths=len(sources),by_status=dict(collections.Counter(s['status'] for s in sources)),by_kind=dict(collections.Counter(s['kind'] for s in sources))),indent=2))
