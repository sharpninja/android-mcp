import pathlib,sys,unittest,tempfile,json,sqlite3,hashlib,gzip
P=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(P/'tooling'))
import build_catalog,sdk_api_index,test_source_annotation_targets

class VersionedSignatureAnnotations(unittest.TestCase):
    def fixture(self,root,signature_version='37.0',annotation_version='37.0',different_revision=False,index_present=False,shadow=False,wrong_return=False,want_report=False):
        helper=test_source_annotation_targets.SourceTargets();inv=helper.fixture(root);spec=json.loads(inv.read_text());spec['sources'][0]['platform_api_version']=signature_version
        if shadow:
            source=spec['sources'][0];data=(root/source['local_path']).read_bytes().replace(b'public class A {',b'public class A<String> {');(root/source['local_path']).write_bytes(data);source.update(sha256=hashlib.sha256(data).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest(),size_bytes=len(data))
        if wrong_return:
            source=spec['sources'][0];data=(root/source['local_path']).read_bytes().replace(b'public void go(String)',b'public int go(String)');(root/source['local_path']).write_bytes(data);source.update(sha256=hashlib.sha256(data).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest(),size_bytes=len(data))
        helper.annotation(root,spec,platform_api_version=annotation_version)
        if different_revision:spec['sources'][-1]['revision']='2'*40
        if index_present:
            data=b'<api version="4"><class name="a/A"/></api>';records=sdk_api_index.parse(data,target=annotation_version)['records'];cache=root/'index.gz'
            with gzip.open(cache,'wt') as stream:
                for row in records:stream.write(json.dumps(row)+'\n')
            helper.source(root,spec,'sdk-api-index','index.xml',data,platform_api_version=annotation_version,metadata_cache=dict(path=cache.name,sha256=hashlib.sha256(cache.read_bytes()).hexdigest(),records=len(records)))
        inv.write_text(json.dumps(spec));build_catalog.build(inv,root/'out')
        with sqlite3.connect((root/'out/catalog.sqlite').as_uri()+'?mode=ro',uri=True) as db:
            result=db.execute('SELECT binding_id FROM annotation_evidence').fetchone()[0]
            report=json.loads(db.execute("SELECT p.document_json FROM parse_reports p JOIN sources s USING(source_id) WHERE s.kind='sdk-annotations'").fetchone()[0]) if want_report else None
        db.close();return (result,report) if want_report else result
    def test_archive_resolution_counts_are_distinct_from_parse_status(self):
        with tempfile.TemporaryDirectory() as td:
            binding,report=self.fixture(pathlib.Path(td),want_report=True)
            self.assertIsNotNone(binding);self.assertEqual(report['mapped_annotations'],report['annotations']);self.assertEqual(report['unmatched_annotations'],0);self.assertEqual(report['mapping_status'],'complete');self.assertEqual(report['platform_api_version'],'37.0')
    def test_matching_snapshot_and_version_resolves_legacy_signature(self):
        with tempfile.TemporaryDirectory() as td:self.assertEqual(self.fixture(pathlib.Path(td)),hashlib.sha256(b'java:a.A#go(String)').hexdigest())
    def test_different_snapshot_is_not_used_as_fallback(self):
        with tempfile.TemporaryDirectory() as td:self.assertIsNone(self.fixture(pathlib.Path(td),different_revision=True))
    def test_different_platform_is_not_used_as_fallback(self):
        with tempfile.TemporaryDirectory() as td:self.assertIsNone(self.fixture(pathlib.Path(td),annotation_version='37.2'))
    def test_existing_index_blocks_signature_backfill_for_absent_target(self):
        with tempfile.TemporaryDirectory() as td:self.assertIsNone(self.fixture(pathlib.Path(td),index_present=True))
    def test_same_snapshot_formal_type_variable_remains_ambiguous(self):
        with tempfile.TemporaryDirectory() as td:self.assertIsNone(self.fixture(pathlib.Path(td),shadow=True))
    def test_same_snapshot_return_type_conflict_stays_unresolved(self):
        with tempfile.TemporaryDirectory() as td:self.assertIsNone(self.fixture(pathlib.Path(td),wrong_return=True))

if __name__=='__main__':unittest.main()
