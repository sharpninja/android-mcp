import unittest,tempfile,pathlib,sys,subprocess,os,shutil,importlib.util,hashlib,gzip,json
P=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(P/'tooling'))
STAGE=P.parent/'stage_sdk372_sources_contract2.py'
if not STAGE.exists():STAGE=P.parent/'stage_sdk372_sources.py'
class StageImportPurity(unittest.TestCase):
 def test_import_in_frozen_layout_creates_no_data_directories(self):
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);script=root/'stage.py';shutil.copyfile(STAGE,script)
   code='import importlib.util,sys; s=importlib.util.spec_from_file_location("isolated_stage",sys.argv[1]); m=importlib.util.module_from_spec(s); s.loader.exec_module(m)'
   before={str(p.relative_to(root)) for p in root.rglob('*')}
   run=subprocess.run([sys.executable,'-B','-c',code,str(script)],env=dict(os.environ,PYTHONPATH=str(P/'tooling'),PYTHONDONTWRITEBYTECODE='1'),capture_output=True,text=True)
   self.assertEqual(run.returncode,0,run.stderr);self.assertEqual({str(p.relative_to(root)) for p in root.rglob('*')},before)
 def test_runtime_paths_are_injected_and_small_extraction_is_atomic(self):
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);script=root/'stage.py';shutil.copyfile(STAGE,script);spec=importlib.util.spec_from_file_location('isolated_stage',script);stage=importlib.util.module_from_spec(spec);spec.loader.exec_module(stage)
   data_root=root/'data';stage.configure(data_root,'nested/cache')
   from cache_fingerprint import current
   self.assertEqual(stage.PARSER_SHA,current('java-source'))
   data=b'\n'*300+b'package a;\npublic class C {\n public int x;\n}\n';oid=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest();(stage.B/oid).write_bytes(data)
   source=dict(path='a/C.java',git_blob=oid,local_path=str((stage.B/oid).relative_to(data_root)));_,stats=stage.work(source);cache=data_root/stats['cache_path'];self.assertTrue(stage.verify_cache(cache,stats));self.assertFalse(list(cache.parent.glob('*.tmp')))
   with gzip.open(cache,'rt') as f:rows=[json.loads(line) for line in f]
   self.assertEqual(next(r for r in rows if r['kind']=='field')['line'],303)
