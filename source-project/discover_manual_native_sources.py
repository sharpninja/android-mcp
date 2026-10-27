"""Enumerate native candidates for EVERY explicit manual-implementation AIDL marker.
Candidates are evidence, not asserted identity/wire matches. Does not execute or
compile source. Absence of this marker proves nothing about other native Binder.
"""
import pathlib,json,re,hashlib,datetime,collections
R=pathlib.Path(__file__).resolve().parent;A=R/'outside-corpus-audit';out=A/'source-inventory-batchnative-manual-v1.json'
if out.exists():raise SystemExit('Preserve existing source inventory')
inventory=R/'source-inventory-release008.json';signals=[];sources={};trees={}
for s in json.loads(inventory.read_text())['sources']:
 if s['kind']!='aidl':continue
 raw=(R/s['local_path']).read_bytes()
 if hashlib.sha256(raw).hexdigest()!=s['sha256']:raise ValueError('Source bytes changed')
 text=raw.decode('utf8');match=re.search(r'\bmanual(?:ly)?[^.\n]{0,70}implement|\bimplement[^.\n]{0,70}manual',text,re.I)
 if not match:continue
 signal={k:s[k] for k in ('repository','revision','path','sha256')};signal.update(signal=match.group(),line=text[:match.start()].count('\n')+1);signals.append(signal)
 key=(s['repository'],s['revision'])
 if key not in trees:
  name=s['repository'].replace('/','__')+'.json';paths=[R/'trees'/name,A/'trees'/name];p=next((p for p in paths if p.exists()),None)
  if p is None:raise ValueError('Missing pinned project tree')
  tree=json.loads(p.read_text())
  if tree['repo']!=s['repository'] or tree['revision']!=s['revision']:raise ValueError('Tree pin differs from AIDL source')
  trees[key]=(p,tree)
 p,tree=trees[key];stem=pathlib.PurePosixPath(s['path']).stem
 for item in tree['entries']:
  path=pathlib.PurePosixPath(item['path'])
  if item['kind']!='blob' or path.stem!=stem or path.suffix not in ('.h','.hpp','.cpp','.cc','.c'):continue
  source_key=key+(item['path'],)
  row=sources.setdefault(source_key,dict(repository=s['repository'],revision=s['revision'],path=item['path'],git_blob=item['oid'],kind='evidence',scope='aosp-native-binder-candidate-evidence',api_level=s.get('api_level'),channel=s.get('channel') or 'unknown',status='not-acquired',url='https://android.googlesource.com/'+s['repository']+'/+/'+s['revision']+'/'+item['path'],license='Original AOSP source license headers retained; no blanket relicensing or acceptance operation',tree_evidence=str(p.relative_to(R)),linked_aidl_evidence=[],semantic_status='Filename-associated native source candidate; descriptor/transaction/wire identity not established'))
  row['linked_aidl_evidence'].append(signal)
doc=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),source_inventory=dict(path=str(inventory.relative_to(R)),sha256=hashlib.sha256(inventory.read_bytes()).hexdigest()),aidl_files_examined=20210,manual_implementation_markers=signals,paths=len(sources),sources=list(sources.values()),global_complete=False,limitations='Full marker-defined candidate enumeration only; other handwritten/generated Binder and unmarked external declarations remain unexcluded')
out.write_text(json.dumps(doc,indent=2));print(json.dumps(dict(aidl_files_examined=20210,marked_aidl_files=len(signals),candidate_paths=len(sources),sha256=hashlib.sha256(out.read_bytes()).hexdigest()),indent=2))
