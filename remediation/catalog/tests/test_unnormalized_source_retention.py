import unittest,pathlib,sys,tempfile,json,sqlite3,base64,contextlib
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
import build_catalog,test_build,copy,hashlib
class UnnormalizedSourceRetention(unittest.TestCase):
 def fixture(self,raw,scope):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory);inv=test_build.BuildTests().fixture(root);spec=json.loads(inv.read_text());(root/'raw-schema').write_bytes(raw);source=copy.deepcopy(spec['sources'][0]);source.update(kind='evidence',path='raw-schema',local_path='raw-schema',sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw),scope=scope,api_level=None,platform_api_version=None,source_release_context={'platform_api_version':'37.0'});spec['sources'].append(source);inv.write_text(json.dumps(spec));build_catalog.build(inv,root/'out')
   with contextlib.closing(sqlite3.connect(root/'out/catalog.sqlite')) as db:
    rows=db.execute("SELECT i.raw,p.document_json FROM issues i JOIN parse_reports p USING(source_id) WHERE i.category IN ('source-format-unknown','hidl-source')").fetchall()
   self.assertEqual(len(rows),1);text,report=rows[0];return text,json.loads(report)
 def test_hidl_source_retains_raw_bytes_and_declared_only_structure_without_wire_claims(self):
  raw=b'package a@1.0; interface IFoo { f(); };'
  text,report=self.fixture(raw,'aosp-pinned-hidl-source-evidence')
  self.assertEqual(text.encode(),raw);self.assertEqual(report['raw_source_encoding'],'utf-8');self.assertGreaterEqual(report['declarations'],2);self.assertFalse(report['normalizer_complete']);self.assertFalse(report['compiler_validation']);self.assertEqual(report['wire_resolution'],'unknown');self.assertEqual(report['android_availability'],'unknown')
 def test_non_utf8_native_evidence_is_lossless(self):
  raw=b'raw\xffbinder';text,report=self.fixture(raw,'aosp-native-binder-search-candidate-evidence')
  self.assertEqual(report['raw_source_encoding'],'base64');self.assertEqual(base64.b64decode(text),raw)
