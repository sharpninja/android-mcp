"""Normalize every already-acquired ABI JSON dump; no binary/source builds."""
import pathlib,json,sqlite3,hashlib,collections,datetime
from native_abi_facts import parse
R=pathlib.Path(__file__).resolve().parent;D=R/'integration/native-abi-source-facts-v1';D.mkdir(exist_ok=False)
db=sqlite3.connect((R/'catalog/output/release-011/catalog.sqlite').as_uri()+'?mode=ro',uri=True)
registry=db.execute('SELECT registry_id FROM active_registry WHERE singleton=1').fetchone()[0]
sources=[json.loads(raw) for raw, in db.execute("SELECT document_json FROM source_details WHERE json_extract(document_json,'$.path') LIKE '%.lsdump'")];db.close();counts=collections.Counter();source_reports=[]
with (D/'records.jsonl').open('w') as records,(D/'callback-parameter-candidates.jsonl').open('w') as callbacks,(D/'type-references.jsonl').open('w') as references:
 for source in sources:
  raw=(R/source['local_path']).read_bytes()
  if hashlib.sha256(raw).hexdigest()!=source['sha256']:raise ValueError('Acquired ABI dump changed')
  identity={k:source[k] for k in ('source_id','repository','revision','path','sha256')};result=parse(raw.decode('utf-8'));counts['source_paths']+=1;counts[result['status']]+=1
  if result['status']!='parsed-source-abi-json':source_reports.append(dict(source=identity,status=result['status'],reason=result['reason']));continue
  for record in result['records']:
   counts['records']+=1;counts[record['collection']]+=1
   records.write(json.dumps(dict(source=identity,source_registry_id=registry,evidence_record_id=hashlib.sha256((source['source_id']+':'+record['collection']+':'+str(record['local_key'])+':'+str(record['ordinal'])).encode()).hexdigest(),identity_scope='source-file-local ABI evidence, not a stable MCP binding ID',record=record,runtime_applicability='unknown',wire_codec_proven=False,permission_authority='unknown'),sort_keys=True)+'\n')
  for candidate in result['callback_parameter_candidates']:counts['callback_parameter_candidates']+=1;callbacks.write(json.dumps(dict(source=identity,source_registry_id=registry,candidate=candidate),sort_keys=True)+'\n')
  for ref in result['references']:references.write(json.dumps(dict(source_id=source['source_id'],**ref),sort_keys=True)+'\n')
  for key,value in result['reference_counts'].items():counts['type_reference:'+key]+=value
  source_reports.append(dict(source=identity,status=result['status'],records=len(result['records']),duplicate_type_keys=result['duplicate_type_keys'],unknown_root_fields=result['unknown_root_fields'],references=result['reference_counts'],callback_candidates=len(result['callback_parameter_candidates'])))
report=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),source_registry_id=registry,counts=dict(counts),tests=7,producer_sha256=hashlib.sha256((R/'native_abi_facts.py').read_bytes()).hexdigest(),reports=source_reports,complete=False,limitations=['Version/architecture/library labels remain exact source-path context; no Android introduced/removed interval is inferred','ABI source records are not Binder transaction layouts, verified codecs, shipping service observations, OS authority or client grants','Function-pointer parameters are structural callback candidates; registration, direction, threading, lifecycle and streaming remain unknown','Local type references stay source-file scoped; old/prebuilt/ABI-dump snapshots are not silently merged or promoted to current runtime'],files=[])
for p in sorted(D.glob('*.jsonl')):report['files'].append(dict(path=p.name,bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(D/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k!='reports'},indent=2))
