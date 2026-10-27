import unittest
from native_literal_audit import declarations,registries,reference_names
class NativeLiterals(unittest.TestCase):
 def test_comment_and_quoted_macro_not_declaration(self):
  text='// IMPLEMENT_META_INTERFACE(X,"fake")\nconst char* s="IMPLEMENT_META_INTERFACE(X, fake)";\nIMPLEMENT_META_INTERFACE(Real,"android.Real")'
  rows=declarations(text);self.assertEqual(len(rows),1);self.assertEqual(rows[0]['literal_argument'],'android.Real');self.assertEqual(rows[0]['line'],3)
 def test_hybrid_and_nested_literal_arguments(self):
  rows=declarations('IMPLEMENT_HYBRID_META_INTERFACE(X,"android.X");\nIMPLEMENT_META_NESTED_INTERFACE(Outer,Inner,u"android.Inner");')
  self.assertEqual([r['literal_argument'] for r in rows],['android.X','android.Inner'])
 def test_computed_argument_unknown(self):
  self.assertIsNone(declarations('IMPLEMENT_META_INTERFACE(X, NAME_PREFIX + ".X")')[0]['literal_argument'])
 def test_registry_enumerated_and_expression_preserved(self):
  rows=registries('const auto kManualInterfaces[]={"a",/*comment*/"b"};\nconst auto kDownstreamManualInterfaces[]={PREFIX};')
  self.assertEqual(rows[0]['literal_names'],['a','b']);self.assertTrue(rows[0]['literal_only']);self.assertFalse(rows[1]['literal_only'])
 def test_prebuilt_registry_not_unioned_into_current_registry(self):
  rows=[dict(source=dict(repository='platform/frameworks/native',path='libs/binder/include/binder/IInterface.h'),literal_names=['current']),dict(source=dict(repository='platform/prebuilts/vndk/v30',path='old/IInterface.h'),literal_names=['old'])]
  self.assertEqual(reference_names(rows),{'current'})
 def test_cpp_raw_string_is_not_macro_declaration(self):
  text='const char* s=R"tag(// literal\nIMPLEMENT_META_INTERFACE(X,"fake"))tag";\nIMPLEMENT_META_INTERFACE(Real,"real")'
  self.assertEqual([r['literal_argument'] for r in declarations(text)],['real'])
