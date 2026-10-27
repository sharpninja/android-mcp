"""Freeze corrected source applicability plus complete finite metadata followups."""
import pathlib,json,hashlib,datetime,collections,copy
from freeze_combined009 import merge_sources,digest
R=pathlib.Path(__file__).resolve().parent
def main():
 output=R/'source-inventory-release011.json'
 if output.exists():raise FileExistsError('Preserve existing source freeze')
 document=json.loads((R/'source-inventory-release010.json').read_text());base=copy.deepcopy(document['sources']);corrected=0
 for source in base:
  if source.get('scope')=='aosp-pinned-hidl-source-evidence':
   source['source_release_context']=dict(aosp_tag='android-17.0.0_r1',release_api_level_from_acquisition=source.get('api_level'),release_platform_version_from_acquisition=source.get('platform_api_version'),meaning='Acquisition release context, not introduced/removed or Android/device applicability evidence')
   source['api_level']=None;source['platform_api_version']=None;source['channel']='unknown';corrected+=1
 groups=[('source-inventory-release010.json',base)];provenance=[]
 for name in ['source-inventory-batchinterface-metadata-v1-acquired002.json','source-inventory-batchnative-registry-followup-v1-acquired001.json']:
  path=R/'outside-corpus-audit'/name;rows=json.loads(path.read_text())['sources']
  if name.startswith('source-inventory-batchnative-registry'):
   receipts=json.loads((R/'outside-corpus-audit/source-receipts-batchnative-registry-followup-v1-lowrate001.json').read_text())
   for source in rows:
    receipt=receipts[source['git_blob']]
    for field in ('sha256','size_bytes'):assert source[field]==receipt[field]
    source['retrieved_at']=receipt['retrieved_at'];source['acquisition_method']='Verified Git-object reuse across byte-identical pinned paths'
  groups.append((str(path.relative_to(R)),rows));provenance.append(dict(path=str(path.relative_to(R)),sha256=digest(path)))
 sources=merge_sources(groups)
 for ordinal,source in enumerate(sources,1):
  if source['status']!='downloaded':raise ValueError('Incomplete selected acquisition '+source['path'])
  if digest(R/source['local_path'])!=source['sha256']:raise ValueError('Source digest mismatch '+source['path'])
  if source.get('metadata_cache') and digest(R/source['metadata_cache']['path'])!=source['metadata_cache']['sha256']:raise ValueError('Cache digest mismatch '+source['path'])
  if ordinal%5000==0:print('source admission',ordinal,'/',len(sources),flush=True)
 document.update(sources=sources,frozen_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),complete=False);document['catalog']['registry_version']=11
 document['coverage_summary']='PARTIAL exact-version public-source corpus: full SDK37.2 source and lifecycle inventory; selected current AOSP repositories revision-reconciled1087/1087; declared AIDL/HIDL and finite native/hash/build cohorts acquired. HIDL/native/compiler/import/runtime gaps remain explicit; metadata is not authority.';document['catalog']['coverage']=document['coverage_summary'];document.setdefault('inventories',[]).extend(provenance);document['source_preparation']='freeze_release011.py';document['hidl_applicability_context_corrections']=corrected
 output.write_text(json.dumps(document,indent=2));summary=dict(at=document['frozen_at'],sources=len(sources),downloaded=len(sources),by_kind=dict(collections.Counter(s['kind'] for s in sources)),hidl_release_contexts_separated_from_android_applicability=corrected,inventory_sha256=digest(output),source_complete_for_declared_selection=True,global_semantic_completeness=False,interface_hash_facts='outside-corpus-audit/interface-hash-facts-v1/report.json',latest_source_revision_check='outside-corpus-audit/latest-release-source-closure-v1/project-pin-reconciliation.json')
 (R/'coverage-manifest-release011.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
if __name__=='__main__':main()
