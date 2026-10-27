import unittest
from interface_module_facts import modules,literal,source_matches
class Modules(unittest.TestCase):
 def test_versions_imports_and_flags_are_literal_source_facts(self):
  rows=modules('aidl_interface { name:"foo", srcs:["p/*.aidl"], stability:"vintf", versions_with_info:[{version:"1",imports:["bar-V2"]}], frozen:true, }')
  self.assertEqual(len(rows),1);self.assertEqual(literal(rows[0]['properties'],'name'),'foo');self.assertTrue(literal(rows[0]['properties'],'frozen'));self.assertEqual(literal(rows[0]['properties'],'versions_with_info')[0]['imports'],['bar-V2'])
 def test_variables_and_concatenation_not_evaluated(self):
  row=modules('aidl_interface { name:"foo", srcs: common + ["x.aidl"], versions:VERSIONS, }')[0]
  self.assertIsNone(literal(row['properties'],'srcs'));self.assertIsNone(literal(row['properties'],'versions'))
 def test_fake_module_in_string_or_comment_ignored(self):
  text='// aidl_interface { name:"fake" }\n x="hidl_interface { fake }"; hidl_interface { name:"real", }'
  self.assertEqual([literal(v['properties'],'name') for v in modules(text)],['real'])
 def test_duplicate_property_stays_unknown(self):
  row=modules('aidl_interface { name:"one", name:"two", }')[0];self.assertIsNone(literal(row['properties'],'name'))
 def test_source_glob_is_repo_relative_and_exclusions_apply(self):
  self.assertEqual(source_matches('dir/Android.bp',['p/**/*.aidl'],['p/test/**'],['dir/p/A.aidl','dir/p/sub/B.aidl','dir/p/test/T.aidl','dir/aidl_api/x/1/p/A.aidl']),['dir/p/A.aidl','dir/p/sub/B.aidl'])
 def test_source_reference_and_parent_escape_not_followed(self):
  with self.assertRaises(ValueError):source_matches('dir/Android.bp',['../*.aidl'],[],['outside.aidl'])
  with self.assertRaises(ValueError):source_matches('dir/Android.bp',[':filegroup'],[],['dir/A.aidl'])
 def test_unclosed_module_is_explicit_unknown(self):
  self.assertEqual(modules('aidl_interface { name:"bad",')[0]['status'],'unclosed-module-block')
 def test_adjacent_modules_are_not_one_expression(self):
  rows=modules('aidl_interface { name:"a", } hidl_interface { name:"b", } cc_library { name:"unrelated", }')
  self.assertEqual([literal(v['properties'],'name') for v in rows],['a','b']);self.assertTrue(all(v['status']=='static-properties-parsed' for v in rows))
 def test_missing_property_value_is_preserved(self):
  self.assertEqual(modules('aidl_interface { name:')[0]['status'],'unclosed-module-block')
