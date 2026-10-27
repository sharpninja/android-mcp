import unittest
from native_descriptor_facts import parse
class Descriptor(unittest.TestCase):
 def test_rust_macro_literal_and_symbol_are_source_facts(self):
  row=parse('binder::declare_binder_interface! { IFoo["android.foo.IFoo"] {native: BnFoo(on_transact),proxy:BpFoo,} }','rust')['facts'][0];self.assertEqual(row['descriptor_literal'],'android.foo.IFoo');self.assertFalse(row['compiled_endpoint_proven'])
 def test_rust_raw_string_descriptor(self):
  row=parse('declare_binder_interface! { IFoo[r#"android.foo.IFoo"#] {} }','rust')['facts'][0];self.assertEqual(row['descriptor_literal'],'android.foo.IFoo')
 def test_mentions_in_comments_and_raw_strings_not_calls(self):
  text='// AIBinder_Class_define("fake",a,b,c);\nauto s=R"tag(AIBinder_Class_define("fake",a,b,c))tag";'
  self.assertEqual(parse(text,'cpp')['facts'],[])
 def test_rust_nested_comments_and_raw_strings_not_calls(self):
  text='/* outer /* inner */ declare_binder_interface! {Fake["fake"]{}} */ let s=r##"declare_binder_interface! {Fake["fake"]{}}"##;'
  self.assertEqual(parse(text,'rust')['facts'],[])
 def test_ndk_symbol_argument_retained_unresolved(self):
  row=parse('AIBinder_Class_define(IFoo::descriptor,create,destroy,transact);','cpp')['facts'][0];self.assertIsNone(row['descriptor_literal']);self.assertIn('IFoo::descriptor',row['descriptor_expression'])
 def test_cpp_macro_descriptor_and_nested_arguments(self):
  row=parse('IMPLEMENT_META_INTERFACE(Foo,"android.foo.IFoo"); AIBinder_Class_define("android.X",factory(a,b),destroy,transact);','cpp')['facts'];self.assertEqual([r['descriptor_literal'] for r in row],['android.foo.IFoo','android.X'])
 def test_macro_definition_not_an_invocation(self):
  self.assertEqual(parse('#define IMPLEMENT_META_INTERFACE(Foo,Name) body','cpp')['facts'],[]);self.assertEqual(parse('macro_rules! declare_binder_interface {($x:tt)=>{}}','rust')['facts'],[])
 def test_unclosed_form_preserved_as_unknown(self):
  row=parse('AIBinder_Class_define("name",create,','cpp')['facts'][0];self.assertEqual(row['status'],'unclosed-token-form')
