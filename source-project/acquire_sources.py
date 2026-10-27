"""Download every matching declaration source from the pinned tree inventory.
No API sample selection. Identical Git blobs are transferred only once.
HTTP failures are retained and never bypassed or retried automatically.
"""
import pathlib,json,hashlib,urllib.request,urllib.error,base64,datetime,re,time
from concurrent.futures import ThreadPoolExecutor,as_completed
R=pathlib.Path(__file__).resolve().parent
B=R/'snapshots/blobs';B.mkdir(parents=True,exist_ok=True)
def classify(repo,p):
 if repo=='platform/prebuilts/sdk':
  if p.startswith('37.0/public/api/') and p.endswith('.txt'):return 'sdk-signature' if not p.endswith(('-removed.txt','-incompatibilities.txt')) else 'sdk-history'
  if p=='37.0/public/data/annotations.zip':return 'sdk-annotations'
  if p in ('NOTICE','37.0/README.md','37.0/finalized-flags.txt'):return 'evidence'
  return None
 if p.endswith('.aidl'):return 'aidl'
 if p.endswith('AndroidManifest.xml'):return 'manifest'
 if '/api/' in '/'+p and p.endswith('current.txt'):return 'source-signature'
 if p.split('/')[-1] in ('RequiresPermission.java','EnforcePermission.java','PermissionManuallyEnforced.java','RequiresNoPermission.java','aidl_language.cpp','aidl_language_y.yy','NOTICE','LICENSE','LICENSE.txt'):return 'evidence'
 return None
sources=[];groups={};coverage=[]
for f in sorted((R/'trees').glob('*.json')):
 t=json.loads(f.read_text());selected=[]
 for e in t['entries']:
  kind=classify(t['repo'],e['path'])
  if not kind or e['kind']!='blob':continue
  s=dict(repository=t['repo'],revision=t['revision'],path=e['path'],git_blob=e['oid'],kind=kind,url=f"https://android.googlesource.com/{t['repo']}/+/{t['revision']}/{e['path']}",scope='test-or-example' if re.search(r'(^|/)(tests?|testdata|samples?|examples?|aidl_api)(/|$)',e['path'],re.I) else 'source',api_level='37.0',channel='stable-source-tag',license='See source headers and repository NOTICE/LICENSE; no new license agreement accepted')
  if '/aidl_api/' in '/'+s['path']:s['scope']='frozen-aidl-api'
  sources.append(s);selected.append(s);groups.setdefault(e['oid'],[]).append(s)
 coverage.append(dict(repository=t['repo'],revision=t['revision'],tree_entries=len(t['entries']),selected_files=len(selected),by_kind={k:sum(s['kind']==k for s in selected) for k in sorted(set(s['kind'] for s in selected))}))
(R/'source-selection.json').write_text(json.dumps(dict(policy=classify.__doc__,repositories=coverage,sources=sources),indent=2))
print('selected',len(sources),'source paths;',len(groups),'unique blobs',flush=True)
def fetch(item):
 oid,ss=item;s=ss[0];dest=B/oid
 try:
  if dest.exists():data=dest.read_bytes();retrieved=datetime.datetime.fromtimestamp(dest.stat().st_mtime,datetime.timezone.utc).isoformat()
  else:
   data=base64.b64decode(urllib.request.urlopen(s['url']+'?format=TEXT',timeout=50).read(),validate=False)
   if hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()!=oid:raise ValueError('Git blob hash mismatch')
   dest.write_bytes(data);retrieved=datetime.datetime.now(datetime.timezone.utc).isoformat()
  return oid,dict(status='downloaded',local_path=str(dest.relative_to(R)),sha256=hashlib.sha256(data).hexdigest(),size_bytes=len(data),retrieved_at=retrieved)
 except Exception as e:return oid,dict(status='unavailable',error=str(e))
receipts=json.loads((R/'download-receipts.json').read_text()) if (R/'download-receipts.json').exists() else {};n=0
# Conservative reproduction defaults after the initial collection encountered
# sustained 429. Existing failed receipts are never retried automatically.
for item in groups.items():
 oid=item[0]
 if oid in receipts:continue
 oid,receipt=fetch(item);receipts[oid]=receipt;n+=1
 (R/'download-receipts.json').write_text(json.dumps(receipts,indent=2))
 if receipt['status']!='downloaded':print('STOP',oid,receipt['error'],flush=True);break
 if n%20==0:print('new blobs',n,'/',len(groups),flush=True)
 time.sleep(1)
(R/'download-receipts.json').write_text(json.dumps(receipts,indent=2))
for s in sources:s.update(receipts.get(s['git_blob'],dict(status='not-retrieved',error='No receipt; acquisition stopped or not yet attempted')))
(R/'source-inventory.json').write_text(json.dumps(dict(schema_version='2.0.0',baseline='android-17.0.0_r1',sdk_public_surface='37.0',sources=sources),indent=2))
print('complete',len(sources),'paths',sum(s['status']=='downloaded' for s in sources),'downloaded;',sum(r.get('size_bytes',0) for r in receipts.values()),'bytes',flush=True)
