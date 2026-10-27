import unittest,pathlib,tempfile,hashlib,json,gzip,zipfile,sqlite3,sys
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
import build_catalog as b,sdk_api_index
import test_build as base_module
class SourceTargets(unittest.TestCase):
 fixture=base_module.BuildTests.fixture
 def source(self,r,spec,kind,name,data,**extra):
  (r/name).write_bytes(data);s=dict(spec['sources'][0],kind=kind,path=name,local_path=name,url='https://dl.google.com/fixture/'+name,sha256=hashlib.sha256(data).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest(),size_bytes=len(data));s.update(extra);spec['sources'].append(s);return s
 def annotation(self,r,spec,**extra):
  p=r/'ann.zip'
  with zipfile.ZipFile(p,'w') as z:z.writestr('a.xml','<root><item name="a.A void go(java.lang.String)"><annotation name="android.annotation.RequiresPermission"><val name="value" val="&quot;android.permission.CAMERA&quot;"/></annotation></item></root>')
  self.source(r,spec,'sdk-annotations','ann.zip',p.read_bytes(),**extra)
 def test_shadowed_type_variable_does_not_match_java_lang_alias(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);spec=json.loads(inv.read_text());s=spec['sources'][0];data=(r/'api.txt').read_bytes().replace(b'public class A {',b'public class A<String> {');(r/'api.txt').write_bytes(data);s.update(sha256=hashlib.sha256(data).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest(),size_bytes=len(data));self.annotation(r,spec);inv.write_text(json.dumps(spec));b.build(inv,r/'out')
   db=sqlite3.connect(r/'out/catalog.sqlite');self.addCleanup(db.close);self.assertIsNone(db.execute('SELECT binding_id FROM annotation_evidence').fetchone()[0])
 def test_current_sdk_annotation_maps_to_exact_jvm_target(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);spec=json.loads(inv.read_text());data=b'<api version="4"><class name="a/A"><method name="go(Ljava/lang/String;)V"/></class></api>';records=sdk_api_index.parse(data)['records'];cache=r/'index.gz'
   with gzip.open(cache,'wt') as f:
    for row in records:f.write(json.dumps(row)+'\n')
   self.source(r,spec,'sdk-api-index','index.xml',data,platform_api_version='37.2',metadata_cache=dict(path=cache.name,sha256=hashlib.sha256(cache.read_bytes()).hexdigest(),records=len(records)))
   self.annotation(r,spec,platform_api_version='37.2');inv.write_text(json.dumps(spec));b.build(inv,r/'out')
   db=sqlite3.connect(r/'out/catalog.sqlite');self.addCleanup(db.close);bid=db.execute('SELECT binding_id FROM annotation_evidence').fetchone()[0];self.assertEqual(bid,hashlib.sha256(b'jvm:a/A#go(Ljava/lang/String;)V').hexdigest())
