"""Reconcile all published AIDL hash directories against complete pinned trees."""
import pathlib,json,collections,datetime,hashlib,re
from aidl_frozen_hash import verify
R=pathlib.Path(__file__).resolve().parent
D=R/'integration/aidl-frozen-hashes-v1';D.mkdir(exist_ok=False)
sources=json.loads((R/'source-inventory-release011.json').read_text())['sources'];by_repo=collections.defaultdict(dict)
for s in sources:by_repo[(s['repository'],s['revision'])][s['path']]=s
hashes={}
for line in (R/'outside-corpus-audit/interface-hash-facts-v1/facts.jsonl').read_text().splitlines():
 v=json.loads(line)
 if v['format']=='published-aidl-hash-hex40':hashes[(v['source']['repository'],v['source']['revision'],v['source']['path'])]=v['source']
trees={}
for folder in ('trees','outside-corpus-audit/trees'):
 for p in (R/folder).glob('*.json'):
  v=json.loads(p.read_text());key=(v.get('repo',v.get('repository')),v['revision'])
  if key in by_repo and any(key==h[:2] for h in hashes):trees[key]=(p,[e['path'] for e in v['entries'] if e['kind']=='blob' and e['path'].endswith('.aidl')])
counts=collections.Counter();results=[]
for (repo,revision,path),identity in hashes.items():
 parent=str(pathlib.PurePosixPath(path).parent);version=parent.rsplit('/',1)[1];tree,paths=trees[(repo,revision)];expected=[v[len(parent)+1:] for v in paths if v.startswith(parent+'/')]
 files={};input_proofs=[];selected=by_repo[(repo,revision)]
 for p,s in selected.items():
  if p.startswith(parent+'/') and p.endswith('.aidl'):
   raw=(R/s['local_path']).read_bytes()
   if hashlib.sha256(raw).hexdigest()!=s['sha256']:raise ValueError('Source bytes changed '+p)
   files[p[len(parent)+1:]]=raw;input_proofs.append({k:s[k] for k in ('repository','revision','path','sha256')})
 hs=selected[path];raw=(R/hs['local_path']).read_bytes()
 if hashlib.sha256(raw).hexdigest()!=hs['sha256']:raise ValueError('Hash source changed')
 try:result=verify(files,expected,version,raw.decode())
 except ValueError as error:result=dict(verified=False,unknown_reason=str(error))
 counts['dump_directories']+=1;counts['verified_hash_directories']+=result['verified'];counts['source_files_in_verified_directories']+=len(files) if result['verified'] else 0
 results.append(dict(repository=repo,revision=revision,dump_directory=parent,hash_source=identity,tree_evidence=str(tree.relative_to(R)),source_inputs=input_proofs,result=result))
with (D/'hashes.jsonl').open('w') as out:
 for item in results:out.write(json.dumps(item,sort_keys=True)+'\n')
report=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),counts=dict(counts),code_sha256=hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest(),hash_algorithm_code_sha256=hashlib.sha256((R/'aidl_frozen_hash.py').read_bytes()).hexdigest(),google_build_source_receipts='outside-corpus-audit/source-receipts-batchaidl-hash-generator-v1-lowrate001.json',tests=7,downloaded_code_executed=False,runtime_observation=False,limits=['Hash input membership/bytes are verified; interface-to-backend hash flag mapping must use pinned compiler source before runtime clause activation','Imports and type dependencies remain separately validated; AIDL version is not Android/APEX version'])
(D/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
