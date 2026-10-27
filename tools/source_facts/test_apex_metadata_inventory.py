import unittest
from apex_metadata_inventory import select_paths
class Inventory(unittest.TestCase):
 def test_manifest_and_nearest_build_selected(self):
  rows,binary=select_paths(['a/apex_manifest.json','a/Android.bp','Android.bp','other.txt'])
  self.assertEqual(set(rows),{'a/apex_manifest.json','a/Android.bp'});self.assertEqual(binary,[])
 def test_binary_manifest_is_an_explicit_exclusion(self):
  rows,binary=select_paths(['apex_manifest.pb','Android.bp']);self.assertEqual(rows,{});self.assertEqual(binary,['apex_manifest.pb'])
 def test_templates_not_treated_as_literal_manifests(self):
  rows,_=select_paths(['apex_manifest.json.in','Android.bp']);self.assertEqual(rows,{})
