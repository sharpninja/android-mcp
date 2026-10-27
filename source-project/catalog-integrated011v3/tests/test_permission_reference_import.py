import unittest,pathlib,sys,tempfile,json,sqlite3,contextlib
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
import build_catalog,test_build,copy,hashlib
class PermissionReferenceImport(unittest.TestCase):
 def test_documented_constants_are_symbols_not_manifest_definitions(self):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory);inv=test_build.BuildTests().fixture(root);spec=json.loads(inv.read_text())
   raw=b'<div data-version-added="37.2"><h3 class="api-name" id="DOC_ONLY">DOC_ONLY</h3><pre class="api-signature">public static final String DOC_ONLY</pre><p>Protection level: internal|role</p><p>Constant Value: "android.permission.DOC_ONLY"</p></div>'
   (root/'reference.html').write_bytes(raw);source=copy.deepcopy(spec['sources'][0]);source.update(kind='evidence',path='reference.html',local_path='reference.html',sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw),scope='official-sdk-permission-reference',api_level=None,platform_api_version=None,channel='unknown');spec['sources'].append(source);inv.write_text(json.dumps(spec));build_catalog.build(inv,root/'out')
   with contextlib.closing(sqlite3.connect(root/'out/catalog.sqlite')) as db:
    self.assertEqual(db.execute("SELECT value FROM permission_symbols WHERE symbol='android.Manifest.permission.DOC_ONLY'").fetchone(),('android.permission.DOC_ONLY',))
    self.assertEqual(db.execute("SELECT count(*) FROM permission_definitions WHERE name='android.permission.DOC_ONLY'").fetchone()[0],0)
    report=json.loads(db.execute("SELECT p.document_json FROM parse_reports p JOIN sources s USING(source_id) WHERE s.path='reference.html'").fetchone()[0])
    self.assertEqual(report['documented_fields'][0]['protection_level_raw'],'internal|role')
    self.assertEqual(report['declarations'],0)
