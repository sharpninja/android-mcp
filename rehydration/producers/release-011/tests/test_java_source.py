"""SDK source AST acceptance fixtures, written before Java-source parser."""
import unittest,pathlib,sys
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
try:import java_source
except ImportError:java_source=None
class JavaSourceTests(unittest.TestCase):
 def setUp(self):self.assertIsNotNone(java_source,'Java source metadata parser not implemented yet')
 def test_nested_overloads_fields_and_annotations(self):
  text='''package a; import java.util.List; import android.annotation.RequiresPermission;
  public class Outer<T> {
   public static final String P="android.permission.CAMERA", Q="android.permission.RECORD_AUDIO";
   public Outer() {}
   @RequiresPermission(value="android.permission.CAMERA",conditional=true)
   public List<String> go(@Deprecated final String x, int... ns) { return null; }
   public void go(int n) {}
   public static class Inner { public int value; }
   public @interface A { String value() default ""; }
  }'''
  result=java_source.parse(text,'Fixture.java');rs=result['records'];self.assertEqual(result['parse_errors'],0)
  self.assertIn('a.Outer.Inner',{r['owner'] for r in rs});self.assertEqual(sum(r['name']=='go' for r in rs),2)
  self.assertEqual({r['name'] for r in rs if r['kind']=='field' and r['owner']=='a.Outer'},{'P','Q'})
  method=next(r for r in rs if r['name']=='go' and len(r['parameters'])==2)
  self.assertEqual(method['parameters'][1]['type'],'int...');self.assertTrue(method['parameters'][0]['annotations'])
  self.assertEqual(method['annotations'][0]['name'],'android.annotation.RequiresPermission');self.assertTrue(any(r['kind']=='annotation' for r in rs))
 def test_method_body_local_types_not_public_surface(self):
  rs=java_source.parse('package a; public class C { public void x(){ class Local { int y; } } }','C.java')['records']
  self.assertFalse(any(r['name']=='Local' for r in rs));self.assertFalse(any(r['name']=='y' for r in rs))
 def test_interface_implicit_public_and_directions_unknown(self):
  rs=java_source.parse('package a; public interface I { String NAME="p"; void f(String p); }','I.java')['records']
  self.assertEqual(next(r for r in rs if r['name']=='f')['extras']['effective_visibility'],'public')
  self.assertIsNone(next(r for r in rs if r['name']=='f')['parameters'][0]['direction'])
 def test_parse_error_retained(self):
  r=java_source.parse('package a; public class X { public void broken( }','X.java');self.assertGreater(r['parse_errors'],0);self.assertTrue(r['issues'])
 def test_enum_and_multiple_array_declarators(self):
  rs=java_source.parse('package a; public enum E { RED,BLUE; public int a[],b; }','E.java')['records']
  self.assertEqual({r['name'] for r in rs if r['extras'].get('enum_constant')},{'RED','BLUE'})
  self.assertEqual(next(r for r in rs if r['kind']=='field' and r['name']=='a')['return_type'],'int[]');self.assertEqual(next(r for r in rs if r['kind']=='field' and r['name']=='b')['return_type'],'int')
 def test_repeated_large_line_numbers(self):
  text='\n'*300+'package a;\npublic class C {\n public int x;\n public void f(int n){}\n}'
  for _ in range(100):
   rows=java_source.parse(text,'Large.java')['records']
   self.assertEqual(next(r for r in rows if r['kind']=='field')['line'],303)
 def test_annotation_like_comment_is_not_metadata(self):
  rows=java_source.parse('package a; public /* @NotAnAnnotation( */ class C { public /* @other( */ int x; }','Comments.java')['records']
  self.assertEqual(next(r for r in rows if r['kind']=='field')['name'],'x')
  self.assertFalse(any(r['annotations'] for r in rows))
 def test_apostrophe_in_annotation_comment(self):
  text='''package a; @LongDef(value={1, // because it'd wrap\n 2}) public @interface Bits {}'''
  rows=java_source.parse(text,'Bits.java')['records']
  annotation=next(r for r in rows if r['kind']=='annotation')
  self.assertEqual(len(annotation['annotations']),1)
  self.assertIn('1,',annotation['annotations'][0]['arguments'])
if __name__=='__main__':unittest.main()
