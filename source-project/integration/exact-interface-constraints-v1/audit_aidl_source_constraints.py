import pathlib,json,sys,collections,datetime,hashlib
R=pathlib.Path(__file__).resolve().parent
sys.path.insert(0,str(R/'catalog-integrated012-sourcev1/tooling'))
from parsers import aidl_all
from aidl_source_constraint import exact_constraint
from interface_module_linkage import dump_directory
D=R/'integration/aidl-source-constraints-v1';D.mkdir(exist_ok=False)
sources={(s['repository'],s['revision'],s['path']):s for s in json.loads((R/'source-inventory-release011.json').read_text())['sources']}
audits={}
for line in (R/'integration/aidl-frozen-hashes-v1/hashes.jsonl').read_text().splitlines():
 v=json.loads(line);audits[(v['repository'],v['revision'],v['dump_directory'])]=v
modules=collections.defaultdict(list)
for line in (R/'outside-corpus-audit/interface-module-facts-v1/facts.jsonl').read_text().splitlines():
 v=json.loads(line);s=v['source'];modules[(s['repository'],s['revision'],s['path'],v['literal_properties']['name'])].append(v)
counts=collections.Counter();unknown=collections.Counter()
with (R/'outside-corpus-audit/interface-module-linkage-v2/source-links.jsonl').open() as lines,(D/'constraints.jsonl').open('w') as out:
 for line in lines:
  link=json.loads(line);s=link['source'];directory=dump_directory(s['path']);audit=audits.get((s['repository'],s['revision'],directory))
  if audit is None:continue
  counts['source_files_in_verified_hash_directories']+=1;source=sources[(s['repository'],s['revision'],s['path'])];raw=(R/source['local_path']).read_bytes()
  if hashlib.sha256(raw).hexdigest()!=s['sha256']:raise ValueError('Source bytes changed')
  try:declarations=aidl_all(raw.decode('utf-8'))
  except ValueError as error:counts['parse_exceptions']+=1;unknown[str(error)]+=1;continue
  b=link['linkage'].get('build_evidence',{});candidates=modules.get((b.get('repository'),b.get('revision'),b.get('path'),link['linkage'].get('build_module_name')),[]);module=candidates[0] if len(candidates)==1 else {}
  hash_source=sources[(s['repository'],s['revision'],audit['hash_source']['path'])];hash_text=(R/hash_source['local_path']).read_text()
  for declaration in declarations:
   if declaration.get('declaration_kind')!='interface':continue
   counts['interface_declarations']+=1;result=exact_constraint(s,declaration,audit,hash_text,module,link['linkage']);counts['exact_interface_constraints']+='clause' in result
   if 'unknown_reason' in result:unknown[result['unknown_reason']]+=1
   out.write(json.dumps(dict(source=s,descriptor=declaration.get('descriptor'),constraint=result),sort_keys=True)+'\n')
report=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),counts=dict(counts),unknown_reasons=dict(unknown),tests=14,producer_sha256=hashlib.sha256((R/'aidl_source_constraint.py').read_bytes()).hexdigest(),downloaded_compiler_code_executed=False,runtime_observation=False,builder_integration='Map source identities to exact imported source IDs; retain source constraint in Entry and hash it. Only a trusted runtime interface hash/reachability match can satisfy it; type dependencies, version conflicts, codecs and grants remain separate. Do not use frozen version as Android or APEX version.',active_release011_unchanged=True)
(D/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
