"""Consume a pinned copy of the schema owner's artifacts; never edit its files."""
import pathlib,shutil,hashlib,json,datetime,ast
R=pathlib.Path(__file__).resolve().parent;owner=pathlib.Path('/home/sharpninja/github/2026-10-26/task-3/remediation/catalog');out=R/'catalog-integrated'
if out.exists():raise ValueError('Preserve integration candidate; do not overwrite')
out.mkdir();records=[]
for folder in ('tooling','contracts','migrations','tests'):
 for p in (owner/folder).rglob('*'):
  if not p.is_file() or '__pycache__' in p.parts:continue
  data=p.read_bytes();dest=out/p.relative_to(owner);dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data);records.append(dict(owner_path=str(p),local_path=str(dest.relative_to(R)),sha256=hashlib.sha256(data).hexdigest()))
for p in owner.iterdir():
 if p.is_file():shutil.copyfile(p,out/p.name)
for name in ('java_source.py','sdk_api_index.py','java_context.py','jvm_source_link.py','sdk_zip_metadata.py','restore_snapshot.py'):
 shutil.copyfile(R/'catalog/tooling'/name,out/'tooling'/name)
for name in ('test_java_source.py','test_java_cache.py','test_sdk_api_index.py','test_sdk_zip_metadata.py','test_jvm_source_link.py','test_java_context.py','test_restore_snapshot.py'):
 shutil.copyfile(R/'catalog/tests'/name,out/'tests'/name)
tree=ast.parse((R/'catalog/tests/test_build.py').read_text());cls=next(n for n in tree.body if isinstance(n,ast.ClassDef))
selected={'test_cached_java_and_current_index_import_with_distinct_evidence','test_cache_hash_failure_does_not_publish','test_malformed_aidl_is_preserved_as_unknown'}
methods=[n for n in cls.body if isinstance(n,ast.FunctionDef) and n.name in selected]
source='''import unittest,pathlib,tempfile,json,hashlib,sqlite3,sys,zipfile,gzip
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
import build_catalog as b
from test_build import BuildTests as BaseBuildTests
'''+ast.unparse(ast.fix_missing_locations(ast.Module(body=[ast.ClassDef(name='SourceIntegrationTests',bases=[ast.Name(id='BaseBuildTests',ctx=ast.Load())],keywords=[],body=methods,decorator_list=[])],type_ignores=[])))+'\n'
(out/'tests/test_source_integration.py').write_text(source)
for item in records:
 if hashlib.sha256(pathlib.Path(item['owner_path']).read_bytes()).hexdigest()!=item['sha256']:raise ValueError('Owner changed during snapshot; retain candidate and refresh explicitly')
(R/'catalog-integration-owner-snapshot.json').write_text(json.dumps(dict(created_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),owner_files_modified=False,source_active_catalog_preserved='catalog/',candidate='catalog-integrated/',files=records),indent=2));print('Pinned owner snapshot and source fixtures prepared',len(records),'files')
