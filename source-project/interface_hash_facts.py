"""Syntactic published-interface hash facts, separate from catalog semantic hashes.
No source hash is promoted to an observed runtime hash or Android applicability.
"""
import re,pathlib,json,hashlib,datetime,collections
def aidl_hashes(text):
 facts=[];unknown=[]
 for line,raw in enumerate(text.splitlines(),1):
  value=raw.split('#',1)[0].strip()
  if not value:continue
  if re.fullmatch(r'[0-9a-fA-F]{40}',value):facts.append(dict(line=line,literal=value,format='published-aidl-hash-hex40',raw=raw))
  else:unknown.append(dict(line=line,raw=raw))
 return dict(facts=facts,unknown_lines=unknown,compiled_runtime_hash_proven=False,android_applicability_proven=False,semantic_note='All literal lines retained; selecting a generated interface hash requires compiler/build/version linkage')
def hidl_hashes(text):
 facts=[];unknown=[]
 for line,raw in enumerate(text.splitlines(),1):
  value=raw.split('#',1)[0].strip()
  if not value:continue
  match=re.fullmatch(r'([0-9a-fA-F]{64})\s+([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*@\d+\.\d+::[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)',value)
  if match:facts.append(dict(line=line,literal=match.group(1),declared_identifier=match.group(2),format='published-hidl-hash-hex64-and-identifier',raw=raw))
  else:unknown.append(dict(line=line,raw=raw))
 return dict(facts=facts,unknown_lines=unknown,compiled_runtime_hash_proven=False,android_applicability_proven=False,semantic_note='Repeated identifiers and compatibility hash lines preserved; HIDL package versions are not Android API versions')
def main(root):
 root=pathlib.Path(root);sources=json.loads((root/'outside-corpus-audit/source-inventory-batchinterface-metadata-v1-acquired002.json').read_text())['sources'];reports=[];counts=collections.Counter();facts=[]
 for source in sources:
  reasons=source['selection_reasons'];format='aidl' if 'stable AIDL version hash' in reasons else 'hidl' if 'HIDL ancestor hash-index candidate' in reasons else None
  if format is None:continue
  raw=(root/source['local_path']).read_bytes();assert hashlib.sha256(raw).hexdigest()==source['sha256'];parsed=(aidl_hashes if format=='aidl' else hidl_hashes)(raw.decode('utf-8'));identity={k:source[k] for k in ('repository','revision','path','sha256','retrieved_at')}
  if format=='aidl':
   match=re.search(r'(?:^|/)aidl_api/([^/]+)/([^/]+)/\.hash$',source['path']);scope=dict(interface_module_name_from_path=match.group(1),frozen_dump_version_from_path=match.group(2)) if match else dict(path_scope='unknown')
  else:scope=dict(path_scope='HIDL ancestor hash index; individual identifiers retain package versions')
  for item in parsed['facts']:facts.append(dict(source=identity,scope=scope,**item));counts[format+':hash-facts']+=1
  reports.append(dict(source=identity,scope=scope,format=format,facts=len(parsed['facts']),unknown_lines=parsed['unknown_lines'],compiled_runtime_hash_proven=False));counts[format+':files']+=1;counts[format+':unknown-lines']+=len(parsed['unknown_lines'])
 destination=root/'outside-corpus-audit/interface-hash-facts-v1';destination.mkdir(exist_ok=False)
 with (destination/'facts.jsonl').open('w') as out:
  for item in facts:out.write(json.dumps(item,sort_keys=True)+'\n')
 report=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),counts=dict(counts),files=reports,code_sha256=hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest(),runtime_observations=False,metadata_cannot_grant_os_or_client_authority=True,complete=False)
 (destination/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k!='files'},indent=2))
if __name__=='__main__':main(pathlib.Path(__file__).resolve().parent)
