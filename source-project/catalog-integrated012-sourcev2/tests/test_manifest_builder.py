import pathlib,sys,tempfile,json,hashlib,sqlite3,base64,unittest
P=pathlib.Path(__file__).resolve().parents[1];sys.path[:0]=[str(P/'tooling'),str(P/'tests')]
import build_catalog,test_build
class ManifestBuilder(unittest.TestCase):
 def run_manifest(self,root,payload):
  inventory=test_build.BuildTests().fixture(root);spec=json.loads(inventory.read_text());s=next(v for v in spec['sources'] if v['kind']=='manifest');(root/s['local_path']).write_bytes(payload);s.update(size_bytes=len(payload),sha256=hashlib.sha256(payload).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(payload)).encode()+b'\0'+payload).hexdigest());inventory.write_text(json.dumps(spec));result=build_catalog.build(inventory,root/'out');db=sqlite3.connect(root/'out/catalog.sqlite');self.addCleanup(db.close);return result,db
 def test_late_declaration_permissions_published_with_normalization_receipt(self):
  payload=b'<!-- license -->\n<?xml version="1.0" encoding="utf-8"?><manifest xmlns:android="http://schemas.android.com/apk/res/android"><permission android:name="android.permission.CAMERA"/></manifest>'
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);result,db=self.run_manifest(root,payload);self.assertEqual(result['counts']['permission_definitions'],1);report=next(v for v in result['source_reports'] if v['kind']=='manifest');self.assertEqual(report['normalization']['kind'],'late-utf8-xml-declaration-masked-after-comments');db.close()
 def test_empty_fixture_retained_as_empty_not_as_valid_manifest(self):
  with tempfile.TemporaryDirectory() as td:
   result,db=self.run_manifest(pathlib.Path(td),b'');report=next(v for v in result['source_reports'] if v['kind']=='manifest');self.assertEqual(report['status'],'empty-source-no-declarations');self.assertFalse(report['valid_android_manifest']);db.close()
 def test_unsupported_nonutf8_source_is_preserved_losslessly(self):
  payload=b'\x03\x00\x08\x00\xff\x00'
  with tempfile.TemporaryDirectory() as td:
   result,db=self.run_manifest(pathlib.Path(td),payload);report=next(v for v in result['source_reports'] if v['kind']=='manifest');self.assertEqual(report['raw_source_encoding'],'base64');self.assertEqual(base64.b64decode(db.execute("SELECT raw FROM issues WHERE reason LIKE 'Manifest XML unresolved:%'").fetchone()[0]),payload);db.close()
