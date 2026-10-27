import unittest,json
from native_abi_facts import parse
class Abi(unittest.TestCase):
 def fixture(self):
  return dict(builtin_types=[dict(linker_set_key='VOID',name='void')],function_types=[dict(linker_set_key='CB',return_type='VOID',parameters=[])],pointer_types=[dict(linker_set_key='PTR_CB',referenced_type='CB')],functions=[dict(linker_set_key='register_a',function_name='register_a',return_type='VOID',parameters=[dict(referenced_type='PTR_CB')])])
 def test_pointer_to_function_parameter_is_structural_callback_candidate(self):
  result=parse(json.dumps(self.fixture()));self.assertEqual(len(result['callback_parameter_candidates']),1);candidate=result['callback_parameter_candidates'][0];self.assertEqual(candidate['parameter_index'],0);self.assertFalse(candidate['lifecycle_proven']);self.assertFalse(result['wire_or_runtime_proven'])
 def test_missing_type_reference_is_retained(self):
  value=self.fixture();value['pointer_types'][0]['referenced_type']='MISSING';result=parse(json.dumps(value));self.assertEqual(result['reference_counts']['unresolved'],1)
 def test_duplicate_type_identity_not_silently_selected(self):
  value=self.fixture();value['record_types']=[dict(linker_set_key='CB',name='collision')];result=parse(json.dumps(value));self.assertIn('CB',result['duplicate_type_keys']);self.assertEqual(result['callback_parameter_candidates'],[])
 def test_unknown_root_collection_preserved(self):
  result=parse('{"future_collection":[{"opaque":7}]}');self.assertEqual(result['unknown_root_fields']['future_collection'],[dict(opaque=7)])
 def test_duplicate_json_keys_preserved_as_unsupported_source(self):
  self.assertEqual(parse('{"functions":[],"functions":[{}]}')['status'],'unsupported-json')
 def test_nonfinite_json_is_not_a_valid_interchange_number(self):
  self.assertEqual(parse('{"functions":[NaN]}')['status'],'unsupported-json')
 def test_mangled_overloads_not_collapsed_by_display_name(self):
  result=parse('{"functions":[{"linker_set_key":"f_int","function_name":"f"},{"linker_set_key":"f_string","function_name":"f"}]}');self.assertEqual(len(result['records']),2);self.assertEqual({r['local_key'] for r in result['records']},{'f_int','f_string'})
