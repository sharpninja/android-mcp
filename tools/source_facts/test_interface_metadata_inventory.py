import unittest
from interface_metadata_inventory import select_paths
class Selection(unittest.TestCase):
 def test_all_stable_hash_versions_selected(self):
  paths=['aidl_api/a/1/.hash','aidl_api/a/2/.hash','aidl_api/a/current/a.aidl','not_aidl_api/a/.hash']
  self.assertEqual(set(select_paths(paths,[]) ),set(paths[:2]))
 def test_nearest_build_ancestors(self):
  result=select_paths(['Android.bp','sub/Android.bp','sub/Android.mk','sub/deep/A.aidl'],['sub/deep/A.aidl'])
  self.assertEqual(set(result),{'sub/Android.bp','sub/Android.mk'})
 def test_hidl_hash_indexes_require_hal_scope(self):
  result=select_paths(['current.txt','hal/current.txt','api/current.txt','hal/1.0/IFoo.hal'],['hal/1.0/IFoo.hal'])
  self.assertEqual(set(result),{'current.txt','hal/current.txt'})
