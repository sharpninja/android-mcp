"""Test focused patch against current source contracts/parsers, all writes here/tmp."""
import pathlib,sys,importlib.util,tempfile,json,hashlib,contextlib,io
R=pathlib.Path(__file__).resolve().parent;S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026/catalog')
sys.path.insert(0,str(S/'tooling'))
spec=importlib.util.spec_from_file_location('patched_publication_builder',R/'build_catalog.publication-gate-only.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);m.P=S
result={}
with tempfile.TemporaryDirectory() as td:
 root=pathlib.Path(td)
 for label,annotation in [('valid','@RequiresPermission("android.permission.CAMERA")'),('invalid','@RequiresPermission(allOf={})')]:
  target=root/label;target.mkdir();raw=('package a {\n public class A {\n method '+annotation+' public void x();\n }\n}').encode();(target/'api.txt').write_bytes(raw)
  source=dict(kind='sdk-signature',path='api.txt',local_path='api.txt',status='downloaded',sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw),url='https://android.googlesource.com/mock/api.txt',revision='1'*40,retrieved_at='2026-10-26T00:00:00Z',license='fixture',scope='source',repository='fixture')
  (target/'inventory.json').write_text(json.dumps(dict(sources=[source])))
  try:
   with contextlib.redirect_stdout(io.StringIO()):m.build(target/'inventory.json',target/'out')
   error=None
  except Exception as e:error=type(e).__name__+': '+str(e)
  published=(target/'out/catalog.sqlite').exists()
  assert published==(label=='valid')
  result[label]=dict(published=published,error=error)
(R/'publication-gate-test-results.json').write_text(json.dumps(dict(tests_passed=2,source_files_read_only=True,source_contract_storage_version='1.1.0',cases=result),indent=2));print(json.dumps(result,indent=2))
