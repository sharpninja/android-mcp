import unittest,pathlib,tempfile,json,hashlib,sqlite3,sys,zipfile
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
import build_catalog as b
import jsonschema
import validate_catalog
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
   data=text.encode();(r/path).write_bytes(data);sources.append(dict(kind=kind,path=path,local_path=path,status='downloaded',sha256=hashlib.sha256(data).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest(),size_bytes=len(data),url='https://android.googlesource.com/mock/'+path,revision='1'*40,retrieved_at='2026-10-26T00:00:00Z',license='fixture',scope='source',repository='fixture',api_level=37,channel='stable'))
  (r/'inventory.json').write_text(json.dumps(dict(catalog=dict(registry_version=1,android_version='fixture',api_level=37,coverage='fixture partial'),sources=sources)));return r/'inventory.json'
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
   db.execute('INSERT INTO overlays VALUES (?,?,?,?,?,?,?,?,?,?)',(bid,bid,db.execute('SELECT signature FROM bindings WHERE binding_id=?',(bid,)).fetchone()[0],1,None,None,'suppressed','{}',now,now))
   self.assertEqual(db.execute('SELECT count(*) FROM runtime_catalog').fetchone()[0],count-1)
   db.execute('UPDATE overlays SET state="active",revision=2,document_json=? WHERE entry_id=?',(json.dumps({'fixture':'override'}),bid));self.assertEqual(json.loads(db.execute('SELECT document_json FROM runtime_catalog WHERE entry_id=?',(bid,)).fetchone()[0]),{'fixture':'override'})
   with self.assertRaises(sqlite3.IntegrityError):db.execute('INSERT INTO occurrences VALUES (?,?,?)',('missing','missing',1))
   db.close()
   before={p.name:p.read_bytes() for p in (r/'out').iterdir() if p.is_file()}
   validate_catalog.run(r/'out',inv,r/'validation-report.json')
   self.assertEqual(before,{p.name:p.read_bytes() for p in (r/'out').iterdir() if p.is_file()})
 def test_hash_failure_does_not_publish(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);(r/'api.txt').write_text('tampered')
   with self.assertRaisesRegex(ValueError,'SHA256 mismatch'):b.build(inv,r/'out')
   self.assertFalse((r/'out/catalog.sqlite').exists())
   self.assertFalse(json.loads((r/'out/validation-failure.json').read_text())['published'])
 def test_one_identity_two_immutable_occurrences(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);data=json.loads(inv.read_text());copy=dict(data['sources'][0]);copy.update(path='second.txt',local_path='second.txt',url='https://android.googlesource.com/mock/second.txt');(r/'second.txt').write_bytes((r/'api.txt').read_bytes());data['sources'].append(copy);inv.write_text(json.dumps(data));b.build(inv,r/'out')
   db=sqlite3.connect(r/'out/catalog.sqlite');self.addCleanup(db.close);rows=db.execute("SELECT b.binding_id,count(*) FROM bindings b JOIN occurrences o USING(binding_id) WHERE b.signature='java:a.A#go(int)' GROUP BY b.binding_id").fetchall();self.assertEqual(len(rows),1);self.assertEqual(rows[0][1],2)
 def test_invalid_ast_cannot_publish(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);data=json.loads(inv.read_text());s=data['sources'][0];raw=(r/'api.txt').read_text().replace('@RequiresPermission(android.Manifest.permission.CAMERA)','@RequiresPermission(allOf={})').encode();(r/'api.txt').write_bytes(raw);s.update(sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw));inv.write_text(json.dumps(data))
   with self.assertRaises(ValueError):b.build(inv,r/'out')
   self.assertFalse((r/'out/catalog.sqlite').exists())
if __name__=='__main__':unittest.main()
