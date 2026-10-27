"""Verify payload/dependency equivalence; never relabel historical cache hashes."""
import pathlib,ast,hashlib,json,importlib.util,gzip,datetime,sys
R=pathlib.Path(__file__).resolve().parent;old=R/'catalog/tooling';new=R/'catalog-round004/tooling'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def function(path,name):return ast.dump(next(n for n in ast.parse(path.read_text()).body if isinstance(n,(ast.FunctionDef,ast.AsyncFunctionDef)) and n.name==name),include_attributes=False)
java_equal=sha(old/'java_source.py')==sha(new/'java_source.py');helpers={name:function(old/'parsers.py',name)==function(new/'parsers.py',name) for name in ('annotations','typ','sha')}
actual=hashlib.sha256((old/'java_source.py').read_bytes()+(old/'parsers.py').read_bytes()).hexdigest();report=json.loads((R/'sdk37.2/java-extraction-report.json').read_text())
if not java_equal or not all(helpers.values()) or actual!=report['parser_sha256']:raise ValueError('Java cache dependency equivalence not proven')
sys.path.insert(0,str(new));spec=importlib.util.spec_from_file_location('new_sdk_index',new/'sdk_api_index.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
parsed=module.parse((R/'sdk37.2/platform-metadata/android-37.2/data/api-versions.xml').read_bytes())['records'];count=0
with gzip.open(R/'sdk37.2/api-index-current.jsonl.gz','rt') as f:
 for count,(row,line) in enumerate(zip(parsed,f,strict=True),1):
  if row!=json.loads(line):raise ValueError('SDK API-index payload changed at record'+str(count))
proof=dict(checked_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),historical_java_parser_sha256=actual,candidate_java_source_sha256=sha(new/'java_source.py'),java_source_file_identical=java_equal,java_imported_helper_asts_identical=helpers,java_cache_files=report['java_files'],java_cache_declarations=report['declaration_records'],candidate_sdk_index_parser_sha256=sha(new/'sdk_api_index.py'),sdk_index_records_compared=count,sdk_index_payload_identical=True,historical_cache_metadata_rewritten=False,conclusion='Reuse original cache payloads with original provenance; record this equivalence proof and new producer processing fingerprint')
(R/'sdk37.2/round004-cache-reuse-proof.json').write_text(json.dumps(proof,indent=2));print(json.dumps(proof,indent=2))
