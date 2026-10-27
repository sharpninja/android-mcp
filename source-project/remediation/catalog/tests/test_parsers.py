import unittest,pathlib,sys,json
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
import parsers as p
class ParserTests(unittest.TestCase):
 def test_overloads_and_annotations(self):
  text='''package a {
 public @interface Ann {
 method public abstract String value();
 }
 public class Outer.Inner<T> {
 ctor public Outer.Inner();
 method @RequiresPermission("p") public <T extends java.lang.Object> java.util.List<T> go(@Nullable java.util.Map<String,java.util.List<T>>, int);
 method public void go(int);
 field public static final String S = "hello // no comment";
 }
 }'''
  rs=list(p.signature(text));self.assertFalse(any(r['kind']=='unsupported' for r in rs));self.assertEqual(rs[1]['kind'],'annotation');ms=[x for x in rs if x['name']=='go'];self.assertNotEqual(ms[0]['canonical'],ms[1]['canonical']);self.assertEqual(ms[0]['parameters'][0]['type'],'java.util.Map<String,java.util.List<T>>');self.assertEqual(ms[0]['return_type'],'java.util.List<T>');self.assertEqual(p.permission(ms[0]['annotations'])['name'],'p')
 def test_permission_modes(self):
  for text,op in [('@RequiresPermission(value="x")','permission'),('@RequiresPermission(allOf={"a","b"})','all'),('@RequiresPermission(anyOf={"a","b"},conditional=true)','conditional'),('@RequiresPermission.Read(@RequiresPermission("x"))','access'),('@PermissionManuallyEnforced','manual'),('@RequiresNoPermission','explicit-none'),('','unknown')]:self.assertEqual(p.permission(p.annotations(text)[1])['op'],op)
  self.assertEqual(p.permission(p.annotations('@RequiresPermission(android.Manifest.permission.X)')[1],{'android.Manifest.permission.X':'android.permission.X'})['name'],'android.permission.X')
  self.assertEqual(p.permission(p.annotations('@EnforcePermission("X")')[1],aidl=True)['op'],'unknown')
  self.assertEqual(p.permission(p.annotations('@RequiresPermission("carrier privileges")')[1])['op'],'unknown')
  self.assertEqual(p.permission(p.annotations('@RequiresPermission.Read')[1])['op'],'access')
 def test_parameter_permission_not_callable(self):
  r=p.member('method public void x(@RequiresPermission("p") String);','a.A','a');self.assertEqual(p.permission(r['annotations'])['op'],'unknown');self.assertTrue(r['parameters'][0]['annotations'])
 def test_aidl(self):
  text='''package a; import b.B;
 @EnforcePermission("android.permission.X") oneway interface IFoo {
 @RequiresNoPermission void ping(in B b, out int[] value) = 7;
 @PermissionManuallyEnforced oneway void send(in String s);
 const int C=3;
 }'''
  d=p.aidl(text);self.assertEqual(d['descriptor'],'a.IFoo');self.assertTrue(d['oneway']);self.assertEqual(len(d['members']),3);self.assertEqual(d['members'][0]['extras']['declared_method_id'],7);self.assertIsNone(d['members'][0]['extras']['wire_transaction_code']);self.assertEqual(d['members'][0]['parameters'][1]['direction'],'out');self.assertEqual(d['members'][2]['kind'],'unsupported');self.assertEqual(p.permission(d['annotations'],aidl=True)['name'],'android.permission.X')
 def test_source_unknown_retained(self):
  r=list(p.signature('package a {\n strange new declaration;\n}'));self.assertEqual(r[1]['kind'],'unsupported');self.assertEqual(r[1]['raw'],'strange new declaration;')
 def test_owner_reset_after_type(self):
  rs=list(p.signature('package a {\n public class A {\n method public void x();\n }\n method public void orphan();\n}'))
  self.assertEqual(rs[-1]['kind'],'unsupported');self.assertIn('without owner',rs[-1]['extras']['parse_error'])
 def test_comment_quoted(self):self.assertIn('"//x"',p.without_comments('const String X="//x"; // hi'))
 def test_aidl_structured(self):
  d=p.aidl('package a; @JavaDerive(toString=true) parcelable P { int n; }');self.assertEqual(d['descriptor'],'a.P');self.assertIn('retained',d['parse_status'])
if __name__=='__main__':unittest.main()
