"""Enumerate published interface hash files and nearest build metadata before fetching."""
import pathlib,json,collections,hashlib,datetime
def select_paths(paths,selected):
 paths=set(paths);result={p:['stable AIDL version hash'] for p in paths if pathlib.PurePosixPath(p).name=='.hash' and 'aidl_api' in pathlib.PurePosixPath(p).parts}
 for source in selected:
  pure=pathlib.PurePosixPath(source)
  if pure.suffix not in ('.aidl','.hal'):continue
  parents=list(pure.parents)
  if pure.suffix=='.hal':
   for parent in parents:
    current=str(parent/'current.txt')
    if current in paths:result.setdefault(current,[]).append('HIDL ancestor hash-index candidate')
  for parent in parents:
   found=[str(parent/name) for name in ('Android.bp','Android.mk') if str(parent/name) in paths]
   if found:
    for name in found:result.setdefault(name,[]).append('nearest selected-interface build ancestor')
    break
 return {p:sorted(set(reasons)) for p,reasons in result.items()}
def main(root):
 root=pathlib.Path(root);selected=collections.defaultdict(list)
 for name in ['source-inventory-release008.json','outside-corpus-audit/source-inventory-batchhidl1087-acquired001.json']:
  for s in json.loads((root/name).read_text())['sources']:
   if s['kind']=='aidl' or s['path'].endswith('.hal'):selected[s['repository']].append(s['path'])
 sources=[];trees=0
 for directory in [root/'trees',root/'outside-corpus-audit/trees']:
  for path in sorted(directory.glob('*.json')):
   tree=json.loads(path.read_text());repo=tree.get('repo',tree.get('repository'));revision=tree.get('revision');entries=tree.get('entries',[])
   if not repo or not revision:raise ValueError('Tree lacks immutable repository/revision '+str(path))
   trees+=1;blobs={e['path']:e for e in entries if e.get('kind')=='blob'}
   for filename,reasons in select_paths(blobs,selected[repo]).items():
    entry=blobs[filename];sources.append(dict(repository=repo,revision=revision,path=filename,git_blob=entry['oid'],kind='evidence',scope='aosp-interface-version-evidence',api_level=None,channel='unknown',status='not-acquired',url='https://android.googlesource.com/'+repo+'/+/'+revision+'/'+filename,license='Applicable original source headers retained; no license acceptance operation',tree_evidence=str(path.relative_to(root)),selection_reasons=reasons,semantic_status='Source hash/build declarations pending interpretation; not observed installed module/version/hash'))
 keys=[(s['repository'],s['revision'],s['path']) for s in sources]
 if len(set(keys))!=len(keys):raise ValueError('Duplicate tree/project sources')
 report=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),enumerated_trees=trees,paths=len(sources),by_reason=dict(collections.Counter(reason for s in sources for reason in s['selection_reasons'])),sources=sources,semantic_complete=False,limitations=['Nearest build files do not close all defaults/import/compiler dependencies','Hash files retain source interface-version evidence; compiled runtime identity unknown'])
 out=root/'outside-corpus-audit/source-inventory-batchinterface-metadata-v1.json';out.write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k!='sources'},indent=2))
if __name__=='__main__':main(pathlib.Path(__file__).resolve().parent)
