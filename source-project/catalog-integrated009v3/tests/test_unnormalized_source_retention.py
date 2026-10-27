import unittest,pathlib,sys,tempfile,json,sqlite3,base64,contextlib
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
import build_catalog,test_source_annotation_targets
class UnnormalizedSourceRetention(unittest.TestCase):
 def fixture(self,raw,scope):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory);helper=test_source_annotation_targets.SourceTargets();inv=helper.fixture(root);spec=json.loads(inv.read_text());helper.source(root,spec,'evidence','raw-schema',raw,scope=scope);inv.write_text(json.dumps(spec));build_catalog.build(inv,root/'out')
   with contextlib.closing(sqlite3.connect(root/'out/catalog.sqlite')) as db:
    rows=db.execute("SELECT i.raw,p.document_json FROM issues i JOIN parse_reports p USING(source_id) WHERE i.category='source-format-unknown'").fetchall()
   self.assertEqual(len(rows),1);text,report=rows[0];return text,json.loads(report)
 def test_hidl_raw_schema_is_in_runtime_database_without_fabricated_bindings(self):
  raw=b'package a@1.0; interface IFoo { f(); };'
  text,report=self.fixture(raw,'aosp-pinned-hidl-source-evidence')
  self.assertEqual(text.encode(),raw);self.assertEqual(report['raw_source_encoding'],'utf-8');self.assertEqual(report['declarations'],0)
 def test_non_utf8_native_evidence_is_lossless(self):
  raw=b'raw\xffbinder';text,report=self.fixture(raw,'aosp-native-binder-search-candidate-evidence')
  self.assertEqual(report['raw_source_encoding'],'base64');self.assertEqual(base64.b64decode(text),raw)
