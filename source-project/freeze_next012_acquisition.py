"""Prepare acquisition-only draft; does not freeze or launch a production build."""
import pathlib,json,datetime,hashlib,copy,collections
from freeze_combined009 import merge_sources,digest
R=pathlib.Path(__file__).resolve().parent
def main():
 output=R/'source-inventory-next012-acquisition-v1.json'
 if output.exists():raise ValueError('Preserve earlier preparation')
 document=json.loads((R/'source-inventory-release011.json').read_text());groups=[('source-inventory-release011.json',copy.deepcopy(document['sources']))];added=[]
 path=R/'outside-corpus-audit/source-inventory-batchapex-metadata-v1-acquired002.json';groups.append((str(path.relative_to(R)),json.loads(path.read_text())['sources']));added.append(dict(path=str(path.relative_to(R)),sha256=digest(path)))
 for batch in ('aidl-hash-generator-v1','aidl-hash-backend-v1'):
  p=R/('outside-corpus-audit/source-inventory-batch'+batch+'.json');records=json.loads(p.read_text())['sources'];receipts=json.loads((R/('outside-corpus-audit/source-receipts-batch'+batch+'-lowrate001.json')).read_text())
  for source in records:
   receipt=receipts[source['git_blob']]
   if receipt['status']!='downloaded':raise ValueError('Incomplete compiler metadata')
   source.update(receipt)
  groups.append((str(p.relative_to(R)),records));added.append(dict(path=str(p.relative_to(R)),sha256=digest(p)))
 sources=merge_sources(groups)
 for ordinal,source in enumerate(sources,1):
  if source['status']!='downloaded' or digest(R/source['local_path'])!=source['sha256']:raise ValueError('Source admission failure '+source['path'])
  if ordinal%10000==0:print('draft source bytes verified',ordinal,'/',len(sources),flush=True)
 document.update(sources=sources,prepared_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),complete=False,producer_readiness='Acquisition-only draft: canonical interface/callback/group/schema integration and full fixture gates must precede a new immutable build')
 document['catalog']['registry_version']=12;document['catalog']['coverage']='PARTIAL selected public-source inventory, including acquired APEX manifest/build and pinned compiler hash semantics; runtime/source gaps remain explicit'
 document.setdefault('inventories',[]).extend(added);document['source_preparation']='freeze_next012_acquisition.py';output.write_text(json.dumps(document,indent=2))
 summary=dict(at=document['prepared_at'],inventory=str(output.relative_to(R)),sha256=digest(output),sources=len(sources),all_selected_paths_downloaded=True,by_kind=dict(collections.Counter(s['kind'] for s in sources)),build_launched=False,active011_unchanged=True,global_semantic_complete=False,producer_readiness=document['producer_readiness'])
 (R/'integration/next012-acquisition-preparation-v1.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
if __name__=='__main__':main()
