import unittest
from apex_manifest_facts import manifest,build_modules,explicit_manifest_path
class Facts(unittest.TestCase):
 def test_source_comments_retained_with_version_placeholder_unknown(self):
  text='{"name":"com.android.foo", // Placeholder version replaced during build\n "version":0}'
  row=manifest(text);self.assertEqual(row['normalized_version_code'],'0');self.assertEqual(row['raw_source_text'],text);self.assertIsNotNone(row['comment_normalization']);self.assertIn('unknown_reason',row['applicability'])
 def test_comment_markers_inside_strings_are_not_removed(self):
  row=manifest('{"name":"url://not-a-comment","version":1}')
  self.assertEqual(row['name'],'url://not-a-comment');self.assertIsNone(row['comment_normalization'])
 def test_source_version_not_turned_into_installed_range(self):
  result=manifest('{"name":"com.android.foo","version":1}')
  self.assertEqual(result['normalized_version_code'],'1');self.assertFalse(result['installed_identity_or_version_proven']);self.assertIn('unknown_reason',result['applicability']);self.assertNotIn('modules',result['applicability'])
 def test_proto_json_string_version_retained(self):
  self.assertEqual(manifest('{"version":"9223372036854775807"}')['normalized_version_code'],'9223372036854775807')
 def test_bad_versions_unknown_without_discarding_raw(self):
  for value in (True,-1,'${BUILD_VERSION}',2**63):
   import json
   result=manifest(json.dumps(dict(version=value)));self.assertIsNone(result['normalized_version_code']);self.assertEqual(result['raw_properties']['version'],value)
 def test_duplicate_name_is_not_silently_selected(self):
  self.assertEqual(manifest('{"name":"a","name":"b"}')['status'],'ambiguous-json')
 def test_malformed_json_preserved(self):
  self.assertEqual(manifest('{unfinished')['raw'],'{unfinished')
 def test_literal_build_manifest_link_not_defaults_inference(self):
  rows=build_modules('apex_defaults {name:"d",manifest:"m.json"} apex {name:"foo",defaults:["d"]} apex {name:"bar",manifest:"bar.json"}')
  self.assertEqual(len(rows),3);self.assertIsNone(explicit_manifest_path('dir/Android.bp',rows[1]));self.assertEqual(explicit_manifest_path('dir/Android.bp',rows[2]),'dir/bar.json')
 def test_manifest_variables_and_filegroups_unresolved(self):
  for value in ('manifest_name','":filegroup"','"../m.json"'):
   row=build_modules('apex {name:"foo",manifest:'+value+'}')[0];self.assertIsNone(explicit_manifest_path('Android.bp',row))
