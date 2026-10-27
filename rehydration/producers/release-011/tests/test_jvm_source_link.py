import unittest,pathlib,sys
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'tooling'))
import jvm_source_link as link
class JvmLinkTests(unittest.TestCase):
 def test_exact_owner_primitives_and_known_reference_type(self):
  owners={'a.C':'a/C','java.lang.String':'java/lang/String'}
  r=dict(kind='method',owner='a.C',name='go',return_type='void',parameters=[dict(type='java.lang.String[]'),dict(type='int')],extras=dict(owner_binary_name='a/C'))
  self.assertEqual(link.key(r,owners),'a/C#go([Ljava/lang/String;I)V')
 def test_unresolved_type_variable_never_guessed(self):
  r=dict(kind='method',owner='a.C',name='go',return_type='T',parameters=[],extras=dict(owner_binary_name='a/C'))
  self.assertIsNone(link.key(r,{'a.C':'a/C'}))
 def test_unqualified_java_lang_name_might_be_shadowed(self):
  self.assertIsNone(link.encode('String','a.C',{'java.lang.String':'java/lang/String'}))
 def test_untyped_index_field_cannot_prove_typed_jvm_identity(self):
  r=dict(kind='field',owner='a.C.Inner',name='X',extras=dict(owner_binary_name='a/C$Inner'))
  self.assertIsNone(link.key(r,{'a.C.Inner':'a/C$Inner'}))
