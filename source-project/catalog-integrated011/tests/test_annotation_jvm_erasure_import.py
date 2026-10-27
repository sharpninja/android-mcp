import unittest,pathlib,tempfile,sys,json,gzip,sqlite3,contextlib,zipfile,hashlib
from xml.sax.saxutils import escape
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
import build_catalog,sdk_api_index,test_source_annotation_targets
class XmlJvmImport(unittest.TestCase):
 def check(self,item,expected,platform='37.2',methods=None):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory);h=test_source_annotation_targets.SourceTargets();inv=h.fixture(root);spec=json.loads(inv.read_text());methods=methods or '<method name="call(La/Callback;)La/Result;"/>';data=('<api version="4"><class name="a/A">'+methods+'</class><class name="a/Callback"/><class name="a/Result"/><class name="java/lang/String"/></api>').encode();records=sdk_api_index.parse(data,target='37.2')['records'];cache=root/'index.gz'
   with gzip.open(cache,'wt') as stream:
    for row in records:stream.write(json.dumps(row)+'\n')
   h.source(root,spec,'sdk-api-index','index.xml',data,platform_api_version='37.2',metadata_cache=dict(path=cache.name,sha256=hashlib.sha256(cache.read_bytes()).hexdigest(),records=len(records)))
   archive=root/'annotations.zip'
   with zipfile.ZipFile(archive,'w') as z:z.writestr('a.xml','<root><item name="'+escape(item,{'"':'&quot;'})+'"><annotation name="androidx.annotation.RequiresPermission"><val name="value" val="&quot;android.permission.CAMERA&quot;"/></annotation></item></root>')
   h.source(root,spec,'sdk-annotations','annotations.zip',archive.read_bytes(),platform_api_version=platform);inv.write_text(json.dumps(spec));build_catalog.build(inv,root/'out')
   with contextlib.closing(sqlite3.connect(root/'out/catalog.sqlite')) as db:
    bid,raw=db.execute('SELECT binding_id,document_json FROM annotation_evidence').fetchone();evidence=json.loads(raw)
    self.assertEqual(bid,hashlib.sha256(expected.encode()).hexdigest() if expected else None)
    if expected:
     self.assertEqual(db.execute("SELECT count(*) FROM permission_relations WHERE binding_id=? AND annotation='external'",(bid,)).fetchone()[0],1)
    return evidence
 def test_generic_outer_erasure_matches_exact_current_descriptor(self):
  evidence=self.check('a.A a.Result<java.lang.String> call(a.Callback<T>)','jvm:a/A#call(La/Callback;)La/Result;');self.assertEqual(evidence['target_match_kind'],'current-sdk-index-jvm-erasure-exact')
 def test_top_level_type_variable_not_guessed(self):
  self.check('a.A a.Result call(T)',None)
 def test_current_index_is_not_borrowed_for_other_platform(self):
  self.check('a.A a.Result<java.lang.String> call(a.Callback<T>)',None,platform='37.0')
 def test_regular_method_with_class_name_is_not_constructor(self):
  self.check('a.A int A()','jvm:a/A#A()I',methods='<method name="&lt;init>()V"/><method name="A()I"/>')
