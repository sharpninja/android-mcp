import pathlib,json,sqlite3,hashlib,collections,datetime
from native_descriptor_facts import parse
R=pathlib.Path(__file__).resolve().parent;D=R/'integration/native-descriptor-source-facts-v1';D.mkdir(exist_ok=False)
db=sqlite3.connect((R/'catalog/output/release-011/catalog.sqlite').as_uri()+'?mode=ro',uri=True)
registry=db.execute('SELECT registry_id FROM active_registry WHERE singleton=1').fetchone()[0]
sources=[json.loads(raw) for raw, in db.execute("SELECT document_json FROM source_details WHERE json_extract(document_json,'$.scope') LIKE 'aosp-native%'")];db.close();counts=collections.Counter();reports=[];not_parsed=[]
with (D/'facts.jsonl').open('w') as out:
 for source in sources:
  suffix=pathlib.PurePosixPath(source['path']).suffix
  if suffix not in ('.cpp','.cc','.h','.hpp','.c','.rs'):
   not_parsed.append(dict(source={k:source[k] for k in ('source_id','repository','revision','path','sha256')},reason='Different source format; ABI JSON has separate normalizer, other formats remain retained'));continue
  counts['source_paths']+=1;payload=(R/source['local_path']).read_bytes()
  if hashlib.sha256(payload).hexdigest()!=source['sha256']:raise ValueError('Source changed')
  result=parse(payload.decode('utf-8'),'rust' if suffix=='.rs' else 'cpp');counts['lexical_issues']+=len(result['lexical_issues']);counts['facts']+=len(result['facts']);identity={k:source[k] for k in ('source_id','repository','revision','path','sha256')}
  for fact in result['facts']:
   counts['family:'+fact['family']]+=1;counts['status:'+fact['status']]+=1
   if fact.get('descriptor_literal') is not None:counts['literal_descriptor_candidates']+=1
   out.write(json.dumps(dict(source=identity,source_registry_id=registry,fact=fact),sort_keys=True)+'\n')
  reports.append(dict(source=identity,facts=len(result['facts']),lexical_issues=result['lexical_issues']))
report=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),source_registry_id=registry,counts=dict(counts),tests=8,producer_sha256=hashlib.sha256((R/'native_descriptor_facts.py').read_bytes()).hexdigest(),reports=reports,different_formats_retained=not_parsed,complete=False,limitations=['Known five-family token forms only; not a C++/Rust compiler, preprocessor, overload/ABI normalizer or global source completeness proof','Literal arguments are candidates from source; function declarations, tests, conditional branches and generated/versioned copies are not shipping endpoint observations','Computed arguments, malformed forms and lexical issues remain explicit; no transaction code or codec inferred'],payload_sha256=hashlib.sha256((D/'facts.jsonl').read_bytes()).hexdigest())
(D/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k not in ('reports','different_formats_retained')},indent=2))
