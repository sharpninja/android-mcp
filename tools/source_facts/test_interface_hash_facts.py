import unittest
from interface_hash_facts import aidl_hashes,hidl_hashes
class HashFacts(unittest.TestCase):
 def test_aidl_hash_lines_kept_in_order_no_compiled_hash_assumption(self):
  result=aidl_hashes('# source comment\n'+'a'*40+'\n'+'b'*40+'\n')
  self.assertEqual([row['literal'] for row in result['facts']],['a'*40,'b'*40]);self.assertFalse(result['compiled_runtime_hash_proven']);self.assertEqual(result['unknown_lines'],[])
 def test_unknown_aidl_hash_line_retained(self):
  self.assertEqual(aidl_hashes('not-frozen\n')['unknown_lines'][0]['raw'],'not-frozen')
 def test_hidl_duplicate_identifier_hashes_are_not_collapsed(self):
  text='a'*64+' android.hardware.foo@1.0::IFoo\n'+'b'*64+' android.hardware.foo@1.0::IFoo # old compatibility hash\n';result=hidl_hashes(text)
  self.assertEqual(len(result['facts']),2);self.assertEqual(result['facts'][0]['declared_identifier'],'android.hardware.foo@1.0::IFoo');self.assertFalse(result['android_applicability_proven'])
 def test_hidl_unknown_format_remains_evidence(self):
  self.assertEqual(hidl_hashes('opaque declaration\n')['unknown_lines'][0]['raw'],'opaque declaration')
