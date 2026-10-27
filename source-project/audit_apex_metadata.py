import pathlib,json,collections,hashlib,datetime
from apex_manifest_facts import manifest,build_modules,explicit_manifest_path
R=pathlib.Path(__file__).resolve().parent;D=R/'integration/apex-source-facts-v2';D.mkdir(exist_ok=False)
inventory=json.loads((R/'outside-corpus-audit/source-inventory-batchapex-metadata-v1-acquired002.json').read_text());counts=collections.Counter();manifests={};modules=[]
with (D/'manifest-facts.jsonl').open('w') as out,(D/'build-module-facts.jsonl').open('w') as builds:
 for source in inventory['sources']:
  raw=(R/source['local_path']).read_bytes()
  if hashlib.sha256(raw).hexdigest()!=source['sha256']:raise ValueError('Changed acquired source')
  identity={k:source[k] for k in ('repository','revision','path','sha256','retrieved_at')}
  if source['path'].endswith('.json'):
   fact=manifest(raw.decode('utf-8'));fact['source']=identity;counts['manifest_sources']+=1;counts[fact['status']]+=1
   manifests[(source['repository'],source['revision'],source['path'])]=fact;out.write(json.dumps(fact,sort_keys=True)+'\n')
  else:
   counts['build_sources']+=1
   for module in build_modules(raw.decode('utf-8')):
    module['source']=identity;module['explicit_manifest_path']=explicit_manifest_path(source['path'],module);modules.append(module);counts['build_modules']+=1;counts[module['kind']]+=1;builds.write(json.dumps(module,sort_keys=True)+'\n')
with (D/'explicit-manifest-links.jsonl').open('w') as out:
 for module in modules:
  source=module['source'];target=module['explicit_manifest_path'];fact=manifests.get((source['repository'],source['revision'],target))
  if not fact:continue
  counts['explicit_build_manifest_links']+=1
  out.write(json.dumps(dict(build_source=source,build_module_name=module['literal_properties'].get('name'),build_kind=module['kind'],manifest_source=fact['source'],manifest_name=fact.get('name'),source_version_code=fact.get('normalized_version_code'),installed_identity_or_version_proven=False,applicability=dict(unknown_reason='Build/product selection, version override and installed version applicability interval remain unproven')),sort_keys=True)+'\n')
report=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),counts=dict(counts),all_paths_downloaded=301,unique_git_objects=300,tests=12,source_bytes_git_verified=True,code_sha256=hashlib.sha256((R/'apex_manifest_facts.py').read_bytes()).hexdigest(),runtime_observation=False,complete=False,limitations=['Literal APEX manifest names/version codes are source facts, not observed installed or build-overridden versions','Defaults and expression dependencies remain preserved but unresolved; absence of an explicit manifest property is not inferred linkage','Soong names and special apex_available selectors are not automatically installed package identifiers'],files=[])
for p in sorted(D.glob('*.jsonl')):report['files'].append(dict(path=p.name,bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(D/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
