"""Declared AIDL/HIDL build and dump linkage, with no runtime/version inference."""
import pathlib,re,json,datetime,collections,hashlib
def dump_directory(path):
 match=re.search(r'(?:^|/)aidl_api/[^/]+/[^/]+',path)
 return path[:match.end()] if match else None
def declared_versions(module):
 properties=module['literal_properties'];result=[]
 if isinstance(properties.get('versions'),list):
  result += [dict(version=value,imports=None,evidence='versions property') for value in properties['versions'] if isinstance(value,str)]
 if isinstance(properties.get('versions_with_info'),list):
  result += [dict(version=value['version'],imports=value.get('imports'),evidence='versions_with_info property') for value in properties['versions_with_info'] if isinstance(value,dict) and isinstance(value.get('version'),str)]
 return result
def link(source,modules):
 source_path=pathlib.PurePosixPath(source['path']);candidates=[];dump=re.search(r'(?:^|/)aidl_api/([^/]+)/([^/]+)/',source['path']);version=dump.group(2) if dump else None
 for module in modules:
  evidence=module['source'];properties=module['literal_properties']
  if any(source[key]!=evidence[key] for key in ('repository','revision')):continue
  if source['path'] in module.get('matched_declared_paths',[]):candidates.append(module);continue
  if dump and module['kind']=='aidl_interface' and properties.get('name')==dump.group(1):
   expected=pathlib.PurePosixPath(evidence['path']).parent/'aidl_api'/dump.group(1)/version
   if source_path.is_relative_to(expected):candidates.append(module)
 if len(candidates)!=1:return dict(status='ambiguous-module-source-link' if candidates else 'no-proven-module-link',candidates=len(candidates),frozen_dump_version=version,compiled_or_device_version_proven=False)
 module=candidates[0];versions=declared_versions(module)
 if not dump:status='literal-current-source-linked'
 elif version=='current':status='development-dump-linked-version-unknown'
 elif any(v['version']==version for v in versions):status='frozen-dump-declared-version-linked'
 else:status='dump-path-linked-module-version-unconfirmed'
 return dict(status=status,module_kind=module['kind'],build_module_name=module['literal_properties'].get('name'),build_evidence=module['source'],frozen_dump_version=version,declared_versions=versions,declared_flags={k:module['literal_properties'].get(k) for k in ('frozen','unstable','stability','apex_available','vendor_available')},compiled_or_device_version_proven=False,android_applicability_proven=False)
def main(root):
 root=pathlib.Path(root);modules=[json.loads(line) for line in (root/'outside-corpus-audit/interface-module-facts-v1/facts.jsonl').read_text().splitlines()];sources=json.loads((root/'source-inventory-release011.json').read_text())['sources'];by_repo=collections.defaultdict(list)
 for module in modules:by_repo[(module['source']['repository'],module['source']['revision'])].append(module)
 out=root/'outside-corpus-audit/interface-module-linkage-v2';out.mkdir(exist_ok=False);counts=collections.Counter();hashes=collections.defaultdict(list)
 for line in (root/'outside-corpus-audit/interface-hash-facts-v1/facts.jsonl').read_text().splitlines():
  item=json.loads(line)
  if item.get('format')=='published-aidl-hash-hex40':hashes[(item['source']['repository'],item['source']['revision'],str(pathlib.PurePosixPath(item['source']['path']).parent))].append(item)
 with (out/'source-links.jsonl').open('w') as stream:
  for source in sources:
   if source['kind']!='aidl' and not source['path'].endswith('.hal'):continue
   result=link(source,by_repo[(source['repository'],source['revision'])]);counts[source['kind']+':'+result['status']]+=1
   if result.get('frozen_dump_version'):
    directory=dump_directory(source['path']);result['published_hash_lines']=hashes.get((source['repository'],source['revision'],directory),[])
   stream.write(json.dumps(dict(source={k:source[k] for k in ('repository','revision','path','sha256')},linkage=result),sort_keys=True)+'\n')
 report=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),source_records=sum(counts.values()),counts=dict(counts),code_sha256=hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest(),runtime_access_inferred=False,complete=False,limitations=['Declared module identity is a Soong build-domain name, not observed installed Android/APEX package identity','Frozen dump path/version/import/hash linkage is source evidence; current/working version is never guessed','Unlinked/ambiguous modules and source selectors remain preserved'])
 (out/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
if __name__=='__main__':main(pathlib.Path(__file__).resolve().parent)
