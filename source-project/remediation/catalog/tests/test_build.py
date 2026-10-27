import unittest,pathlib,tempfile,json,hashlib,sqlite3,sys,zipfile
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
import build_catalog as b
import jsonschema
P=pathlib.Path(__file__).resolve().parents[1]
class BuildTests(unittest.TestCase):
 def fixture(self,r):
  values=[('sdk-signature','api.txt','''// Signature format: 6.0
package android {
 public class Manifest.permission {
 field public static final String CAMERA="android.permission.CAMERA";
 }
}
package a {
 public class A {
 ctor public A();
 method @RequiresPermission(android.Manifest.permission.CAMERA) public void go(int);
 method public void go(String);
 }
}'''),('manifest','AndroidManifest.xml','<manifest xmlns:android="http://schemas.android.com/apk/res/android"><permission android:name="android.permission.CAMERA" android:protectionLevel="dangerous"/></manifest>'),('aidl','IFoo.aidl','package a; @RequiresNoPermission interface IFoo { @EnforcePermission("android.permission.CAMERA") void go(in int n)=4; }')]
  sources=[]
  for kind,path,text in values:
   data=text.encode();(r/path).write_bytes(data);sources.append(dict(kind=kind,path=path,local_path=path,status='downloaded',sha256=hashlib.sha256(data).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest(),size_bytes=len(data),url='https://android.googlesource.com/mock/'+path,revision='1'*40,retrieved_at='2026-10-26T00:00:00Z',license='fixture',scope='source',repository='fixture'))
  (r/'inventory.json').write_text(json.dumps(dict(sources=sources)));return r/'inventory.json'
 def test_import_integrity_schema_overlay_and_immutability(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);v=b.build(inv,r/'out');self.assertEqual(v['integrity_check'],'ok');self.assertEqual(v['foreign_key_check'],[]);self.assertEqual(v['counts']['permission_definitions'],1)
   schema=json.loads((P/'contracts/Entry.schema.json').read_text());validator=jsonschema.Draft202012Validator(schema)
   docs=[json.loads(l) for l in (r/'out/entries.jsonl').read_text().splitlines()]
   for doc in docs:validator.validate(doc)
   self.assertTrue(any(d['permission'].get('name')=='android.permission.CAMERA' for d in docs));self.assertEqual(json.loads((r/'out/catalog.json').read_text())['entries'],docs)
   db=sqlite3.connect(r/'out/catalog.sqlite');db.execute('PRAGMA foreign_keys=ON');bid=db.execute('SELECT entry_id FROM effective_entries LIMIT 1').fetchone()[0]
   with self.assertRaises(sqlite3.IntegrityError):db.execute('UPDATE bindings SET name="changed" WHERE binding_id=?',(bid,))
   count=db.execute('SELECT count(*) FROM runtime_catalog').fetchone()[0];now='2026-10-26T00:00:00Z'
   db.execute('INSERT INTO overlays VALUES (?,?,?,?,?,?,?,?,?,?)',('local-overlay',bid,'fixture',1,None,None,'suppressed','{}',now,now))
   self.assertEqual(db.execute('SELECT count(*) FROM runtime_catalog').fetchone()[0],count-1)
   db.execute('UPDATE overlays SET state="active",document_json=? WHERE entry_id="local-overlay"',(json.dumps({'fixture':'override'}),));self.assertEqual(json.loads(db.execute('SELECT document_json FROM runtime_catalog WHERE entry_id=?',(bid,)).fetchone()[0]),{'fixture':'override'})
   with self.assertRaises(sqlite3.IntegrityError):db.execute('INSERT INTO occurrences VALUES (?,?,?)',('missing','missing',1))
   db.close()
 def test_hash_failure_does_not_publish(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);(r/'api.txt').write_text('tampered')
   with self.assertRaisesRegex(ValueError,'SHA256 mismatch'):b.build(inv,r/'out')
   self.assertFalse((r/'out/catalog.sqlite').exists())
if __name__=='__main__':unittest.main()
