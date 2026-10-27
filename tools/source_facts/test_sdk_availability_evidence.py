import unittest
from sdk_availability_evidence import enumerate_evidence
class AvailabilitySourceFacts(unittest.TestCase):
 def test_member_keeps_own_and_owner_constraints_separate(self):
  doc=b'<api version="4"><sdk id="30" name="R"/><class name="a/C" since="30" removed="35" sdks="30:4" module="m"><method name="m()V" since="31" removed="36"/></class></api>'
  rows,sdks=enumerate_evidence(doc)
  self.assertEqual(len(rows),2);self.assertEqual(sdks,[{'id':'30','name':'R'}])
  self.assertEqual(rows[1]['owner_attributes']['removed'],'35')
  self.assertEqual(rows[1]['attributes']['removed'],'36')
  self.assertEqual(rows[1]['owner_attributes']['sdks'],'30:4')
 def test_absence_remains_absent_and_future_history_not_filtered(self):
  rows,_=enumerate_evidence(b'<api version="4"><class name="a/C" since="38"><field name="f"/></class></api>')
  self.assertEqual(len(rows),2);self.assertEqual(rows[1]['attributes'],{'name':'f'})
  self.assertNotIn('removed',rows[0]['attributes'])
  self.assertGreater(rows[1]['line'],0)
 def test_new_attributes_preserved(self):
  rows,_=enumerate_evidence(b'<api version="4"><class name="a/C" since="37.2" future="raw"/></api>')
  self.assertEqual(rows[0]['attributes']['future'],'raw')
