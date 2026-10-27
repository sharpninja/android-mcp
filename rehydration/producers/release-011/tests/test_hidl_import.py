import unittest,pathlib,tempfile,sys,json,sqlite3,contextlib,base64
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
import build_catalog,test_build,hashlib,copy
class HidlImport(unittest.TestCase):
 def fixture(self,root,raw,**extras):
  inv=test_build.BuildTests().fixture(root);spec=json.loads(inv.read_text());(root/'IFoo.hal').write_bytes(raw);s=copy.deepcopy(spec['sources'][2]);s.update(kind='evidence',path='IFoo.hal',local_path='IFoo.hal',scope='aosp-pinned-hidl-source-evidence',api_level=None,channel='unknown',platform_api_version=None,sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw));s.update(extras);spec['sources'].append(s);inv.write_text(json.dumps(spec));return inv
 def test_structural_records_and_multi_results_survive_schema_gate(self):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory);inv=self.fixture(root,b'package hardware.test@2.7; interface IFoo { struct Point { int32_t x; }; get(Point[3]points) generates (bool ok, Point result); };');build_catalog.build(inv,root/'out')
   with contextlib.closing(sqlite3.connect(root/'out/catalog.sqlite')) as db:
    docs=[json.loads(r[0]) for r in db.execute("SELECT document_json FROM registry_entries WHERE json_extract(document_json,'$.extensions.language')='hidl'")]
    self.assertEqual(len(docs),5);method=next(d for d in docs if d['kind']=='method');self.assertEqual(len(method['extensions']['results']),2);self.assertEqual(method['permission']['op'],'unknown');self.assertEqual(method['codec_status'],'unknown')
    self.assertIn('hardware.test@2.7::IFoo.Point',[d['canonical_signature'].removeprefix('hidl:') for d in docs]);self.assertTrue(all('unknown_reason' in v['availability'] for d in docs for v in d['extensions']['device_resolution_variants']))
 def test_package_version_cannot_become_android_api_applicability(self):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory);inv=self.fixture(root,b'package hardware.test@2.7; interface IFoo {};',platform_api_version='37.0')
   with self.assertRaisesRegex(ValueError,'does not establish Android applicability'):build_catalog.build(inv,root/'out')
   self.assertFalse((root/'out/catalog.sqlite').exists())
 def test_invalid_utf8_retains_source_without_binding_inference(self):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory);raw=b'invalid\xffHIDL';inv=self.fixture(root,raw);build_catalog.build(inv,root/'out')
   with contextlib.closing(sqlite3.connect(root/'out/catalog.sqlite')) as db:
    value=db.execute("SELECT raw FROM issues WHERE category='hidl-source'").fetchone()[0];self.assertEqual(base64.b64decode(value),raw);self.assertEqual(db.execute("SELECT count(*) FROM registry_entries WHERE json_extract(document_json,'$.extensions.language')='hidl'").fetchone()[0],0)
