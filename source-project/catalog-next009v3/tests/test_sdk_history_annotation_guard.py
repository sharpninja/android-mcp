import unittest,pathlib,sys,tempfile,json,gzip,hashlib,sqlite3,contextlib
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
import sdk_api_index,build_catalog,test_source_annotation_targets
class HistoricalAnnotationGuard(unittest.TestCase):
 def fixture(self,source_view):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory);helper=test_source_annotation_targets.SourceTargets();inv=helper.fixture(root);spec=json.loads(inv.read_text())
   raw=b'<api version="4"><class name="a/A" since="1" removed="36"><method name="go(Ljava/lang/String;)V"/></class></api>'
   records=sdk_api_index.parse(raw,target='37.2',preserve_history=True)['records'];cache=root/'index.gz'
   with gzip.open(cache,'wt') as stream:
    for row in records:stream.write(json.dumps(row)+'\n')
   helper.source(root,spec,'sdk-api-index','index.xml',raw,platform_api_version='37.2',sdk_api_index_view=source_view,metadata_cache=dict(path=cache.name,sha256=hashlib.sha256(cache.read_bytes()).hexdigest(),records=len(records)))
   helper.annotation(root,spec,platform_api_version='37.2');inv.write_text(json.dumps(spec));build_catalog.build(inv,root/'out')
   with contextlib.closing(sqlite3.connect(root/'out/catalog.sqlite')) as db:
    self.assertEqual(db.execute("SELECT count(*) FROM bindings WHERE signature LIKE 'jvm:%'").fetchone()[0],2)
    self.assertIsNone(db.execute('SELECT binding_id FROM annotation_evidence').fetchone()[0])
 def test_historical_target_retained_without_current_annotation_claim(self):self.fixture('all-declared')
 def test_cache_surface_mismatch_rejected(self):
  with self.assertRaisesRegex(ValueError,'index inventory view mismatch'):self.fixture('target-current')
