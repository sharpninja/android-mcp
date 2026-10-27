import unittest
from aidl_frozen_hash import compute,verify
class Hash(unittest.TestCase):
 def test_google_hash_format_and_c_sort(self):
  self.assertEqual(compute({'p/B.aidl':b'b','p/A.aidl':b'a'},'2'),compute({'p/A.aidl':b'a','p/B.aidl':b'b'},'2'))
  self.assertEqual(compute({'p/A.aidl':b'a','p/B.aidl':b'b'},'2'),'f0e200d9c033149fe27eebb6633c4997feefdd6b')
 def test_version_one_uses_latest_version_literal(self):
  self.assertNotEqual(compute({'A.aidl':b'a'},'1'),compute({'A.aidl':b'a'},'2'))
 def test_only_last_nonempty_published_hash_is_selected(self):
  files={'A.aidl':b'a'};current=compute(files,'2')
  self.assertTrue(verify(files,['A.aidl'],'2','a'*40+'\n'+current+'\n\n')['verified'])
 def test_old_compatible_hash_cannot_mask_mismatching_last_hash(self):
  files={'A.aidl':b'a'};current=compute(files,'2')
  self.assertFalse(verify(files,['A.aidl'],'2',current+'\n'+'a'*40+'\n')['verified'])
 def test_full_tree_inputs_required(self):
  self.assertFalse(verify({'A.aidl':b'a'},['A.aidl','B.aidl'],'1','a'*40)['verified'])
 def test_current_is_not_a_frozen_version(self):
  with self.assertRaises(ValueError):compute({'A.aidl':b'a'},'current')
 def test_escaping_or_ambiguous_paths_not_hashed(self):
  for path in ('../A.aidl','/A.aidl','p\\A.aidl','p\nA.aidl'):
   with self.assertRaises(ValueError):compute({path:b'a'},'1')
