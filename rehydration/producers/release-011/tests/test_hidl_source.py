import unittest,pathlib,sys
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
from hidl_source import parse
class Hidl(unittest.TestCase):
 def test_interface_nested_type_and_following_method(self):
  result=parse('package android.hardware.test@1.0; interface IFoo { struct Point { int32_t x; }; get(vec<Point> points) generates (bool ok, Point[3] triangle); };')
  self.assertEqual([r['name'] for r in result['records']],['android.hardware.test@1.0','IFoo','Point','x','get']);self.assertEqual(result['unsupported'],[])
  method=result['records'][-1];self.assertEqual(method['extras']['results'][1]['type'],'Point[3]');self.assertIsNone(method['extras']['wire_transaction_id'])
 def test_shifts_dont_consume_enum_values_or_following_method(self):
  result=parse('package p@1.0; interface I { enum Flags : uint32_t { A=1<<2, B=A|2, C }; ping(); };')
  self.assertEqual([r['name'] for r in result['records']][-4:],['A','B','C','ping'])
 def test_nested_template_comma_separation(self):
  result=parse('package p@1.0; interface I { m(vec<vec<int32_t>> a, bitfield<Flags> b); };');method=result['records'][-1]
  self.assertEqual([p['type'] for p in method['parameters']],['vec<vec<int32_t>>','bitfield<Flags>'])
 def test_annotations_with_braces_and_semicolon_string_are_not_bodies(self):
  result=parse('package p@1.0; @Callflow(next={"a;","b"}) interface I { @entry m(); };')
  self.assertEqual([r['name'] for r in result['records']],['p@1.0','I','m']);self.assertEqual(len(result['records'][1]['all_annotations']),1)
 def test_comments_and_line_offsets(self):
  result=parse('\n'*300+'// interface IFake {};\npackage p@1.0; interface I {};')
  self.assertEqual(len(result['records']),2);self.assertEqual(result['records'][1]['line'],302)
 def test_unknown_statement_and_missing_package_retained(self):
  result=parse('package p@1.0; mystery ?;');self.assertEqual(result['records'][-1]['kind'],'unsupported');self.assertEqual(len(result['unsupported']),1)
  self.assertEqual(parse('interface I {};')['records'][0]['kind'],'unsupported')
 def test_oneway_with_results_not_silently_accepted(self):
  result=parse('package p@1.0; interface I { oneway m() generates (bool ok); };');self.assertEqual(result['records'][-1]['kind'],'unsupported')
 def test_source_hidl_version_is_not_android_availability(self):
  result=parse('package p@2.7; interface I {};');r=result['records'][1]
  self.assertEqual(r['extras']['hidl_package_version'],'2.7');self.assertEqual(r['extras']['android_availability'],'unknown')
 def test_typedef_and_safe_union(self):
  result=parse('package p@1.0; typedef vec<int32_t> Values; safe_union Value { int32_t number; string text; };')
  self.assertEqual([r['name'] for r in result['records']],['p@1.0','Values','Value','number','text'])
 def test_type_canonical_does_not_repeat_own_name(self):
  result=parse('package p@1.0; interface I { struct Point { int32_t x; }; };')
  self.assertEqual(result['records'][1]['canonical'],'p@1.0::I');self.assertEqual(result['records'][2]['canonical'],'p@1.0::I.Point')
 def test_line_of_method_after_leading_blank_lines(self):
  result=parse('package p@1.0;\ninterface I {\n\n m();\n};')
  self.assertEqual(result['records'][-1]['line'],4)
 def test_missing_semicolon_and_nested_interface_retained_unknown(self):
  self.assertTrue(parse('package p@1.0; interface I {}')['unsupported'])
  self.assertTrue(parse('package p@1.0; interface I { interface INested {}; };')['unsupported'])
 def test_interface_special_type_is_a_field_not_nested_declaration(self):
  result=parse('package p@1.0; struct Holder { interface instance; };')
  self.assertEqual(result['records'][-1]['kind'],'field');self.assertEqual(result['records'][-1]['return_type'],'interface');self.assertEqual(result['unsupported'],[])
 def test_type_delimiter_can_touch_variable_name(self):
  result=parse('package p@1.0; interface I { m(vec<uint8_t>input) generates (uint32_t[3]output); };')
  self.assertEqual(result['records'][-1]['parameters'][0]['name'],'input');self.assertEqual(result['records'][-1]['extras']['results'][0]['name'],'output');self.assertEqual(result['unsupported'],[])
