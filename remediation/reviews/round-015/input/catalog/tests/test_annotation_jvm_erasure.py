import pathlib,sys,unittest
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
from annotation_link import xml_jvm_key
class XmlJvmErasure(unittest.TestCase):
 owners={'a.A':'a/A','a.Callback':'a/Callback','java.lang.String':'java/lang/String','a.Result':'a/Result','a.A.Inner':'a/A$Inner'}
 def test_qualified_generic_outer_erasure_has_exact_descriptor(self):
  key=xml_jvm_key('a.A a.Result<java.lang.String> call(a.Callback<a.Result>, java.lang.String)',self.owners)
  self.assertEqual(key,'a/A#call(La/Callback;Ljava/lang/String;)La/Result;')
 def test_type_variable_is_not_erased_to_object_by_guess(self):
  self.assertIsNone(xml_jvm_key('a.A void call(T)',self.owners));self.assertIsNone(xml_jvm_key('a.A T call()',self.owners))
 def test_unqualified_java_lang_alias_not_guessed(self):
  self.assertIsNone(xml_jvm_key('a.A void call(String)',self.owners))
 def test_parameterized_array_and_parameter_annotation_ordinal(self):
  self.assertEqual(xml_jvm_key('a.A void call(a.Callback<T>[]) 0',self.owners),'a/A#call([La/Callback;)V')
 def test_malformed_generics_and_void_parameters_rejected(self):
  self.assertIsNone(xml_jvm_key('a.A void call(a.Callback<T)',self.owners));self.assertIsNone(xml_jvm_key('a.A void call(void)',self.owners))
 def test_constructor_and_same_name_regular_method_differ(self):
  self.assertEqual(xml_jvm_key('a.A A()',self.owners),'a/A#<init>()V');self.assertEqual(xml_jvm_key('a.A int A()',self.owners),'a/A#A()I')
 def test_nested_binary_owner_is_known_not_inferred(self):
  self.assertEqual(xml_jvm_key('a.A a.A.Inner call()',self.owners),'a/A#call()La/A$Inner;')
