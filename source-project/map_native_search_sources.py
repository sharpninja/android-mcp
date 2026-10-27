"""Resolve public search leads ONLY through exact pinned manifest/tree objects."""
import pathlib,json,hashlib,datetime,collections
def project_for_path(projects,path):
 matches=[p for p in projects if path.startswith(p['path']+'/')]
 return max(matches,key=lambda p:len(p['path'])) if matches else None
def run(root):
 root=pathlib.Path(root);A=root/'outside-corpus-audit';out=A/'source-inventory-batchnative-search303.json'
 if out.exists():raise ValueError('Preserve existing pinned acquisition inventory')
 manifest_path=root/'project-coverage.json';manifest=json.loads(manifest_path.read_text());paths=(A/'native-code-search-303-paths.txt').read_text().splitlines();groups=collections.defaultdict(list);unresolved=[]
 for path in paths:
  project=project_for_path(manifest['projects'],path)
  if project is None:unresolved.append(dict(path=path,reason='No project in pinned baseline manifest'));continue
  groups[project['name']].append((project,path))
 sources=[];trees=[]
 for repo,leads in groups.items():
  name=repo.replace('/','__')+'.json';tree_path=next((p for p in (root/'trees'/name,A/'trees'/name) if p.exists()),None)
  if tree_path is None:raise ValueError('Pinned tree missing for '+repo)
  raw=tree_path.read_bytes();tree=json.loads(raw)
  if tree['repo']!=repo:raise ValueError('Repository/tree identity mismatch')
  entries={e['path']:e for e in tree['entries']};trees.append(dict(path=str(tree_path.relative_to(root)),sha256=hashlib.sha256(raw).hexdigest(),revision=tree['revision']))
  for project,global_path in leads:
   relative=global_path[len(project['path'])+1:];entry=entries.get(relative)
   if entry is None or entry['kind']!='blob':unresolved.append(dict(path=global_path,repository=repo,revision=tree['revision'],reason='Search lead is absent or not a file in exact pinned baseline tree'));continue
   oid=entry['oid'];s=dict(repository=repo,revision=tree['revision'],path=relative,git_blob=oid,git_mode=entry['mode'],kind='evidence',scope='aosp-native-binder-search-candidate-evidence',api_level=None,channel='unknown',status='not-acquired',url='https://android.googlesource.com/'+repo+'/+/'+tree['revision']+'/'+relative,license='Applicable original source/header notices retained; no blanket relicensing or agreement accepted',manifest_path=project['path'],tree_evidence=str(tree_path.relative_to(root)),discovery_path=global_path,discovery_evidence='outside-corpus-audit/native-code-search-303-evidence.json',semantic_status='Public declaration-family match; native signature/descriptor/wire semantics not inferred')
   blob=root/'snapshots/blobs'/oid
   if blob.exists():
    data=blob.read_bytes()
    if hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()!=oid:raise ValueError('Cached Git blob identity mismatch')
    s.update(status='downloaded',local_path=str(blob.relative_to(root)),size_bytes=len(data),sha256=hashlib.sha256(data).hexdigest(),retrieved_at=datetime.datetime.fromtimestamp(blob.stat().st_mtime,datetime.timezone.utc).isoformat())
   sources.append(s)
 doc=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),baseline=manifest['tag'],manifest_commit=manifest['manifest_revision'],discovery_paths=len(paths),pinned_paths=len(sources),sources=sources,unresolved_discovery_paths=unresolved,tree_evidence=trees,global_complete=False,limitations='Search index branch is a discovery lead. Only exact baseline manifest/tree objects can be acquired; no equivalence to another release or global semantic completeness asserted.')
 out.write_text(json.dumps(doc,indent=2));print(json.dumps(dict(paths=len(paths),pinned=len(sources),unresolved=len(unresolved),by_status=dict(collections.Counter(s['status'] for s in sources)),projects=len(groups),sha256=hashlib.sha256(out.read_bytes()).hexdigest()),indent=2));return doc
if __name__=='__main__':run(pathlib.Path(__file__).resolve().parent)
