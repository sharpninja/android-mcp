"""Finite APEX manifest/build metadata discovery across already pinned trees."""
import pathlib,json,collections,datetime
def select_paths(paths):
 paths=set(paths);selected={};binary=[]
 for path in paths:
  pure=pathlib.PurePosixPath(path)
  if pure.name=='apex_manifest.pb':binary.append(path);continue
  if pure.name!='apex_manifest.json':continue
  selected[path]=['literal APEX manifest source candidate']
  for parent in pure.parents:
   build=str(parent/'Android.bp')
   if build in paths:selected.setdefault(build,[]).append('nearest APEX manifest Soong ancestor');break
 return {k:sorted(set(v)) for k,v in selected.items()},sorted(binary)
def main(root):
 root=pathlib.Path(root);sources=[];excluded=[];trees=0
 for directory in (root/'trees',root/'outside-corpus-audit/trees'):
  for filename in sorted(directory.glob('*.json')):
   tree=json.loads(filename.read_text());repo=tree.get('repo',tree.get('repository'));revision=tree['revision'];entries={e['path']:e for e in tree['entries'] if e['kind']=='blob'};trees+=1
   selected,binary=select_paths(entries)
   for path in binary:excluded.append(dict(repository=repo,revision=revision,path=path,reason='Binary protobuf retained in pinned tree enumeration; not downloaded/source-interpreted'))
   for path,reasons in selected.items():
    sources.append(dict(repository=repo,revision=revision,path=path,git_blob=entries[path]['oid'],kind='evidence',scope='aosp-apex-source-version-evidence',api_level=None,channel='unknown',status='not-acquired',url='https://android.googlesource.com/'+repo+'/+/'+revision+'/'+path,license='Applicable original source headers retained; no license acceptance operation',tree_evidence=str(filename.relative_to(root)),selection_reasons=reasons,semantic_status='APEX source name/version declaration; build override and installed product/version unknown'))
 result=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),enumerated_trees=trees,paths=len(sources),unique_objects=len({s['git_blob'] for s in sources}),sources=sources,excluded_binary_manifests=excluded,by_reason=dict(collections.Counter(reason for s in sources for reason in s['selection_reasons'])),global_semantic_complete=False)
 (root/'outside-corpus-audit/source-inventory-batchapex-metadata-v1.json').write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items() if k not in ('sources','excluded_binary_manifests')},indent=2))
if __name__=='__main__':main(pathlib.Path(__file__).resolve().parent)
