import unittest,pathlib,tempfile,json,hashlib,sqlite3,sys,zipfile,gzip
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
 def test_schema_failure_does_not_publish(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);spec=json.loads(inv.read_text());s=spec['sources'][0]
   data=(r/'api.txt').read_bytes().replace(b'@RequiresPermission(android.Manifest.permission.CAMERA)',b'@RequiresPermission(allOf={})');(r/'api.txt').write_bytes(data)
   s.update(sha256=hashlib.sha256(data).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest(),size_bytes=len(data));inv.write_text(json.dumps(spec))
   with self.assertRaisesRegex(ValueError,'schema rejected'):b.build(inv,r/'out')
   self.assertFalse((r/'out/catalog.sqlite').exists())
 def test_cached_java_and_current_index_import_with_distinct_evidence(self):
  import java_source,sdk_api_index
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);spec=json.loads(inv.read_text())
   for kind,name,data,parser in [('java-source','C.java',b'package a; public class C { @android.annotation.RequiresPermission("android.permission.CAMERA") public void f(int x){} }',lambda b:java_source.parse(b)['records']),('sdk-api-index','api.xml',b'<api version="4"><class name="a/C"><method name="f(I)V"/><field name="VALUE"/></class></api>',lambda b:sdk_api_index.parse(b)['records'])]:
    (r/name).write_bytes(data);cache=r/(name+'.gz');records=parser(data)
    with gzip.open(cache,'wt') as f:
     for row in records:f.write(json.dumps(row)+'\n')
    source=dict(spec['sources'][0],kind=kind,path=name,local_path=name,sha256=hashlib.sha256(data).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest(),size_bytes=len(data),url='https://dl.google.com/mock/'+name,metadata_cache=dict(path=cache.name,sha256=hashlib.sha256(cache.read_bytes()).hexdigest(),records=len(records)))
    spec['sources'].append(source)
   inv.write_text(json.dumps(spec));v=b.build(inv,r/'out')
   self.assertEqual(v['counts']['sources'],5)
   docs=[json.loads(l) for l in (r/'out/entries.jsonl').read_text().splitlines()]
   self.assertTrue(any(d['canonical_signature']=='jvm:a/C#f(I)V' for d in docs))
   java=next(d for d in docs if d['canonical_signature']=='java-source:a.C#f(int)');self.assertEqual(java['permission']['name'],'android.permission.CAMERA')
   jvm=next(d for d in docs if d['canonical_signature']=='jvm:a/C#f(I)V');self.assertEqual(jvm['permission']['name'],'android.permission.CAMERA')
   self.assertEqual(java['extensions']['proven_current_sdk_jvm_identity'],'a/C#f(I)V')
 def test_cache_hash_failure_does_not_publish(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);spec=json.loads(inv.read_text());s=spec['sources'][0];s['kind']='java-source';s['metadata_cache']=dict(path='bad.gz',sha256='0'*64,records=0);(r/'bad.gz').write_bytes(b'broken');inv.write_text(json.dumps(spec))
   with self.assertRaisesRegex(ValueError,'Metadata cache SHA256 mismatch'):b.build(inv,r/'out')
   self.assertFalse((r/'out/catalog.sqlite').exists())
 def test_malformed_aidl_is_preserved_as_unknown(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);spec=json.loads(inv.read_text());s=spec['sources'][2];data=b'package a; @Broken( interface IFoo {';(r/'IFoo.aidl').write_bytes(data)
   s.update(sha256=hashlib.sha256(data).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest(),size_bytes=len(data));inv.write_text(json.dumps(spec));b.build(inv,r/'out')
   db=sqlite3.connect(r/'out/catalog.sqlite');row=db.execute('SELECT raw_schema,parse_status FROM aidl_schemas').fetchone();self.assertEqual(row,(data.decode(),'unsupported'));self.assertGreater(db.execute('SELECT count(*) FROM issues').fetchone()[0],0);db.close()
 def test_package_and_type_identity_namespaces_are_distinct(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);spec=json.loads(inv.read_text());s=spec['sources'][0];data=(r/'api.txt').read_bytes()+b'\npackage a {\n public class B {\n }\n}\npackage a.B {\n public class C {\n }\n}\n';(r/'api.txt').write_bytes(data);s.update(sha256=hashlib.sha256(data).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest(),size_bytes=len(data));inv.write_text(json.dumps(spec));b.build(inv,r/'out')
   db=sqlite3.connect(r/'out/catalog.sqlite');rows=db.execute("SELECT signature,kind FROM bindings WHERE signature IN ('java:a.B','java:package:a.B') ORDER BY signature").fetchall();self.assertEqual(set(rows),{('java:a.B','class'),('java:package:a.B','package')});db.close()
 def test_absent_permission_evidence_remains_visible(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);spec=json.loads(inv.read_text());s=dict(spec['sources'][0]);data=b'package a {\n public class A {\n method public void go(int);\n }\n}\n';(r/'second.txt').write_bytes(data);s.update(path='second.txt',local_path='second.txt',url='https://android.googlesource.com/mock/second.txt',sha256=hashlib.sha256(data).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest(),size_bytes=len(data));spec['sources'].append(s);inv.write_text(json.dumps(spec));b.build(inv,r/'out')
   docs=[json.loads(l) for l in (r/'out/entries.jsonl').read_text().splitlines()];doc=next(d for d in docs if d['canonical_signature']=='java:a.A#go(int)');self.assertEqual(doc['permission']['op'],'permission')
   self.assertIn('unknown',{x['op'] for x in doc['extensions']['permission_variants']});self.assertTrue(any(v['metadata_status']=='absent' for v in doc['extensions']['permission_variant_occurrences']))
if __name__=='__main__':unittest.main()
